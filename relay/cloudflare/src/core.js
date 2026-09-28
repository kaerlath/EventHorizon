import {session,heartbeat} from './session.js';
import {announcementMessage} from './announcement.js';
import {EventDeletion} from './deletion.js';
import {DmReminders} from './dm-reminders.js';
import {PersonalEvents} from './personal.js';
import {communityEvents} from './community.js';
import {eventArt,saveEventArt} from './event-art.js';
import {botInstallUrl} from './bot-install.js';
import {importFont,requireFonts} from './fonts.js';
import {CalendarSubscriptions} from './subscriptions.js';
import {GoogleCalendar,googleConfigured} from './google.js';
import { RelayError, token, encode, imageData, validate, canCreate, canPost, permissions, b64 } from './model.js';

export const json = (value, status = 200) => Response.json(value, { status, headers: {'Cache-Control':'no-store','Referrer-Policy':'no-referrer'} });
export function configured(env) {
  try {
    const origin=new URL(env.PUBLIC_ORIGIN);
    return origin.protocol==='https:' && origin.pathname==='/' && !origin.username && !origin.password && !origin.search && !origin.hash && /^\d+$/.test(env.DISCORD_CLIENT_ID??'') && !!env.DISCORD_CLIENT_SECRET && !!env.DISCORD_BOT_TOKEN;
  } catch {return false;}
}
const bearer = r => (r.headers.get('Authorization') ?? '').replace(/^Bearer /,'');
const input = async r => {
  const chunks = []; let size = 0;
  if (!r.body) throw new RelayError('A request body is required.');
  const reader = r.body.getReader();
  for (;;) { const {value,done} = await reader.read(); if(done) break; size += value.length;
    if(size > 6 * 1024 * 1024) { await reader.cancel(); throw new RelayError('Request exceeds 6 MB.',413); } chunks.push(value); }
  try { return JSON.parse(await new Blob(chunks).text()); } catch { throw new RelayError('Invalid JSON.'); }
};
class DiscordRejected extends RelayError { constructor(message,status,discordStatus){super(message,status);this.discordStatus=discordStatus;} }

