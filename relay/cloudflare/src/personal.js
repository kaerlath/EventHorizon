import {itinerary} from './model.js';
import {RelayError,validate,schedule} from './model.js';

// Private records never enter the event: namespace used by community APIs.
const key=(user,id)=>`personal:${user}:${id}`;
const jobKey=(user,id)=>`google-job:personal:${user}:${id}`;
export class PersonalEvents {
  constructor(core){this.core=core;this.store=core.store;}
  async change(login,id,raw,remove=false){
    const k=key(login.userId,id),old=await this.store.get(k);
    if(remove&&!old)return {message:'No Google copy exists for this account.'};
    const c=await this.store.get('google:'+login.userId);
    this.core.google.requireConfigured();
    if(!c?.calendarId)throw new RelayError('Connect Google and prepare your calendar in Connection settings first.');
    if(old&&old.status!=='removed'&&(old.subject!==c.subject||old.calendarId!==c.calendarId))throw new RelayError('Reconnect the original Google account before changing this copy.',409);
    if(!remove&&old?.status==='Removing')throw new RelayError('Wait for Google removal to finish before adding again.',409);
    let record=old?.record;
    if(!remove){
      const data=Object.fromEntries(Object.entries(raw??{}).map(([k,v])=>[k[0].toLowerCase()+k.slice(1),v]));
      if(data.personalOnly!==true||data.discordEventId||data.discordMessageId)throw new RelayError('Only unpublished personal events can use private sync.');
      if(typeof data.title!=='string'||!data.title.trim())throw new RelayError('Enter a title.');
      // Reuse time/length validation but discard every community destination.
      record=validate({...data,guildId:'0',channelId:'',server:'',channel:'',recurrence:'None'},id,0,true).item;
      record.location=data.location??'';record.personalOnly=true;
    }
    const saved={record,userId:login.userId,subject:c.subject,calendarId:c.calendarId,
      eventId:old&&old.status!=='removed'?old.eventId:crypto.randomUUID().replaceAll('-',''),status:remove?'Removing':'Queued'};
    await this.store.put(k,saved);
    await this.store.put(jobKey(login.userId,id),{kind:'personal',id,userId:login.userId,subject:c.subject,calendarId:c.calendarId,eventId:saved.eventId,remove,attempts:0,due:Date.now()});
    await this.store.setAlarm(Date.now()+60000);
    return {message:remove?'Removal queued for your Google copy. The local personal event is kept.':'Personal event saved. Google Calendar sync queued; no Discord post was created.'};
  }
  async retry(login){
    let count=0;
    for(const [k,saved] of await this.store.list({prefix:`personal:${login.userId}:`}))if(saved.status!=='removed'){
      await this.change(login,k.split(':').at(-1),saved.record,saved.status==='Removing');count++;
    }
    return count;
  }
  async process(job){
    const k=key(job.userId,job.id),saved=await this.store.get(k),c=await this.store.get('google:'+job.userId);
    if(!saved||saved.eventId!==job.eventId||!c||c.subject!==job.subject||c.calendarId!==job.calendarId)return;
    const google=this.core.google,path='calendars/'+encodeURIComponent(c.calendarId)+'/events';
    if(job.remove){
      try{await google.api(job.userId,c,'DELETE',path+'/'+saved.eventId);}
      catch(e){if(![404,410].includes(e.googleStatus))throw e;}
      saved.status='removed';saved.record=null;
    }else{
      const r=saved.record,t=schedule(r,0);
      const payload={summary:r.title,description:r.description+(r.scheduleMode==='Sessions'?'\n\n'+itinerary(r):''),transparency:r.scheduleMode==='Sessions'?'transparent':'opaque',location:[r.world,r.location].filter(Boolean).join(' — '),visibility:'private',start:{dateTime:t.start,timeZone:t.zone},end:{dateTime:t.end,timeZone:t.zone}};
      try{await google.api(job.userId,c,'POST',path,{id:saved.eventId,...payload});}
      catch(e){if(e.googleStatus===409)await google.api(job.userId,c,'PATCH',path+'/'+saved.eventId,payload);else throw e;}
      saved.status='Synced';
    }
    await this.store.put(k,saved);
  }
}
