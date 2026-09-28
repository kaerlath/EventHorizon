import test from 'node:test';
import assert from 'node:assert/strict';
import {RelayCore} from '../src/core.js';
import {googleConfigured} from '../src/google.js';

class Storage {
  data=new Map();alarm=null;
  async get(k){return structuredClone(this.data.get(k));}
  async put(k,v){if(typeof k === 'object'){for(const [key,value] of Object.entries(k))this.data.set(key,structuredClone(value));}else this.data.set(k,structuredClone(v));}
  async delete(k){for(const key of Array.isArray(k)?k:[k])this.data.delete(key);}
  async list({prefix='',limit=Infinity}={}){return new Map([...this.data].filter(([k])=>k.startsWith(prefix)).slice(0,limit).map(([k,v])=>[k,structuredClone(v)]));}
  async getAlarm(){return this.alarm;}async setAlarm(v){this.alarm=v;}
}
const env={PUBLIC_ORIGIN:'https://relay.example',DISCORD_CLIENT_ID:'1',DISCORD_CLIENT_SECRET:'discord-secret',DISCORD_BOT_TOKEN:'bot',GOOGLE_CLIENT_ID:'client.apps.googleusercontent.com',GOOGLE_CLIENT_SECRET:'secret',GOOGLE_TOKEN_KEY:'ab'.repeat(32)};
const login={userId:'42',name:'Tester',accessToken:'discord',expires:Date.now()+3600000};
const scope='https://www.googleapis.com/auth/calendar.app.created';
function fixture(){
  const store=new Storage(),state={requests:[],events:new Map(),calendarCreates:0,failCreate:false,loseEvent:false,reject:false,refreshes:0,revoked:false};
  const fetcher=async(url,init={})=>{
    assert.equal(init.redirect,'manual');state.requests.push({url,init});
    if(url==='https://oauth2.googleapis.com/token'){
      if(init.body.get('grant_type')==='refresh_token')state.refreshes++;
      return Response.json({access_token:'access-private',refresh_token:'refresh-private',expires_in:3600,scope});
    }
    if(url==='https://openidconnect.googleapis.com/v1/userinfo')return Response.json({sub:'123456',email:'test@example.com'});
    if(url==='https://oauth2.googleapis.com/revoke'){state.revoked=true;return Response.json({});}
    if(url==='https://www.googleapis.com/calendar/v3/calendars'){
      state.calendarCreates++;if(state.failCreate)throw new Error('lost create response');return Response.json({id:'dedicated@example.com'});
    }
    if(url.includes('/calendar/v3/calendars/')){
      if(state.reject)return new Response('{}',{status:503});
      const body=JSON.parse(init.body),id=body.id??url.split('/').at(-1);
      if(init.method==='POST'&&state.events.has(id))return new Response('{}',{status:409});
      state.events.set(id,body);
      if(state.loseEvent){state.loseEvent=false;throw new Error('response lost after event saved');}
      return Response.json({id});
    }
    throw new Error('Unexpected URL '+url);
  };
  return {store,state,core:new RelayCore(store,env,()=>{},fetcher)};
}
async function connect(core){
  const link=await core.google.link(login);
  const page=await core.handle(new Request(link.verificationUrl));assert.equal(page.status,200);
  const redirect=await core.handle(new Request(link.verificationUrl,{method:'POST'}));assert.equal(redirect.status,303);
  const url=new URL(redirect.headers.get('Location'));
  assert.equal(url.searchParams.get('scope'),'openid email '+scope);
  assert.equal(url.searchParams.get('code_challenge_method'),'S256');
  assert.match(url.searchParams.get('code_challenge'),/^[a-zA-Z0-9_-]{43}$/);
  const callback=env.PUBLIC_ORIGIN+'/google/callback?state='+url.searchParams.get('state')+'&code=test';
  assert.equal((await core.handle(new Request(callback))).status,200);
  return callback;
}
async function savedEvent(f){
  const id=crypto.randomUUID(),saved={userId:'42',eventId:'777',guildId:'1',record:{id,title:'Moonlit gathering',description:'Bring tea.',organizer:'In-game name',guildId:'1',location:'Garden',world:'Balmung',startLocal:'2035-06-12 18:00',timeZoneId:'Mountain Standard Time',durationMinutes:120,googleCalendarSync:true}};
  await f.store.put('event:'+id,saved);return {id,saved};
}
test('Google is optional and missing encryption configuration disables it',()=>{
  assert.equal(googleConfigured({}),false);assert.equal(googleConfigured({...env,GOOGLE_TOKEN_KEY:'short'}),false);assert.equal(googleConfigured(env),true);
});
test('Google OAuth uses one-time state and stores encrypted tokens bound to Discord user',async()=>{
  const {core,store}=fixture(),callback=await connect(core);
  const c=await store.get('google:42'),raw=JSON.stringify(c);
  assert.ok(!raw.includes('access-private'));assert.ok(!raw.includes('refresh-private'));
  assert.equal((await core.google.open('42',c.credentials)).refresh,'refresh-private');
  await assert.rejects(core.google.open('43',c.credentials));
  assert.notEqual((await core.handle(new Request(callback))).status,200);
  assert.equal((await core.google.status('43')).connected,false);
});
test('cancelled or superseded Google links cannot attach an account',async()=>{
  const {core}=fixture(),link=await core.google.link(login);
  const response=await core.handle(new Request(link.verificationUrl,{method:'POST'}));
  const state=new URL(response.headers.get('Location')).searchParams.get('state');
  await core.google.disconnect('42');
  assert.notEqual((await core.handle(new Request(env.PUBLIC_ORIGIN+'/google/callback?state='+state+'&code=test'))).status,200);
});
test('dedicated calendar is created once and reused after reconnect',async()=>{
  const {core,state}=fixture();await connect(core);await core.google.prepare('42');await core.google.prepare('42');
  await core.google.disconnect('42');await connect(core);await core.google.prepare('42');
  assert.equal(state.calendarCreates,1);assert.equal(state.revoked,true);
});
test('uncertain calendar creation is not repeated',async()=>{
  const {core,state}=fixture();await connect(core);state.failCreate=true;
  await assert.rejects(core.google.prepare('42'));await assert.rejects(core.google.prepare('42'),/could not be confirmed/);
  assert.equal(state.calendarCreates,1);
});
test('background Google sync retains one event after an ambiguous create',async()=>{
  const f=fixture();await connect(f.core);await f.core.google.prepare('42');const {id,saved}=await savedEvent(f);
  await f.core.google.enqueue(id,saved);f.state.loseEvent=true;await f.core.google.processJobs();
  const job=await f.store.get('google-job:'+id);assert.equal(job.attempts,1);job.due=0;await f.store.put('google-job:'+id,job);
  await f.core.google.processJobs();assert.equal(f.state.events.size,1);assert.equal(await f.store.get('google-job:'+id),undefined);
  saved.record.title='Updated gathering';await f.store.put('event:'+id,saved);await f.core.google.enqueue(id,saved);await f.core.google.processJobs();
  assert.equal(f.state.events.size,1);assert.equal([...f.state.events.values()][0].summary,'Updated gathering');
  assert.equal([...f.state.events.values()][0].start.dateTime,'2035-06-13T00:00:00.000Z');
});
test('Google failures stop after bounded retries and show attention status',async()=>{
  const f=fixture();await connect(f.core);await f.core.google.prepare('42');const {id,saved}=await savedEvent(f);
  await f.core.google.enqueue(id,saved);f.state.reject=true;
  for(let i=0;i<6;i++){const job=await f.store.get('google-job:'+id);job.due=0;await f.store.put('google-job:'+id,job);await f.core.google.processJobs();}
  assert.equal((await f.core.google.status('42')).failed,1);
  const requests=f.state.requests.length;await f.core.google.processJobs();assert.equal(requests,f.state.requests.length);
});
test('opting out and disconnect cancel pending jobs but preserve existing calendar',async()=>{
  const f=fixture();await connect(f.core);await f.core.google.prepare('42');const {id,saved}=await savedEvent(f);
  await f.core.google.enqueue(id,saved);saved.record.googleCalendarSync=false;await f.store.put('event:'+id,saved);await f.core.google.processJobs();assert.equal(f.state.events.size,0);
  saved.record.googleCalendarSync=true;await f.core.google.enqueue(id,saved);await f.core.google.disconnect('42');assert.equal((await f.store.list({prefix:'google-job:'})).size,0);
  assert.ok(await f.store.get('google-calendar:42:123456'));
});
test('expired access token refreshes without a running Discord session',async()=>{
  const f=fixture();await connect(f.core);await f.core.google.prepare('42');const {id,saved}=await savedEvent(f);
  const c=await f.store.get('google:42'),credentials=await f.core.google.open('42',c.credentials);credentials.expires=0;c.credentials=await f.core.google.seal('42',credentials);await f.store.put('google:42',c);
  await f.core.google.enqueue(id,saved);await f.core.cleanup();assert.equal(f.state.refreshes,1);assert.equal(f.state.events.size,1);
});
test('Google control endpoints require Discord authentication',async()=>{
  const {core}=fixture();for(const path of ['status','link','prepare','disconnect','retry'])assert.equal((await core.handle(new Request(env.PUBLIC_ORIGIN+'/google/'+path,{method:path==='status'?'GET':'POST'}))).status,401);
});