// One Durable Object serializes the beta relay's state transitions. Intents are
// committed before Discord creates, including across process/edge restarts.
export class RelayCore {
  constructor(storage, env, renderer, fetcher = (...args) => globalThis.fetch(...args)) {
    // Keep the Workers native fetch receiver; calling it as a RelayCore method
    // can throw Illegal invocation before an outbound request is even sent.
    this.store = storage; this.env = env; this.render = renderer; this.fetcher = (...args) => fetcher(...args); this.google = new GoogleCalendar(this); this.subscriptions = new CalendarSubscriptions(this); this.personal = new PersonalEvents(this); this.dmReminders = new DmReminders(this); this.deletion = new EventDeletion(this);
  }
  get configured() { return configured(this.env); }
  get origin() { return (this.env.PUBLIC_ORIGIN ?? '').replace(/\/$/,''); }
  async ephemeral(key, value, seconds) {
    await this.store.put(key, {...value, expires:Date.now()+seconds*1000});
    if (!await this.store.getAlarm()) await this.store.setAlarm(Date.now()+60000);
  }
  async live(key) {
    const value = await this.store.get(key);
    if (!value || value.expires <= Date.now()) { if(value) await this.store.delete(key); throw new RelayError('Connection expired. Link Discord again.',401); }
    return value;
  }
  async cleanup() {
    let remaining=false;
    for (const prefix of ['link:','key:','state:','session:','google-link:','google-link-user:','google-state:']) {
      for (const [key,v] of await this.store.list({prefix})) {
        if(v.expires <= Date.now()) await this.store.delete(key); else remaining=true;
      }
    }
    remaining = await this.deletion.process() || remaining;
    remaining = await this.google.processJobs() || remaining;
    remaining = await this.dmReminders.process() || remaining;
    if(remaining)await this.store.setAlarm(Date.now()+60000);
  }
  async handle(request) {
    try { return await this.route(request); }
    catch (e) { return json({error:e instanceof RelayError ? e.message : 'The relay could not complete the request. Existing publication IDs are retained.'},e instanceof RelayError ? e.status : 503); }
  }
  async route(request) {
    const u = new URL(request.url), path = u.pathname, method = request.method;
    if (path === '/health' && method === 'GET') return json({service:'Event Horizon', version:'0.15.2', configured:this.configured, composition:'browser-run',googleCalendar:googleConfigured(this.env),fontLibrary:true});
    if (!this.configured) throw new RelayError('The relay needs its public origin and Discord application credentials.',503);
    if (u.origin !== this.origin) throw new RelayError('Use the configured relay address.',400);
    if(path === '/discord/install' && method === 'GET')return json({url:botInstallUrl(this.env.DISCORD_CLIENT_ID)});
    const googlePublic = await this.google.publicRoute(u,method);
    if(googlePublic)return googlePublic;
    if (path === '/auth/link' && method === 'POST') {
      if ((await this.store.list({prefix:'link:',limit:1000})).size >= 1000) throw new RelayError('Too many pending connections.',429);
      const pollToken = token(), key = token(), code = token().slice(0,8).toUpperCase();
      await this.ephemeral('link:'+pollToken,{key,code},300);
      await this.ephemeral('key:'+key,{pollToken},300);
      return json({pollToken,verificationUrl:`${this.origin}/auth/authorize/${key}`,code});
    }
    const auth = /^\/auth\/authorize\/([a-f0-9]{64})$/.exec(path);
    if (auth && ['GET','POST'].includes(method)) {
      const {pollToken} = await this.live('key:'+auth[1]); const link = await this.live('link:'+pollToken);
      if (link.login) throw new RelayError('This connection is already complete.');
      if (method === 'GET') return new Response(`<!doctype html><meta charset=utf-8><meta name=viewport content="width=device-width"><title>Event Horizon</title><style>body{background:#090e20;color:#e4e9ff;font:20px system-ui;max-width:640px;margin:10vh auto;padding:24px}strong{display:block;font-size:36px;letter-spacing:6px;margin:30px 0}button{padding:16px;font:inherit}</style><h1>Event Horizon</h1><p>Connect the game session showing this reference:</p><strong>${link.code}</strong><p>Continue only if this matches your plugin and you started this connection yourself.</p><form method=post><button>Continue to Discord</button></form>`,{headers:{'Content-Type':'text/html; charset=utf-8','Cache-Control':'no-store','Referrer-Policy':'no-referrer','Content-Security-Policy':"default-src 'none'; style-src 'unsafe-inline'; form-action 'self' https://discord.com; frame-ancestors 'none'"}});
      if (link.state) await this.store.delete('state:'+link.state);
      const state = token(); link.state=state; await this.store.put('link:'+pollToken,link);
      await this.ephemeral('state:'+state,{pollToken},Math.max(1,Math.floor((link.expires-Date.now())/1000)));
      const query = new URLSearchParams({response_type:'code',scope:'identify guilds',client_id:this.env.DISCORD_CLIENT_ID,redirect_uri:this.origin+'/auth/callback',state,prompt:'consent'});
      return new Response(null,{status:303,headers:{Location:'https://discord.com/oauth2/authorize?'+query,'Cache-Control':'no-store','Referrer-Policy':'no-referrer'}});
    }
    if (path === '/auth/callback' && method === 'GET') {
      let stage='pairing lookup';
      try {
      const state = u.searchParams.get('state') ?? ''; const {pollToken} = await this.live('state:'+state);
      await this.store.delete('state:'+state);
      const link = await this.live('link:'+pollToken);
      if (link.login || u.searchParams.has('error') || !u.searchParams.get('code')) throw new RelayError('Discord authorization cancelled or already used. Start again.');
      stage='Discord token exchange';
      const response = await this.fetcher('https://discord.com/api/v10/oauth2/token',{method:'POST',body:new URLSearchParams({client_id:this.env.DISCORD_CLIENT_ID,client_secret:this.env.DISCORD_CLIENT_SECRET,grant_type:'authorization_code',code:u.searchParams.get('code'),redirect_uri:this.origin+'/auth/callback'}),redirect:'manual',signal:AbortSignal.timeout(20000)});
      if(response.status>=300 && response.status<400) throw new RelayError('Discord returned an unexpected redirect during token exchange. No credentials were forwarded. Start a new connection.',502);
      if(!response.ok) throw new RelayError(`Discord rejected the token exchange (HTTP ${response.status}). Check the OAuth client secret and registered callback address, then start a new connection.`,401);
      stage='Discord token response';
      const data = await response.json();
      if(typeof data.access_token!=='string' || !data.access_token || !Number.isFinite(data.expires_in) || data.expires_in<=0)throw new Error('Invalid token response');
      stage='Discord account lookup';
      const user = await this.discord('GET','users/@me',undefined,data.access_token,false);
      if(typeof user.id!=='string'||typeof user.username!=='string')throw new Error('Invalid account response');
      link.login={userId:user.id,name:user.username,accessToken:data.access_token,refreshToken:typeof data.refresh_token==='string'?data.refresh_token:undefined,accessExpires:Date.now()+data.expires_in*1000,expires:Date.now()+Math.min(3600,data.expires_in)*1000};
      stage='saving the connection';
      await this.store.put('link:'+pollToken,link);
      return new Response('Discord linked. Return to Event Horizon and click Finish connection.',{headers:{'Cache-Control':'no-store','Referrer-Policy':'no-referrer'}});
      } catch(e) {
        if(e instanceof RelayError)throw e;
        // Stage names are fixed strings; never return raw exceptions, URLs,
        // response bodies, authorization codes or credentials.
        throw new RelayError(`Discord connection failed during ${stage}. Start a new connection; if it repeats, report this stage to the relay operator.`,503);
      }
    }
    if(path === '/auth/poll' && method === 'POST') {
      const body = await input(request), key = 'link:'+(body.pollToken ?? body.PollToken ?? ''); const link = await this.live(key);
      if(!link.login) return json({status:'pending'});
      const sessionToken = token(); await this.store.put('session:'+sessionToken,link.login);
      await this.store.delete([key,'key:'+link.key]);
      return json({status:'connected',token:sessionToken,userName:link.login.name});
    }
    if(path === '/auth/logout' && method === 'POST') { await this.store.delete('session:'+bearer(request)); return json({ok:true}); }
    const sessionKey = 'session:'+bearer(request);
    const login = await session(this,sessionKey);
    if(path === '/auth/heartbeat' && method === 'POST')return json(await heartbeat(this,sessionKey,login,(await input(request)).enabled));
    const art=/^\/events\/([a-f0-9-]{36})\/image$/i.exec(path);
    if(art && method==='GET')return json(await eventArt(this,login,art[1].toLowerCase()));
    if(path === '/fonts' && method === 'POST')return json(await importFont(this.store,login.userId,await input(request)));
    if(path === '/fonts' && method === 'GET')return json([...await this.store.list({prefix:'font-user:'+login.userId+':'})].map(([,v])=>v));
    if(path==='/discord/reminders/test' && method==='POST')return json(await this.dmReminders.test(login));
    const dm=/^\/events\/([a-f0-9-]{36})\/personal-reminders$/i.exec(path);
    if(dm && method==='GET')return json(await this.dmReminders.status(login.userId,dm[1].toLowerCase()));
    if(dm && method==='PUT')return json(await this.dmReminders.change(login,dm[1].toLowerCase(),await input(request)));
    const deletion=/^\/events\/([a-f0-9-]{36})(\/deletion)?$/i.exec(path);
    if(deletion && method==='DELETE' && !deletion[2])return json(await this.deletion.begin(login,deletion[1].toLowerCase()));
    if(deletion && method==='GET' && deletion[2])return json(await this.deletion.status(login,deletion[1].toLowerCase()));
    const personal = /^\/google\/personal\/([a-f0-9-]{36})$/i.exec(path);
    if(personal && ['PUT','DELETE'].includes(method))return json(await this.personal.change(login,personal[1].toLowerCase(),method==='PUT'?await input(request):null,method==='DELETE'));
    if(path === '/google/status' && method === 'GET')return json(await this.google.status(login.userId));
    if(path === '/google/link' && method === 'POST')return json(await this.google.link(login));
    if(path === '/google/prepare' && method === 'POST')return json(await this.google.prepare(login.userId));
    if(path === '/google/disconnect' && method === 'POST')return json(await this.google.disconnect(login.userId));
    if(path === '/google/retry' && method === 'POST') {
      let count=await this.subscriptions.retry(login); count+=await this.personal.retry(login);
      for(const [key,saved] of await this.store.list({prefix:'event:'}))if(!saved.deleting && saved.userId===login.userId && saved.record?.googleCalendarSync){
        await this.authorize(login,saved.guildId);await this.google.enqueue(key.slice(6),saved);count++;
      }
      return json({count});
    }
    if(path === '/events/community' && method === 'GET')return json(await communityEvents(this,login));
    if(path === '/google/subscriptions' && method === 'GET')return json(await this.subscriptions.list(login.userId));
    const subscription = /^\/google\/subscriptions\/([a-f0-9-]{36})$/i.exec(path);
    if(subscription && ['POST','DELETE'].includes(method))return json(await this.subscriptions.change(login,subscription[1].toLowerCase(),method==='DELETE'));
    if(path === '/discord/guilds' && method === 'GET') return json(await this.guilds(login));
    const channel = /^\/discord\/guilds\/(\d+)\/channels$/.exec(path);
    if(channel && method === 'GET') return json(await this.channels(login,channel[1]));
    if(path === '/events' && method === 'GET') {
      const allowed = new Set((await this.guilds(login)).map(g=>g.id)), result=[];
      for(const [,saved] of await this.store.list({prefix:'event:'})) if(!saved.deleting && saved.userId===login.userId && allowed.has(saved.guildId) && saved.eventId && saved.record)
        result.push({...saved.record,discordEventId:saved.eventId,discordMessageId:saved.messageId,relayOrigin:this.origin+'/',status:'Published'});
      return json(result);
    }
    if(path === '/preview' && method === 'POST') {
      const body = await input(request), raw = body.event ?? body.Event;
      const rawId = raw?.id ?? raw?.Id;
      if(typeof rawId !== 'string')throw new RelayError('An event ID is required.');
      const id = rawId.toLowerCase();
      const {item,start,end,zone} = validate(raw,id,0,true);
      await requireFonts(this.store,login.userId,item.announcementStyle);
      let banner = body.bannerDataUrl ?? body.BannerDataUrl;
      const saved = await this.store.get('event:'+id);
      if(saved && saved.userId !== login.userId)throw new RelayError('This event belongs to another account.',403);
      if(saved)await this.authorize(login,saved.guildId);
      if(banner == null)banner = saved ? await this.loadBanner(id,saved.bannerChunks) : '';
      imageData(banner);
      const image = await this.render(item,login.name,banner,{start,end,zone});
      return json({imageBase64:b64(image)});
    }
    const publish = /^\/events\/([a-f0-9-]{36})\/publish$/i.exec(path);
    if(publish && method === 'PUT') return json(await this.publish(login,publish[1].toLowerCase(),await input(request)));
    throw new RelayError('Unknown endpoint.',404);
  }
  async discord(method,path,body,credential=this.env.DISCORD_BOT_TOKEN,bot=true,upload=null) {
    const headers = {Authorization:`${bot?'Bot':'Bearer'} ${credential}`}; let data;
    if(upload) {
      data=new FormData(); data.set('payload_json',JSON.stringify(body)); data.set('files[0]',new Blob([upload],{type:'image/png'}),'event-card.png');
    } else if(body) { headers['Content-Type']='application/json'; data=JSON.stringify(body); }
    const response=await this.fetcher('https://discord.com/api/v10/'+path,{method,headers,body:data,redirect:'manual',signal:AbortSignal.timeout(20000)});
    // 5xx is ambiguous for a create; preserve the pending intent.
    // Workers does not implement redirect:error. Manual plus explicit rejection
    // provides the same no-forwarding protection on its supported runtime.
    if(response.status>=300 && response.status<400)throw new Error('Unexpected Discord redirect');
    if(!response.ok) {
      if(response.status>=500) throw new Error('Discord response uncertain');
      throw new DiscordRejected('Discord rejected the request. Check permissions and retry.',response.status===429?429:403,response.status);
    }
    return response.status===204 ? {} : response.json();
  }
  async allGuilds(credential,bot) {
    let after='0', result=[];
    for(let i=0;i<20;i++) { const page=await this.discord('GET',`users/@me/guilds?limit=200&after=${after}`,undefined,credential,bot); result.push(...page);
      if(page.length<200)return result; const next=page.reduce((n,g)=>BigInt(g.id)>BigInt(n)?g.id:n,after); if(next===after)break; after=next; }
    throw new RelayError('Could not finish loading servers.',503);
  }
  async guilds(login) {
    const user=await this.allGuilds(login.accessToken,false), bot=await this.allGuilds(this.env.DISCORD_BOT_TOKEN,true), installed=new Set(bot.map(g=>g.id));
    return user.filter(g=>installed.has(g.id)&&(g.owner||canCreate(g.permissions))).map(g=>({id:g.id,name:g.name}));
  }
  async authorize(login,guild) { if(!(await this.guilds(login)).some(g=>g.id===guild))throw new RelayError('You need event permissions and the Event Horizon bot in this server.',403); }
  async channels(login,guild) {
    await this.authorize(login,guild);
    const channels=await this.discord('GET',`guilds/${guild}/channels`), roles=await this.discord('GET',`guilds/${guild}/roles`);
    const member=await this.discord('GET',`guilds/${guild}/members/${login.userId}`), bot=await this.discord('GET','users/@me');
    const botMember=await this.discord('GET',`guilds/${guild}/members/${bot.id}`), info=await this.discord('GET',`guilds/${guild}`);
    return channels.filter(c=>[0,5].includes(c.type) && (info.owner_id===login.userId||canPost(permissions(guild,login.userId,member,roles,c))) && canPost(permissions(guild,bot.id,botMember,roles,c))).map(c=>({id:c.id,name:c.name}));
  }
  async loadBanner(id,count) { let result=''; for(let i=0;i<(count??0);i++) { const chunk=await this.store.get(`banner:${id}:${i}`); if(typeof chunk!=='string')throw new Error('Banner data missing'); result+=chunk; } return result; }
  async saveBanner(id,value,oldCount) {
    const count=Math.ceil(value.length/90000);
    // Caller commits chunk count and record in the same storage transaction.
    for(let i=0;i<count;i++) await this.store.put(`banner:${id}:${i}`,value.slice(i*90000,(i+1)*90000));
    for(let i=count;i<(oldCount??0);i++)await this.store.delete(`banner:${id}:${i}`);
    return count;
  }
  async publish(login,id,body) {
    if((body.event??body.Event)?.personalOnly || (body.event??body.Event)?.PersonalOnly)throw new RelayError("Personal events cannot be published to Discord.");
    const {item,start,end,zone}=validate(body.event??body.Event,id);
    await requireFonts(this.store,login.userId,item.announcementStyle);
    const banner=body.bannerDataUrl??body.BannerDataUrl; imageData(banner);
    await this.authorize(login,item.guildId);
    if(item.channelId && !(await this.channels(login,item.guildId)).some(c=>c.id===item.channelId))throw new RelayError('You and the bot need view, send, embed and attachment permissions.',403);
    const key='event:'+id; let saved=await this.store.get(key);
    if(saved && saved.userId!==login.userId)throw new RelayError('This event belongs to another account.',403);
    if(saved?.deleting)throw new RelayError('This event was deleted or is being deleted. Create a new event instead.',409);
    if(saved && (saved.guildId!==item.guildId||saved.channelId!==item.channelId))throw new RelayError('Create a copy to change the destination.');
    saved??={userId:login.userId,guildId:item.guildId,channelId:item.channelId,eventId:'',messageId:'',bannerChunks:0};
    if(saved.eventPending)throw new RelayError('An earlier event create has an uncertain result. Operator reconciliation is required; no duplicate was sent.',409);
    const source=banner??await this.loadBanner(id,saved.bannerChunks);
    const card=item.channelId && !saved.messagePending ? await this.render(item,login.name,source,{start,end,zone}) : null;
    const payload={name:item.title,description:item.description,scheduled_start_time:start,scheduled_end_time:end,privacy_level:2,entity_type:3,channel_id:null,entity_metadata:{location:item.world?`${item.world} — ${item.location}`:item.location}};
    if(banner!==undefined && banner!==null)payload.image=banner||null;
    if(!saved.eventId) {
      saved.eventPending=true; await this.store.put(key,saved);
      try { const result=await this.discord('POST',`guilds/${item.guildId}/scheduled-events`,payload); saved.eventId=result.id; saved.eventPending=false; await this.store.put(key,saved); }
      catch(e) { if(e instanceof DiscordRejected) {saved.eventPending=false;await this.store.put(key,saved);} throw e; }
    } else await this.discord('PATCH',`guilds/${item.guildId}/scheduled-events/${saved.eventId}`,payload);
    saved.record=item;
    await this.store.transaction(async tx=>{
      const previous=this.store; this.store=tx;
      try { saved.bannerChunks=await this.saveBanner(id,source,saved.bannerChunks); await tx.put(key,saved); } finally {this.store=previous;}
    });
    let warning=null;
    if(item.channelId) {
      if(saved.messagePending) warning='Event saved. An earlier announcement has an uncertain result; operator reconciliation is required.';
      else {
        const url=`https://discord.com/events/${item.guildId}/${saved.eventId}`;
        const message=announcementMessage(item,login.name,{start,end,zone},url,!!saved.messageId);
        try {
          if(!saved.messageId) {saved.messagePending=true;await this.store.put(key,saved);}
          const result=await this.discord(saved.messageId?'PATCH':'POST',`channels/${item.channelId}/messages${saved.messageId?'/'+saved.messageId:''}`,message,undefined,true,card);
          saved.messageId=result.id;saved.messagePending=false;await this.store.put(key,saved);
          try { await saveEventArt(this.store,id,card,true); }
          catch { warning='Discord saved the announcement, but its in-game image could not be cached. Save & Sync again to refresh it.'; }
        } catch(e) {
          if(e instanceof DiscordRejected) {saved.messagePending=false;await this.store.put(key,saved);warning='Event saved, but Discord rejected the announcement. Check permissions and retry.';}
          else warning='Event saved. Announcement delivery could not be confirmed; operator reconciliation may be required.';
        }
      }
    }
    try { const googleWarning=await this.google.enqueue(id,saved); if(googleWarning)warning=[warning,googleWarning].filter(Boolean).join(' '); }
    catch { warning=[warning,'Discord saved, but Google sync could not be queued. Save & Sync again to retry.'].filter(Boolean).join(' '); }
    try { await this.subscriptions.fanout(id); }
    catch { warning=[warning,'Discord saved, but some personal calendar updates could not be queued. Save & Sync again to retry.'].filter(Boolean).join(' '); }
    return {eventId:saved.eventId,messageId:saved.messageId,warning};
  }
}
