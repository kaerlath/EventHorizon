import test from 'node:test';
import assert from 'node:assert/strict';
import {DmReminders} from '../src/dm-reminders.js';
class Store {
  data=new Map();
  async get(k){return structuredClone(this.data.get(k));}
  async put(k,v){this.data.set(k,structuredClone(v));}
  async delete(k){this.data.delete(k);}
  async list({prefix=''}){return new Map([...this.data].filter(([k])=>k.startsWith(prefix)).map(([k,v])=>[k,structuredClone(v)]));}
  async setAlarm(){}
}
function fixture(){
  const store=new Store(),calls=[],core={store,discord:async(method,path,body)=>{calls.push({method,path,body});return {id:'123'};}};
  const dm=new DmReminders(core),id=crypto.randomUUID(),login={userId:'42'};
  const date=new Date(Date.now()+86400000);date.setUTCSeconds(0,0);
  const body={enabled:true,minutes:[60,15],recipient_id:'999',event:{id,title:'Personal @everyone',personalOnly:true,startLocal:date.toISOString().slice(0,16).replace('T',' '),timeZoneId:'UTC',durationMinutes:30}};
  return {store,calls,core,dm,id,login,body,start:date.getTime()};
}
test('opt in sends two private DMs from persistent alarms without a player session',async()=>{
  const f=fixture();assert.equal((await f.dm.status('42',f.id)).enabled,false);
  await f.dm.change(f.login,f.id,f.body);
  const restarted=new DmReminders(f.core);
  await restarted.process(f.start-60*60000);assert.equal(f.calls.length,2);
  assert.equal(f.calls[0].body.recipient_id,'42');assert.equal(f.calls[1].path,'channels/123/messages');
  assert.deepEqual(f.calls[1].body.allowed_mentions,{parse:[]});
  await restarted.process(f.start-15*60000);assert.equal(f.calls.length,4);
  await restarted.process(f.start);assert.equal(f.calls.length,4);
});
test('opt out cancels unsent messages; other users cannot cancel them',async()=>{
  const f=fixture();await f.dm.change(f.login,f.id,f.body);
  await f.dm.change({userId:'99'},f.id,{enabled:false});
  assert.equal((await f.dm.status('42',f.id)).enabled,true);
  await f.dm.change(f.login,f.id,{enabled:false});await f.dm.process(f.start);assert.equal(f.calls.length,0);
});
test('rescheduling replaces pending reminders and duplicate times produce one DM',async()=>{
  const f=fixture();await f.dm.change(f.login,f.id,f.body);
  f.body.minutes=[15,15];await f.dm.change(f.login,f.id,f.body);
  await f.dm.process(f.start-60*60000);assert.equal(f.calls.length,0);
  await f.dm.process(f.start-15*60000);assert.equal(f.calls.length,2);
  await f.dm.change(f.login,f.id,f.body);await f.dm.process(f.start-15*60000);assert.equal(f.calls.length,2);
});
test('failed or ambiguous sends are recorded and not repeatedly resent',async()=>{
  const f=fixture();await f.dm.change(f.login,f.id,f.body);
  f.core.discord=async()=>{throw Error('Blocked');};await f.dm.process(f.start-60*60000);
  assert.match((await f.dm.status('42',f.id)).message,/failed or unconfirmed/);
  f.core.discord=async()=>{throw Error('Must not retry');};await f.dm.process(f.start-59*60000);
  const saved=await f.store.get(`personal-dm:42:${f.id}`);saved.deliveries[1].status='Sending';await f.store.put(`personal-dm:42:${f.id}`,saved);
  await f.dm.process(f.start-15*60000);assert.match((await f.dm.status('42',f.id)).message,/Delivery uncertain/);
});
test('test reminders target caller and are limited to one per minute',async()=>{
  const f=fixture();await f.dm.test(f.login);assert.equal(f.calls[0].body.recipient_id,'42');
  await assert.rejects(()=>f.dm.test(f.login),/one minute/);
});
