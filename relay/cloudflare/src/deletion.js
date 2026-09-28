import {RelayError} from './model.js';

export class EventDeletion {
  constructor(core){this.core=core;this.store=core.store;}
  async owned(login,id){
    const saved=await this.store.get('event:'+id);
    if(!saved)throw new RelayError('Published event not found.',404);
    if(saved.userId!==login.userId)throw new RelayError('Only the account that published this event can delete it.',403);
    return saved;
  }
  async status(login,id){
    const saved=await this.owned(login,id),job=await this.store.get('delete-job:'+id);
    let pending=0;
    for(const [,sub] of await this.store.list({prefix:`google-sub:${id}:`}))if(sub.status!=='removed')pending++;
    const discordDone=!!saved.deleted;
    return {status:discordDone?'Deleted':'Deleting',pendingGoogle:pending,
      message:!saved.deleting?'This event has not been deleted.':
        `${discordDone?'Discord event and announcement removed.':'Discord removal pending'+(job?.attempts>=6?' — retry after checking bot permissions':'')+'.'} ${pending} Google copy/copies still awaiting removal.${saved.legacyRemovalUnknown?' An older organizer Google copy could not be identified; reconnect the original Google account and retry.':''} Disconnected Google accounts require reconnection. Independently copied or exported entries cannot be removed.`};
  }
  async begin(login,id){
    const saved=await this.owned(login,id);
    await this.core.authorize(login,saved.guildId);
    if(saved.eventPending||saved.messagePending)throw new RelayError('An earlier Discord create has an uncertain result. Reconcile it before deleting so no unidentified post is left behind.',409);
    saved.deleting=true;
    await this.store.put('event:'+id,saved);
    await this.store.put('delete-job:'+id,{attempts:0,due:Date.now()});
    await this.store.setAlarm(Date.now()+60000);
    // Deletion is durable before external operations. Partial failures can retry.
    await this.processOne(id,saved);
    return this.status(login,id);
  }
  async queueCalendars(id,saved){
    if((saved.googleCopy || saved.record?.googleCalendarSync) && !await this.store.get(`google-sub:${id}:${saved.userId}`)){
      const c=saved.googleCopy??await this.store.get('google:'+saved.userId);
      if(c?.calendarId){
        await this.store.put(`google-sub:${id}:${saved.userId}`,{id,userId:saved.userId,subject:c.subject,calendarId:c.calendarId,eventId:c.eventId??id.replaceAll('-',''),title:saved.record.title,active:false,status:'Removing'});
        saved.legacyRemovalUnknown=false;
      }else saved.legacyRemovalUnknown=true;
    }
    await this.store.delete('google-job:'+id);
    for(const [key,sub] of await this.store.list({prefix:`google-sub:${id}:`}))if(sub.status!=='removed'){
      sub.active=false;sub.status='Removing';await this.store.put(key,sub);
      const existing=await this.store.get(`google-job:sub:${id}:${sub.userId}`);
      if(!existing||existing.action!=='remove'||existing.attempts>=6)await this.core.subscriptions.queue(sub,'remove');
    }
    await this.store.put('event:'+id,saved);
  }
  async removeDiscord(path){
    try{await this.core.discord('DELETE',path);}
    catch(e){if(e.discordStatus!==404)throw e;}
  }
  async processOne(id,saved){
    const job=await this.store.get('delete-job:'+id);if(!job)return;
    try{
      await this.queueCalendars(id,saved);
      if(saved.messageId&&!saved.messageDeleted){await this.removeDiscord(`channels/${saved.channelId}/messages/${saved.messageId}`);saved.messageDeleted=true;await this.store.put('event:'+id,saved);}
      if(saved.eventId&&!saved.eventDeleted){await this.removeDiscord(`guilds/${saved.guildId}/scheduled-events/${saved.eventId}`);saved.eventDeleted=true;await this.store.put('event:'+id,saved);}
      saved.deleted=true;await this.store.put('event:'+id,saved);
      for(const prefix of [`banner:${id}:`,`art:${id}:`])for(const [key] of await this.store.list({prefix}))await this.store.delete(key);
      await this.store.delete('art:'+id);
      await this.store.delete('delete-job:'+id);
    }catch{
      job.attempts++;job.due=Date.now()+60000*2**Math.min(job.attempts,5);await this.store.put('delete-job:'+id,job);
    }
  }
  async process(){
    let count=0,remaining=false;
    for(const [key,job] of await this.store.list({prefix:'delete-job:'})){
      if(job.attempts>=6)continue;
      if(job.due>Date.now()||count>=4){remaining=true;continue;}
      count++;const id=key.slice('delete-job:'.length),saved=await this.store.get('event:'+id);
      if(saved?.deleting)await this.processOne(id,saved);
      if(await this.store.get(key))remaining=true;
    }
    return remaining;
  }
}
