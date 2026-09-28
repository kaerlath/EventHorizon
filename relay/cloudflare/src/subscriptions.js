import {RelayError} from './model.js';
import {requireViewer} from './community.js';

const subscriptionKey=(id,userId)=>`google-sub:${id}:${userId}`;
const jobKey=(id,userId)=>`google-job:sub:${id}:${userId}`;

export class CalendarSubscriptions {
  constructor(core){this.core=core;}
  get store(){return this.core.store;}
  async queue(sub,action='sync'){
    await this.store.put(jobKey(sub.id,sub.userId),{kind:'subscription',id:sub.id,userId:sub.userId,action,subject:sub.subject,calendarId:sub.calendarId,eventId:sub.eventId,attempts:0,due:Date.now()});
    await this.store.setAlarm(Date.now()+60000);
  }
  async list(userId){
    const result=[];
    for(const [,sub] of await this.store.list({prefix:'google-sub:'}))if(sub.userId===userId && sub.status!=='removed'){
      const job=await this.store.get(jobKey(sub.id,userId));
      result.push({id:sub.id,title:sub.title,status:job?(job.attempts>=6?'Needs attention':job.action==='remove'?'Removing':'Queued'):sub.status,active:sub.active});
    }
    return result;
  }
  async change(login,id,remove=false){
    const key=subscriptionKey(id,login.userId),old=await this.store.get(key),saved=await this.store.get('event:'+id);
    if(remove&&old?.status==='removed')return {message:'Your calendar copy has already been removed.'};
    const legacy=saved?.userId===login.userId && saved.record?.googleCalendarSync;
    if(remove&&!old&&!legacy)return {message:'This event has no personal calendar subscription.'};
    if(!remove)await requireViewer(this.core,login.userId,saved);
    this.core.google.requireConfigured();
    const c=await this.store.get('google:'+login.userId);
    if(!c?.calendarId)throw new RelayError('Connect Google and prepare your calendar in Connection settings first.');
    if(remove&&old&&(old.subject!==c.subject||old.calendarId!==c.calendarId))throw new RelayError('Reconnect the Google account that holds this copy before removing it.');
    if(!remove&&old?.status==='Removing')throw new RelayError('Wait for the pending removal to finish before adding this event again.',409);
    const reusable=old && old.status!=='removed' && old.subject===c.subject && old.calendarId===c.calendarId;
    const sub={id,userId:login.userId,subject:c.subject,calendarId:c.calendarId,
      eventId:reusable?old.eventId:legacy?id.replaceAll('-',''):id.replaceAll('-','')+crypto.randomUUID().replaceAll('-','').slice(0,12),
      title:remove?(old?.title??saved.record.title):saved.record.title,active:!remove,status:remove?'Removing':'Queued'};
    await this.store.put(key,sub);
    // The old organizer checkbox also targets this user's copy. Convert it so it
    // cannot silently recreate an entry after an explicit personal removal.
    if(saved?.userId===login.userId){saved.record.googleCalendarSync=false;await this.store.put('event:'+id,saved);await this.store.delete('google-job:'+id);}
    await this.queue(sub,remove?'remove':'sync');
    return {message:remove?'Removal queued for your Google copy only.':'Added to your calendar subscriptions. Your copy will follow organizer updates.'};
  }
  async fanout(id){
    for(const [,sub] of await this.store.list({prefix:`google-sub:${id}:`}))if(sub.active){await this.queue(sub);}
  }
  async retry(login){
    let count=0;
    for(const [,sub] of await this.store.list({prefix:'google-sub:'}))if(sub.userId===login.userId && sub.status!=='removed'){
      if(sub.status==='Removing'){await this.change(login,sub.id,true);count++;}
      else if(sub.active){await this.change(login,sub.id);count++;}
    }
    return count;
  }
  async disconnect(userId){
    for(const [key,sub] of await this.store.list({prefix:'google-sub:'}))if(sub.userId===userId&&sub.status!=='removed'){
      sub.active=false;sub.status=sub.status==='Removing'?'Removing':'Disconnected';await this.store.put(key,sub);
    }
  }
  async process(job){
    const key=subscriptionKey(job.id,job.userId),sub=await this.store.get(key),c=await this.store.get('google:'+job.userId);
    if(!sub||sub.eventId!==job.eventId)return;
    if(!c||c.subject!==job.subject||c.calendarId!==job.calendarId){
      if(job.action==='remove')throw new RelayError('Reconnect the original Google account to remove this copy.',409);
      return;
    }
    const google=this.core.google;
    const path='calendars/'+encodeURIComponent(c.calendarId)+'/events/'+sub.eventId;
    if(job.action==='remove'){
      try{await google.api(job.userId,c,'DELETE',path);}
      catch(e){if(![404,410].includes(e.googleStatus))throw e;}
      sub.active=false;sub.status='removed';await this.store.put(key,sub);return;
    }
    if(!sub.active)return;
    const saved=await this.store.get('event:'+job.id);
    try{await requireViewer(this.core,job.userId,saved);}
    catch(e){
      if(e instanceof RelayError&&[403,404].includes(e.status)){
        sub.active=false;sub.status='Access unavailable';await this.store.put(key,sub);return;
      }
      throw e;
    }
    const payload=google.eventPayload(saved);
    try{await google.api(job.userId,c,'POST','calendars/'+encodeURIComponent(c.calendarId)+'/events',{id:sub.eventId,...payload});}
    catch(e){if(e.googleStatus===409)await google.api(job.userId,c,'PATCH',path,payload);else throw e;}
    sub.title=saved.record.title;sub.status='Synced';await this.store.put(key,sub);
  }
}
