import test from 'node:test';
import assert from 'node:assert/strict';
import {RelayCore} from '../src/core.js';
class Storage {
  data=new Map(); alarm=null;
  async get(k){return structuredClone(this.data.get(k));}
  async put(k,v){this.data.set(k,structuredClone(v));}
  async delete(k){this.data.delete(k);}
  async getAlarm(){return this.alarm;}
  async setAlarm(v){this.alarm=v;}
}
const env={PUBLIC_ORIGIN:'https://relay.example',DISCORD_CLIENT_ID:'1234',DISCORD_CLIENT_SECRET:'secret',DISCORD_BOT_TOKEN:'bot'};
async function setup(fetcher=()=>{throw Error('Unexpected renewal');}){
  const store=new Storage();
  await store.put('session:player',{userId:'42',accessToken:'access',refreshToken:'refresh',accessExpires:Date.now()+86400000,expires:Date.now()+3600000});
  const core=new RelayCore(store,env,()=>{},fetcher);
  const request=(path,body)=>core.handle(new Request(env.PUBLIC_ORIGIN+path,{method:'POST',headers:{Authorization:'Bearer player','Content-Type':'application/json'},body:JSON.stringify(body??{})}));
  return {store,request};
}
test('play lease survives the original hour, but expires after a missed check-in',async()=>{
  const {store,request}=await setup();
  assert.equal((await request('/auth/heartbeat',{enabled:true})).status,200);
  const now=Date.now,base=now();
  try{
    for(let minutes=2;minutes<=122;minutes+=2){Date.now=()=>base+minutes*60000;assert.equal((await request('/auth/heartbeat',{enabled:true})).status,200);}
    Date.now=()=>base+138*60000;
    assert.equal((await request('/auth/heartbeat',{enabled:true})).status,401);
    assert.equal(await store.get('session:player'),undefined);
  }finally{Date.now=now;}
});
test('renewal rotates Discord credentials without exposing them to the plugin',async()=>{
  let calls=0;
  const {store,request}=await setup(async(url,init)=>{
    calls++;assert.equal(init.redirect,'manual');assert.equal(init.body.get('grant_type'),'refresh_token');assert.equal(init.body.get('refresh_token'),'refresh');
    return Response.json({access_token:'new-access',refresh_token:'rotated',expires_in:86400});
  });
  const login=await store.get('session:player');login.playSession=true;login.accessExpires=Date.now()+1000;await store.put('session:player',login);
  const response=await request('/auth/heartbeat',{enabled:true});assert.deepEqual(await response.json(),{ok:true});
  assert.equal(calls,1);assert.equal((await store.get('session:player')).refreshToken,'rotated');
  await request('/auth/heartbeat',{enabled:true});assert.equal(calls,1);
});
test('revoked authorization ends the session, transient errors retain a retryable lease',async()=>{
  for(const status of [400,401,429,503,302]){
    const {store,request}=await setup(async()=>new Response('{}',{status}));
    const login=await store.get('session:player');login.playSession=true;login.accessExpires=Date.now();await store.put('session:player',login);
    const revoked=status===400||status===401;
    assert.equal((await request('/auth/heartbeat',{enabled:true})).status,revoked?401:503);
    assert.equal(!!await store.get('session:player'),!revoked);
  }
});
test('opt out restores bounded expiry and logout cannot be undone by a heartbeat',async()=>{
  const {store,request}=await setup();
  await request('/auth/heartbeat',{enabled:true});
  await request('/auth/heartbeat',{enabled:false});
  const login=await store.get('session:player');assert.equal(login.playSession,false);assert.ok(login.expires<=Date.now()+3600000);
  await request('/auth/logout');
  assert.equal((await request('/auth/heartbeat',{enabled:true})).status,401);
});
test('heartbeat requires an authenticated session and boolean mode',async()=>{
  const {request}=await setup();assert.equal((await request('/auth/heartbeat',{enabled:'yes'})).status,400);
  const core=new RelayCore(new Storage(),env,()=>{});
  assert.equal((await core.handle(new Request(env.PUBLIC_ORIGIN+'/auth/heartbeat',{method:'POST',body:'{"enabled":true}'}))).status,401);
});
