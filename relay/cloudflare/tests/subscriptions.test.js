import test from 'node:test';
import assert from 'node:assert/strict';
import {RelayCore} from '../src/core.js';

class Storage {
  data=new Map();alarm=null;
  async get(k){return structuredClone(this.data.get(k));}
  async put(k,v){if(typeof k === 'object'){for(const [key,value] of Object.entries(k))this.data.set(key,structuredClone(value));}else this.data.set(k,structuredClone(v));}
  async delete(k){for(const key of Array.isArray(k)?k:[k])this.data.delete(key);}
  async list({prefix='',limit=Infinity}={}){return new Map([...this.data].filter(([k])=>k.startsWith(prefix)).slice(0,limit).map(([k,v])=>[k,structuredClone(v)]));}
  async getAlarm(){return this.alarm;}async setAlarm(v){this.alarm=v;}
  async transaction(fn){return fn(this);}
}
const env={PUBLIC_ORIGIN:'https://relay.example',DISCORD_CLIENT_ID:'1',DISCORD_CLIENT_SECRET:'secret',DISCORD_BOT_TOKEN:'bot',GOOGLE_CLIENT_ID:'google',GOOGLE_CLIENT_SECRET:'secret',GOOGLE_TOKEN_KEY:'aa'.repeat(32)};
const viewer={userId:'43',name:'Viewer',accessToken:'viewer',expires:Date.now()+3600000};
const owner={...viewer,userId:'42',accessToken:'owner'};
async function fixture(){
  const store=new Storage(),state={deny:false,member:true,discordWrites:0,google:new Map(),googleCalls:[],loseCreate:false,failDelete:false,failMembership:false};
  const fetcher=async(url,init={})=>{
    assert.equal(init.redirect,'manual');
    const p=new URL(url).pathname,method=init.method??'GET';
    if(url.startsWith('https://www.googleapis.com/calendar/v3/')){
      state.googleCalls.push({url,method});const base=p.split('/events')[0],body=init.body?JSON.parse(init.body):null,id=body?.id??p.split('/').at(-1),key=base+'/'+id;
      if(method==='DELETE'){
        if(state.failDelete)return new Response('{}',{status:503});
        const found=state.google.delete(key);return new Response(null,{status:found?204:404});
      }
      if(method==='POST'&&state.google.has(key))return new Response('{}',{status:409});
      state.google.set(key,body);
      if(state.loseCreate){state.loseCreate=false;throw new Error('lost response');}
      return Response.json({id});
    }
    if(url==='https://oauth2.googleapis.com/revoke')return Response.json({});
    if(p.endsWith('/users/@me/guilds'))return Response.json([{id:'1',permissions:init.headers.Authorization==='Bearer owner'?'8':'1024'}]);
    if(p.includes('/members/')){
      if(state.failMembership)return new Response('{}',{status:503});
      if(!state.member&&p.endsWith('/43'))return new Response('{}',{status:404});
      return Response.json({roles:[]});
    }
    if(p.endsWith('/guilds/1'))return Response.json({owner_id:'42'});
    if(p.endsWith('/guilds/1/roles'))return Response.json([{id:'1',permissions:'1024'}]);
    if(p.endsWith('/guilds/1/channels'))return Response.json([{id:'2',type:0,permission_overwrites:state.deny?[{id:'43',type:1,deny:'1024',allow:'0'}]:[]}]);
    if(p.includes('/scheduled-events/')){state.discordWrites++;return Response.json({id:'777'});}
    throw new Error('Unexpected '+url);
  };
  const core=new RelayCore(store,env,()=>{},fetcher),id=crypto.randomUUID();
  const saved={userId:'42',guildId:'1',channelId:'2',eventId:'777',messageId:'888',record:{id,title:'Community night',description:'Original description',startLocal:'2035-06-12 18:00',timeZoneId:'UTC',durationMinutes:120,location:'Garden',world:'Balmung',guildId:'1',channelId:'2',googleCalendarSync:false,organizer:'Host'}};
  await store.put('event:'+id,saved);
  for(const userId of ['42','43','44'])await store.put('google:'+userId,{subject:'google'+userId,email:'test@example.com',calendarId:'calendar'+userId,credentials:await core.google.seal(userId,{access:'access',refresh:'refresh',expires:Date.now()+3600000})});
  await store.put('session:viewer',viewer);await store.put('session:other',{...viewer,userId:'44'});
  return {core,store,state,id,saved,fetcher};
}
const request=(path,method='GET',session='viewer')=>new Request(env.PUBLIC_ORIGIN+path,{method,headers:{Authorization:'Bearer '+session}});

