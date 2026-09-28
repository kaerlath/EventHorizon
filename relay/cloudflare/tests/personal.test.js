import test from 'node:test';
import assert from 'node:assert/strict';
import {RelayCore} from '../src/core.js';

class Storage {
  data=new Map();
  async get(k){return structuredClone(this.data.get(k));}
  async put(k,v){this.data.set(k,structuredClone(v));}
  async delete(k){this.data.delete(k);}
  async list({prefix=''}){return new Map([...this.data].filter(([k])=>k.startsWith(prefix)).map(([k,v])=>[k,structuredClone(v)]));}
  async setAlarm(){} async getAlarm(){return 1;}
}
async function fixture(){
  const store=new Storage(),calls=[],core=new RelayCore(store,{PUBLIC_ORIGIN:'https://relay.example',DISCORD_CLIENT_ID:'1',DISCORD_CLIENT_SECRET:'s',DISCORD_BOT_TOKEN:'b',GOOGLE_CLIENT_ID:'g',GOOGLE_CLIENT_SECRET:'s',GOOGLE_TOKEN_KEY:'ab'.repeat(32)},()=>{throw Error('No render');},()=>{throw Error('No Discord calls allowed');});
  for(const userId of ['a','b']){
    await store.put('session:'+userId,{userId,expires:Date.now()+3600000});
    await store.put('google:'+userId,{subject:userId,calendarId:'cal'+userId});
  }
  core.google.api=async(user,c,method,path,body)=>{calls.push({user,method,path,body});return {};};
  const id=crypto.randomUUID(),record={id,title:'Private appointment',description:'Only mine',startLocal:'2035-01-20 12:00',timeZoneId:'UTC',durationMinutes:30,personalOnly:true};
  const request=(user,method,body=record,path='/google/personal/'+id)=>core.handle(new Request('https://relay.example'+path,{method,headers:{Authorization:'Bearer '+user,'Content-Type':'application/json'},body:method==='PUT'?JSON.stringify(body):undefined}));
  return {core,store,calls,id,record,request};
}
test('private sync uses Google only and creates no community record',async()=>{
  const f=await fixture();assert.equal((await f.request('a','PUT')).status,200);
  assert.equal((await f.store.list({prefix:'event:'})).size,0);
  await f.core.google.processJobs();assert.equal(f.calls.length,1);
  assert.equal(f.calls[0].user,'a');assert.equal(f.calls[0].body.visibility,'private');
  assert.equal(f.calls[0].body.description,'Only mine');assert.ok(!JSON.stringify(f.calls).includes('discord.com'));
});
test('another account cannot remove or overwrite the original private copy',async()=>{
  const f=await fixture();await f.request('a','PUT');await f.core.google.processJobs();
  await f.request('b','DELETE');assert.equal((await f.store.list({prefix:'google-job:'})).size,0);
  await f.request('b','PUT',{...f.record,title:'Other account'});await f.core.google.processJobs();
  assert.equal((await f.store.get(`personal:a:${f.id}`)).record.title,'Private appointment');
  assert.notEqual(f.calls[0].body.id,f.calls[1].body.id);
});
test('removal deletes only the caller Google copy; readd gets a fresh ID',async()=>{
  const f=await fixture();await f.request('a','PUT');await f.core.google.processJobs();const first=f.calls[0].body.id;
  await f.request('a','DELETE');assert.equal((await f.request('a','PUT')).status,409);
  await f.core.google.processJobs();assert.equal(f.calls[1].method,'DELETE');
  await f.request('a','PUT');await f.core.google.processJobs();assert.notEqual(f.calls[2].body.id,first);
});
test('private events cannot pass through Discord publish and require authentication',async()=>{
  const f=await fixture();assert.equal((await f.request('missing','PUT')).status,401);
  assert.equal((await f.request('a','PUT',{event:f.record},`/events/${f.id}/publish`)).status,400);
  assert.equal((await f.request('a','PUT',{...f.record,personalOnly:false})).status,400);
});
test('transient Google failures retain retry job and stable ID',async()=>{
  const f=await fixture();await f.request('a','PUT');const first=await f.store.get(`personal:a:${f.id}`);
  f.core.google.api=async()=>{throw Error('Temporary outage');};await f.core.google.processJobs();
  const jobs=await f.store.list({prefix:'google-job:'});assert.equal([...jobs.values()][0].attempts,1);
  await f.request('a','PUT',{...f.record,title:'Changed'});
  assert.equal((await f.store.get(`personal:a:${f.id}`)).eventId,first.eventId);
});
