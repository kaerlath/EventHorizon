import test from 'node:test';
import assert from 'node:assert/strict';
import {RelayCore} from '../src/core.js';
import {requireViewer} from '../src/community.js';

class Storage {
  data=new Map();
  async get(k){return structuredClone(this.data.get(k));}
  async put(k,v){this.data.set(k,structuredClone(v));}
  async delete(k){for(const id of Array.isArray(k)?k:[k])this.data.delete(id);}
  async list({prefix=''}){return new Map([...this.data].filter(([k])=>k.startsWith(prefix)).map(([k,v])=>[k,structuredClone(v)]));}
  async setAlarm(){} async getAlarm(){return 1;}
}
async function fixture(){
  const store=new Storage(),calls=[],state={fail:false,missing:false,googleFail:false};
  const env={PUBLIC_ORIGIN:'https://relay.example',DISCORD_CLIENT_ID:'1',DISCORD_CLIENT_SECRET:'s',DISCORD_BOT_TOKEN:'b',GOOGLE_CLIENT_ID:'g',GOOGLE_CLIENT_SECRET:'s',GOOGLE_TOKEN_KEY:'ab'.repeat(32)};
  const fetcher=async(url,init)=>{
    assert.equal(init.method,'DELETE');calls.push({url,method:init.method});
    return new Response(null,{status:state.fail?503:state.missing?404:204});
  };
  function connect(core){
    core.authorize=async()=>{};
    core.google.api=async(user,c,method,path)=>{
      assert.equal(method,'DELETE');if(state.googleFail)throw Error('Google outage');
      calls.push({user,method,path});return {};
    };
    return core;
  }
  const core=connect(new RelayCore(store,env,()=>{throw Error('No rendering');},fetcher));
  const id=crypto.randomUUID(),owner={userId:'42',expires:Date.now()+3600000};
  const record={id,title:'Delete me',startLocal:'2035-01-02 12:00',durationMinutes:60,timeZoneId:'UTC',googleCalendarSync:true};
  await store.put('event:'+id,{userId:'42',guildId:'1',channelId:'2',eventId:'3',messageId:'4',record,googleCopy:{subject:'42',calendarId:'c42',eventId:id.replaceAll('-','')}});
  for(const user of ['42','43','44'])await store.put('google:'+user,{subject:user,calendarId:'c'+user});
  for(const user of ['43','44'])await store.put(`google-sub:${id}:${user}`,{id,userId:user,subject:user,calendarId:'c'+user,eventId:'copy'+user,title:'Delete me',status:'Synced',active:true});
  await store.put('session:owner',owner);await store.put('session:other',{...owner,userId:'43'});
  const request=(token='owner',method='DELETE',suffix='')=>core.handle(new Request(`https://relay.example/events/${id}${suffix}`,{method,headers:{Authorization:'Bearer '+token}}));
  return {core,store,calls,state,id,owner,request,restart:()=>connect(new RelayCore(store,env,()=>{},fetcher))};
}
test('owner deletes both Discord objects and every managed Google copy including legacy organizer',async()=>{
  const f=await fixture();const r=await f.request();assert.equal(r.status,200);
  const result=await r.json();assert.equal(result.status,'Deleted');assert.equal(result.pendingGoogle,3);
  assert.equal(f.calls.length,2);await f.core.google.processJobs();assert.equal(f.calls.length,5);
  assert.deepEqual(f.calls.slice(2).map(c=>c.user).sort(),['42','43','44']);
  assert.equal((await f.core.deletion.status(f.owner,f.id)).pendingGoogle,0);
  await f.request();await f.core.google.processJobs();assert.equal(f.calls.length,5,'idempotent retry');
});
test('non-owner and unauthenticated deletion have no effects',async()=>{
  const f=await fixture();assert.equal((await f.request('other')).status,403);assert.equal((await f.request('bad')).status,401);
  assert.equal(f.calls.length,0);assert.equal((await f.store.get('event:'+f.id)).deleting,undefined);
  assert.equal((await f.request('other','GET','/deletion')).status,403);
});
test('partial Discord failure survives restart and 404 is successful removal',async()=>{
  const f=await fixture();f.state.fail=true;const r=await(await f.request()).json();assert.equal(r.status,'Deleting');
  const job=await f.store.get('delete-job:'+f.id);job.due=0;await f.store.put('delete-job:'+f.id,job);
  f.state.fail=false;f.state.missing=true;await f.restart().deletion.process();
  assert.equal((await f.store.get('event:'+f.id)).deleted,true);
});
test('deleting events cannot be viewed, republished, or subscribed again',async()=>{
  const f=await fixture();await f.request();
  await assert.rejects(()=>requireViewer(f.core,'43',f.store.data.get('event:'+f.id)),/not found/);
  await assert.rejects(()=>f.core.subscriptions.change({userId:'43'},f.id),/not found/);
  const record={id:f.id,title:'Try again',location:'Garden',guildId:'1',channelId:'2',startLocal:'2035-01-02 12:00',timeZoneId:'UTC',durationMinutes:60};
  f.core.channels=async()=>[{id:'2'}];
  await assert.rejects(()=>f.core.publish(f.owner,f.id,{event:record}),/deleted/);
});
test('disconnected Google copy remains pending until original account reconnects',async()=>{
  const f=await fixture();await f.store.delete('google:43');await f.request();await f.core.google.processJobs();
  assert.equal((await f.core.deletion.status(f.owner,f.id)).pendingGoogle,1);
  assert.equal((await f.store.get(`google-job:sub:${f.id}:43`)).attempts,1);
  await f.store.put('google:43',{subject:'43',calendarId:'c43'});
  await f.core.subscriptions.retry({userId:'43'});await f.core.google.processJobs();
  assert.equal((await f.core.deletion.status(f.owner,f.id)).pendingGoogle,0);
});
test('Google failure never recreates events and can be retried after Discord deletion',async()=>{
  const f=await fixture();await f.request();f.state.googleFail=true;await f.core.google.processJobs();
  assert.equal((await f.core.deletion.status(f.owner,f.id)).pendingGoogle,3);
  f.state.googleFail=false;
  for(const [k,j] of await f.store.list({prefix:'google-job:'})){j.due=0;await f.store.put(k,j);}
  await f.core.google.processJobs();assert.equal((await f.core.deletion.status(f.owner,f.id)).pendingGoogle,0);
});
test('tracked organizer copy is removed even when its legacy sync checkbox is off',async()=>{
  const f=await fixture(),saved=await f.store.get('event:'+f.id);
  saved.record.googleCalendarSync=false;await f.store.put('event:'+f.id,saved);
  await f.request();await f.core.google.processJobs();
  assert.equal(f.calls.filter(c=>c.user==='42').length,1);
});