test('ordinary members discover read-only community events without organizer permissions',async()=>{
  const f=await fixture();const response=await f.core.handle(request('/events/community'));
  assert.equal(response.status,200);const rows=await response.json();assert.equal(rows.length,1);assert.equal(rows[0].readOnly,true);assert.equal(rows[0].googleCalendarSync,false);
  assert.equal((await f.core.guilds(viewer)).length,0);
  f.state.deny=true;assert.deepEqual(await(await f.core.handle(request('/events/community'))).json(),[]);
});
test('hidden-channel and departed members cannot subscribe',async()=>{
  const f=await fixture();f.state.deny=true;
  assert.equal((await f.core.handle(request('/google/subscriptions/'+f.id,'POST'))).status,403);
  f.state.deny=false;f.state.member=false;
  assert.equal((await f.core.handle(request('/google/subscriptions/'+f.id,'POST'))).status,403);
  assert.equal((await f.store.list({prefix:'google-sub:'})).size,0);
});
test('personal subscription leaves original and organizer calendar preferences unchanged',async()=>{
  const f=await fixture();const response=await f.core.handle(request('/google/subscriptions/'+f.id,'POST'));
  assert.equal(response.status,200);await f.core.google.processJobs();
  assert.equal(f.state.google.size,1);assert.equal(f.state.discordWrites,0);assert.deepEqual(await f.store.get('event:'+f.id),f.saved);
  assert.equal((await f.core.subscriptions.list('43'))[0].status,'Synced');
  assert.deepEqual(await f.core.subscriptions.list('44'),[]);
});
test('organizer edits update all subscribed copies without duplicate events',async()=>{
  const f=await fixture();await f.core.subscriptions.change(viewer,f.id);await f.core.subscriptions.change({...viewer,userId:'44'},f.id);await f.core.google.processJobs();
  assert.equal(f.state.google.size,2);
  f.saved.record.title='Updated community night';await f.store.put('event:'+f.id,f.saved);await f.core.subscriptions.fanout(f.id);await f.core.google.processJobs();
  assert.equal(f.state.google.size,2);for(const event of f.state.google.values())assert.equal(event.summary,'Updated community night');
});
test('remove affects only the caller and allows a fresh later subscription',async()=>{
  const f=await fixture();await f.core.subscriptions.change(viewer,f.id);await f.core.subscriptions.change({...viewer,userId:'44'},f.id);await f.core.google.processJobs();
  const prior=(await f.store.get(`google-sub:${f.id}:43`)).eventId;
  await f.core.subscriptions.change(viewer,f.id,true);await f.core.google.processJobs();
  assert.equal(f.state.google.size,1);assert.ok([...f.state.google.keys()][0].includes('calendar44'));assert.equal(f.state.discordWrites,0);
  await f.core.subscriptions.change(viewer,f.id);await f.core.google.processJobs();assert.equal(f.state.google.size,2);
  assert.notEqual((await f.store.get(`google-sub:${f.id}:43`)).eventId,prior);
});
test('pending removal survives restart, blocks re-add and is not overwritten by fanout',async()=>{
  const f=await fixture();await f.core.subscriptions.change(viewer,f.id);await f.core.google.processJobs();
  await f.core.subscriptions.change(viewer,f.id,true);f.state.failDelete=true;await f.core.google.processJobs();
  await assert.rejects(f.core.subscriptions.change(viewer,f.id),/removal/);await f.core.subscriptions.fanout(f.id);
  const key=`google-job:sub:${f.id}:43`,job=await f.store.get(key);assert.equal(job.action,'remove');job.due=0;await f.store.put(key,job);
  f.state.failDelete=false;const restarted=new RelayCore(f.store,env,()=>{},f.fetcher);await restarted.google.processJobs();
  assert.equal(f.state.google.size,0);assert.equal((await f.store.get(`google-sub:${f.id}:43`)).status,'removed');
});
test('revoked visibility stops future private updates but allows removal of existing copy',async()=>{
  const f=await fixture();await f.core.subscriptions.change(viewer,f.id);await f.core.google.processJobs();f.state.deny=true;
  f.saved.record.title='Private new details';await f.store.put('event:'+f.id,f.saved);await f.core.subscriptions.fanout(f.id);await f.core.google.processJobs();
  assert.equal([...f.state.google.values()][0].summary,'Community night');assert.equal((await f.core.subscriptions.list('43'))[0].status,'Access unavailable');
  await f.core.subscriptions.change(viewer,f.id,true);await f.core.google.processJobs();assert.equal(f.state.google.size,0);
});
test('transient Discord check failures retry without disclosing new details',async()=>{
  const f=await fixture();await f.core.subscriptions.change(viewer,f.id);f.state.failMembership=true;
  await f.core.google.processJobs();assert.equal(f.state.google.size,0);
  assert.equal((await f.store.get(`google-job:sub:${f.id}:43`)).attempts,1);assert.equal((await f.core.subscriptions.list('43'))[0].active,true);
});
test('duplicate clicks and ambiguous Google create retain a single personal copy',async()=>{
  const f=await fixture();await f.core.subscriptions.change(viewer,f.id);const sub=await f.store.get(`google-sub:${f.id}:43`);
  await f.core.subscriptions.change(viewer,f.id);assert.equal((await f.store.get(`google-sub:${f.id}:43`)).eventId,sub.eventId);
  f.state.loseCreate=true;await f.core.google.processJobs();await f.core.subscriptions.change(viewer,f.id);await f.core.google.processJobs();assert.equal(f.state.google.size,1);
});
test('disconnect pauses subscriptions, requiring explicit resume',async()=>{
  const f=await fixture();await f.core.subscriptions.change(viewer,f.id);await f.core.google.disconnect('43');
  assert.equal((await f.core.subscriptions.list('43'))[0].status,'Disconnected');assert.equal((await f.store.list({prefix:'google-job:'})).size,0);
  await f.core.subscriptions.fanout(f.id);assert.equal((await f.store.list({prefix:'google-job:'})).size,0);
});
test('legacy organizer sync is converted without duplicating its Google event',async()=>{
  const f=await fixture();f.saved.record.googleCalendarSync=true;await f.store.put('event:'+f.id,f.saved);
  await f.core.google.enqueue(f.id,f.saved);await f.core.google.processJobs();assert.equal(f.state.google.size,1);
  await f.core.subscriptions.change(owner,f.id);await f.core.google.processJobs();assert.equal(f.state.google.size,1);
  assert.equal((await f.store.get('event:'+f.id)).record.googleCalendarSync,false);
  await f.core.subscriptions.change(owner,f.id,true);await f.core.google.processJobs();assert.equal(f.state.google.size,0);
});
test('personal subscriptions cannot authorize editing the original event',async()=>{
  const f=await fixture();await f.core.subscriptions.change(viewer,f.id);
  await assert.rejects(f.core.publish(viewer,f.id,{event:f.saved.record}));assert.equal(f.state.discordWrites,0);
  assert.equal((await f.core.handle(new Request(env.PUBLIC_ORIGIN+'/google/subscriptions/'+f.id,{method:'POST'}))).status,401);
});


test('publishing an organizer update enqueues personal subscriptions automatically',async()=>{
  const f=await fixture();f.saved.channelId='';f.saved.record.channelId='';f.saved.messageId='';await f.store.put('event:'+f.id,f.saved);
  await f.core.subscriptions.change(viewer,f.id);await f.core.google.processJobs();
  f.saved.record.title='Actual published edit';
  const result=await f.core.publish(owner,f.id,{event:f.saved.record});assert.equal(result.eventId,'777');
  assert.equal((await f.core.subscriptions.list('43'))[0].status,'Queued');
  await f.core.google.processJobs();assert.equal([...f.state.google.values()][0].summary,'Actual published edit');
});
