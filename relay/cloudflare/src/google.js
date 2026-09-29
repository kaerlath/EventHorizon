import {itinerary} from './model.js';
import {RelayError,token,b64,encode,schedule} from './model.js';

const SCOPE='https://www.googleapis.com/auth/calendar.app.created';
const unbase64=s=>Uint8Array.from(atob(s),c=>c.charCodeAt(0));
const base64url=b=>b64(b).replaceAll('+','-').replaceAll('/','_').replace(/=+$/,'');
export const googleConfigured=env=>!!env.GOOGLE_CLIENT_ID && !!env.GOOGLE_CLIENT_SECRET && /^[a-f0-9]{64}$/i.test(env.GOOGLE_TOKEN_KEY??'');
class GoogleError extends RelayError {
  constructor(status){super(status===401?'Google authorization expired. Reconnect Google Calendar.':`Google Calendar could not complete this operation (HTTP ${status}).`,502);this.googleStatus=status;}
}

export class GoogleCalendar {
  constructor(core){this.core=core;}
  get store(){return this.core.store;}
  get env(){return this.core.env;}
  get configured(){return googleConfigured(this.env);}
  requireConfigured(){if(!this.configured)throw new RelayError('Google Calendar is not configured on this relay yet.',503);}
  async request(url,init){
    const response=await this.core.fetcher(url,{...init,redirect:'manual',signal:AbortSignal.timeout(20000)});
    if(!response.ok)throw new GoogleError(response.status);
    return response.status===204?{}:response.json();
  }
  async cryptKey(){return crypto.subtle.importKey('raw',Uint8Array.from(this.env.GOOGLE_TOKEN_KEY.match(/../g),s=>parseInt(s,16)), 'AES-GCM',false,['encrypt','decrypt']);}
  async seal(userId,value){
    const iv=crypto.getRandomValues(new Uint8Array(12));
    const data=await crypto.subtle.encrypt({name:'AES-GCM',iv,additionalData:new TextEncoder().encode(userId)},await this.cryptKey(),new TextEncoder().encode(JSON.stringify(value)));
    return {iv:b64(iv),data:b64(new Uint8Array(data))};
  }
  async open(userId,sealed){return JSON.parse(new TextDecoder().decode(await crypto.subtle.decrypt({name:'AES-GCM',iv:unbase64(sealed.iv),additionalData:new TextEncoder().encode(userId)},await this.cryptKey(),unbase64(sealed.data))));}
  async tokenRequest(params){return this.request('https://oauth2.googleapis.com/token',{method:'POST',body:new URLSearchParams({client_id:this.env.GOOGLE_CLIENT_ID,client_secret:this.env.GOOGLE_CLIENT_SECRET,...params})});}
  async access(userId,connection){
    const credentials=await this.open(userId,connection.credentials);
    if(credentials.expires>Date.now()+60000)return credentials.access;
    const result=await this.tokenRequest({grant_type:'refresh_token',refresh_token:credentials.refresh});
    if(typeof result.access_token!=='string'||!Number.isFinite(result.expires_in))throw new RelayError('Google returned an invalid token response.',502);
    credentials.access=result.access_token;credentials.expires=Date.now()+result.expires_in*1000;
    if(result.refresh_token)credentials.refresh=result.refresh_token;
    connection.credentials=await this.seal(userId,credentials);await this.store.put('google:'+userId,connection);
    return credentials.access;
  }
  async api(userId,connection,method,path,body){
    return this.request('https://www.googleapis.com/calendar/v3/'+path,{method,headers:{Authorization:'Bearer '+await this.access(userId,connection),'Content-Type':'application/json'},body:body===undefined?undefined:JSON.stringify(body)});
  }
  async status(userId){
    const c=await this.store.get('google:'+userId);let pending=0,failed=0;
    for(const [,job] of await this.store.list({prefix:'google-job:'}))if(job.userId===userId){if(job.attempts>=6)failed++;else pending++;}
    return {configured:this.configured,connected:!!c,email:c?.email??'',calendarReady:!!c?.calendarId,pending,failed};
  }
  async link(login){
    this.requireConfigured();
    if(await this.store.get('google:'+login.userId))throw new RelayError('Disconnect Google before connecting another account.');
    const old=await this.store.get('google-link-user:'+login.userId);
    if(old)await this.store.delete('google-link:'+old.key);
    const key=token(),code=key.slice(0,8).toUpperCase();
    await this.core.ephemeral('google-link:'+key,{userId:login.userId,code},300);
    await this.core.ephemeral('google-link-user:'+login.userId,{key},300);
    return {code,verificationUrl:this.core.origin+'/google/authorize/'+key};
  }
  async publicRoute(u,method){
    const match=/^\/google\/authorize\/([a-f0-9]{64})$/.exec(u.pathname);
    if(match&&['GET','POST'].includes(method)){
      this.requireConfigured();const key=match[1],link=await this.core.live('google-link:'+key);
      if(method==='GET')return new Response(`<!doctype html><meta charset=utf-8><title>Event Horizon Google Calendar</title><style>body{font:20px system-ui;background:#0b1427;color:#edf1ff;max-width:650px;margin:10vh auto;padding:24px}button{font:inherit;padding:16px}strong{font-size:32px}</style><h1>Connect Google Calendar</h1><p>Check that this reference matches your in-game window:</p><strong>${encode(link.code)}</strong><p>This creates a dedicated Event Horizon calendar in the Google account you choose. Only events you opt into sync are copied. Continue only if you started this connection yourself.</p><form method=post><button>Continue to Google</button></form>`,{headers:{'Content-Type':'text/html;charset=utf-8','Cache-Control':'no-store','Referrer-Policy':'no-referrer','Content-Security-Policy':"default-src 'none'; style-src 'unsafe-inline'; form-action 'self' https://accounts.google.com; frame-ancestors 'none'"}});
      const verifier=token(),challenge=base64url(new Uint8Array(await crypto.subtle.digest('SHA-256',new TextEncoder().encode(verifier))));
      const state=token();await this.store.delete('google-link:'+key);
      await this.core.ephemeral('google-state:'+state,{userId:link.userId,verifier,key},300);
      const q=new URLSearchParams({client_id:this.env.GOOGLE_CLIENT_ID,redirect_uri:this.core.origin+'/google/callback',response_type:'code',scope:'openid email '+SCOPE,access_type:'offline',prompt:'consent select_account',state,code_challenge:challenge,code_challenge_method:'S256'});
      return new Response(null,{status:303,headers:{Location:'https://accounts.google.com/o/oauth2/v2/auth?'+q,'Cache-Control':'no-store','Referrer-Policy':'no-referrer'}});
    }
    if(u.pathname==='/google/callback'&&method==='GET'){
      this.requireConfigured();const state=u.searchParams.get('state')??'',link=await this.core.live('google-state:'+state);
      await this.store.delete('google-state:'+state);
      const latest=await this.store.get('google-link-user:'+link.userId);
      if(latest?.key!==link.key)throw new RelayError('This Google connection was replaced or cancelled. Start again.');
      await this.store.delete('google-link-user:'+link.userId);
      if(u.searchParams.has('error')||!u.searchParams.get('code'))throw new RelayError('Google connection cancelled.');
      if(await this.store.get('google:'+link.userId))throw new RelayError('Google is already connected.');
      const result=await this.tokenRequest({grant_type:'authorization_code',code:u.searchParams.get('code'),code_verifier:link.verifier,redirect_uri:this.core.origin+'/google/callback'});
      if(!result.scope?.split(' ').includes(SCOPE)||!result.refresh_token||!result.access_token||!Number.isFinite(result.expires_in))throw new RelayError('Approve calendar access and reconnect Google.',400);
      const user=await this.request('https://openidconnect.googleapis.com/v1/userinfo',{headers:{Authorization:'Bearer '+result.access_token}});
      if(typeof user.sub!=='string'||!/^[0-9]+$/.test(user.sub))throw new RelayError('Google account could not be identified.',502);
      const saved=await this.store.get('google-calendar:'+link.userId+':'+user.sub);
      await this.store.put('google:'+link.userId,{subject:user.sub,email:typeof user.email==='string'?user.email:'Google account',calendarId:saved?.calendarId??'',credentials:await this.seal(link.userId,{access:result.access_token,refresh:result.refresh_token,expires:Date.now()+result.expires_in*1000})});
      return new Response('Google connected. Return to Event Horizon, refresh Google status, and choose Prepare calendar.',{headers:{'Cache-Control':'no-store','Referrer-Policy':'no-referrer'}});
    }
    return null;
  }
  async prepare(userId){
    this.requireConfigured();const c=await this.store.get('google:'+userId);
    if(!c)throw new RelayError('Connect Google Calendar first.');
    if(c.calendarId)return this.status(userId);
    const key='google-calendar:'+userId+':'+c.subject,known=await this.store.get(key);
    if(known?.calendarId){c.calendarId=known.calendarId;await this.store.put('google:'+userId,c);return this.status(userId);}
    if(known?.pending)throw new RelayError('A calendar creation could not be confirmed. Check Google Calendar with the relay operator before trying again; no duplicate was created.',409);
    await this.store.put(key,{pending:true});
    try{
      const calendar=await this.api(userId,c,'POST','calendars',{summary:'Event Horizon',description:'Events synchronized from Event Horizon. Manage event details in the plugin.',timeZone:'UTC'});
      if(typeof calendar.id!=='string'||!calendar.id)throw new Error('Missing calendar ID');
      c.calendarId=calendar.id;await this.store.put(key,{calendarId:calendar.id,pending:false});await this.store.put('google:'+userId,c);
    }catch(e){if(e instanceof GoogleError&&e.googleStatus>=400&&e.googleStatus<500)await this.store.delete(key);throw e;}
    return this.status(userId);
  }
  async disconnect(userId){
    const c=await this.store.get('google:'+userId);let revoked=false;
    if(c&&this.configured){try{
      const credentials=await this.open(userId,c.credentials);
      const response=await this.core.fetcher('https://oauth2.googleapis.com/revoke',{method:'POST',body:new URLSearchParams({token:credentials.refresh}),redirect:'manual',signal:AbortSignal.timeout(20000)});
      revoked=response.ok;
    }catch{}}
    await this.core.subscriptions.disconnect(userId);
    await this.store.delete(['google:'+userId,'google-link-user:'+userId]);
    for(const [key,value] of await this.store.list({prefix:'google-job:'}))if(value.userId===userId && value.action!=='remove')await this.store.delete(key);
    return {warning:c&&!revoked?'Disconnected locally. Google revocation was not confirmed; remove Event Horizon access in your Google account settings.':null};
  }
  async enqueue(id,saved){
    const key='google-job:'+id;
    if(await this.store.get(`google-sub:${id}:${saved.userId}`)){await this.store.delete(key);return null;}
    if(saved.deleting||!saved.record.googleCalendarSync){await this.store.delete(key);return null;}
    const c=await this.store.get('google:'+saved.userId);
    if(!this.configured||!c?.calendarId)return 'Discord saved. Connect Google and prepare your calendar, then Save & Sync again.';
    saved.googleCopy={subject:c.subject,calendarId:c.calendarId,eventId:id.replaceAll('-','')};await this.store.put('event:'+id,saved);
    await this.store.put(key,{userId:saved.userId,subject:c.subject,calendarId:c.calendarId,attempts:0,due:Date.now()});
    await this.store.setAlarm(Date.now()+60000);
    return 'Discord saved. Google Calendar sync is queued; check Google status shortly.';
  }
  eventPayload(saved){
    const item=saved.record,times=schedule(item,0);
    return {transparency:item.scheduleMode==='Sessions'?'transparent':'opaque',summary:item.title,description:`${encode(item.description)}${item.scheduleMode==='Sessions'?'\n\n'+encode(itinerary(item)):''}\n\nOrganizer: ${encode(item.organizer)}\nhttps://discord.com/events/${item.guildId}/${saved.eventId}`,location:[item.world,item.location].filter(Boolean).join(' — '),start:{dateTime:times.start,timeZone:times.zone},end:{dateTime:times.end,timeZone:times.zone}};
  }
  async processJobs(){
    let processed=0,remaining=false;
    for(const [key,job] of await this.store.list({prefix:'google-job:'})){
      if(job.attempts>=6)continue;
      if(job.due>Date.now()||processed>=4){remaining=true;continue;}
      processed++;
      try{
        if(job.kind==='personal'){await this.core.personal.process(job);await this.store.delete(key);continue;}
        if(job.kind==='subscription'){
          await this.core.subscriptions.process(job);await this.store.delete(key);continue;
        }
        const id=key.slice('google-job:'.length),saved=await this.store.get('event:'+id),c=await this.store.get('google:'+job.userId);
        if(saved?.deleting||!saved?.record?.googleCalendarSync||saved.userId!==job.userId||!c||c.subject!==job.subject||c.calendarId!==job.calendarId){await this.store.delete(key);continue;}
        this.requireConfigured();
        const item=saved.record,times=schedule(item,0),eventId=id.replaceAll('-','');
        const payload=this.eventPayload(saved);
        const path='calendars/'+encodeURIComponent(c.calendarId)+'/events';
        try{await this.api(job.userId,c,'POST',path,{id:eventId,...payload});}
        catch(e){if(e instanceof GoogleError&&e.googleStatus===409)await this.api(job.userId,c,'PATCH',path+'/'+eventId,payload);else throw e;}
        await this.store.delete(key);
      }catch{
        job.attempts++;job.due=Date.now()+60000*2**Math.min(job.attempts,5);await this.store.put(key,job);
        if(job.attempts<6)remaining=true;
      }
    }
    return remaining;
  }
}
