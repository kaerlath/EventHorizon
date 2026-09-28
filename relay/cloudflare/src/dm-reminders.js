import {RelayError,validate} from './model.js';
const key=(user,id)=>`personal-dm:${user}:${id}`;

export class DmReminders {
  constructor(core){this.core=core;this.store=core.store;}
  async status(user,id){
    const saved=await this.store.get(key(user,id));
    return {enabled:!!saved?.enabled,message:saved?.deliveries.map(d=>`${d.minutes} min: ${d.status}`).join(' | ')||'Discord reminders are off.'};
  }
  async change(login,id,body){
    const k=key(login.userId,id),old=await this.store.get(k);
    if(body.enabled===false){await this.store.delete(k);return {enabled:false,message:'Unsent Discord reminders cancelled for your account.'};}
    if(body.enabled!==true)throw new RelayError('Choose whether Discord reminders are enabled.');
    const raw=body.event;
    if(raw?.personalOnly!==true||raw.discordEventId)throw new RelayError('Discord reminders here are only for personal events.');
    const leads=body.minutes;
    if(!Array.isArray(leads)||leads.length<1||leads.length>2||leads.some(x=>!Number.isInteger(x)||x<0||x>10080))throw new RelayError('Choose one or two reminder times, up to seven days before.');
    const {item,start}=validate({...raw,guildId:'0',channelId:'',server:'',channel:'',recurrence:'None'},id,Date.now(),true);
    if(!old&&(await this.store.list({prefix:`personal-dm:${login.userId}:`,limit:101})).size>=100)throw new RelayError('You can schedule up to 100 personal events with Discord reminders.');
    const time=Date.parse(start),deliveries=[...new Set(leads)].sort((a,b)=>b-a).map(minutes=>{
      const previous=old?.start===start?old.deliveries.find(d=>d.minutes===minutes):null;
      return previous??{minutes,due:time-minutes*60000,status:time-minutes*60000<Date.now()?'Missed':'Scheduled',nonce:crypto.randomUUID().replaceAll('-','').slice(0,24)};
    });
    await this.store.put(k,{userId:login.userId,id,enabled:true,title:item.title,start,deliveries});
    await this.store.setAlarm(Date.now()+60000);
    return {enabled:true,message:'Discord reminders saved for your linked account. They run without the game. Reminder times already passed are skipped.'};
  }
  async send(user,content,nonce){
    const channel=await this.core.discord('POST','users/@me/channels',{recipient_id:user});
    if(!/^\d+$/.test(channel.id??''))throw new Error('Invalid DM channel');
    return this.core.discord('POST',`channels/${channel.id}/messages`,{content,allowed_mentions:{parse:[]},nonce,enforce_nonce:true});
  }
  async test(login){
    const k='dm-test:'+login.userId,last=await this.store.get(k);
    if(last&&Date.now()-last<60000)throw new RelayError('Wait one minute before sending another test.',429);
    await this.store.put(k,Date.now());
    try{await this.send(login.userId,'Event Horizon: your test reminder arrived. Scheduled personal reminders can reach you while you are out of game.',crypto.randomUUID().replaceAll('-','').slice(0,24));}
    catch{throw new RelayError('DM delivery was not confirmed. Allow direct messages from the bot and check that you share a server with it.',400);}
    return {message:'Test DM sent to your linked Discord account.'};
  }
  async process(now=Date.now()){
    let remaining=false,count=0;
    for(const [k,saved] of await this.store.list({prefix:'personal-dm:'})){
      if(Date.parse(saved.start)+7*86400000<now){await this.store.delete(k);continue;}
      for(const d of saved.deliveries){
        // A crash during a send is ambiguous. Never silently send a duplicate.
        if(d.status==='Sending'){d.status='Delivery uncertain';await this.store.put(k,saved);}
        if(d.status!=='Scheduled')continue;
        if(d.due>now||count>=4){remaining=true;continue;}
        if(now-d.due>10*60000){d.status='Missed';await this.store.put(k,saved);continue;}
        count++;d.status='Sending';await this.store.put(k,saved);
        try{
          await this.send(saved.userId,`Event Horizon personal reminder: ${saved.title}\nStarts <t:${Math.floor(Date.parse(saved.start)/1000)}:F> (<t:${Math.floor(Date.parse(saved.start)/1000)}:R>).`,d.nonce);
          d.status='Sent';
        }catch{d.status='Delivery failed or unconfirmed — check Discord privacy settings';}
        await this.store.put(k,saved);
      }
      // Keep alarms alive until old private reminder content can be pruned.
      remaining=true;
    }
    for(const [k,time] of await this.store.list({prefix:'dm-test:'}))if(now-time>86400000)await this.store.delete(k);
    return remaining;
  }
}
