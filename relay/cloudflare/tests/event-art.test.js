import test from 'node:test';
import assert from 'node:assert/strict';
import {RelayCore} from '../src/core.js';
import {saveEventArt} from '../src/event-art.js';
class Storage {
  data=new Map();
  async get(k){return structuredClone(this.data.get(k));}
  async put(k,v){if(typeof k==='object'){for(const [key,value] of Object.entries(k))this.data.set(key,structuredClone(value));}else this.data.set(k,structuredClone(v));}
  async delete(k){this.data.delete(k);}
}
const id='12345678-1234-1234-1234-123456789abc',env={PUBLIC_ORIGIN:'https://relay.example',DISCORD_CLIENT_ID:'1',DISCORD_CLIENT_SECRET:'s',DISCORD_BOT_TOKEN:'b'};
async function fixture(){
  const store=new Storage(),state={allowed:true,renders:0,writes:0};
  const core=new RelayCore(store,env,async()=>{state.renders++;return new Uint8Array([1,2,3]);},async(url,init)=>{
    if(init.method!=='GET')state.writes++;
    if(url.includes('/members/'))return state.allowed?Response.json({roles:[]}):new Response('{}',{status:403});
    if(url.endsWith('/channels'))return Response.json([{id:'456',permission_overwrites:[]}]);
    if(url.endsWith('/roles'))return Response.json([{id:'123',permissions:'1024'}]);
    return Response.json({owner_id:'other'});
  });
  await store.put('session:viewer',{userId:'viewer',expires:Date.now()+60000});
  await store.put('event:'+id,{userId:'organizer',guildId:'123',channelId:'456',eventId:'789',bannerChunks:0,record:{id,title:'Event',description:'Saved description',location:'Garden',world:'Balmung',startLocal:'2035-06-12 18:00',timeZoneId:'UTC',durationMinutes:120,guildId:'123',channelId:'456',recurrence:'None'}});
  const get=()=>core.handle(new Request(env.PUBLIC_ORIGIN+'/events/'+id+'/image',{headers:{Authorization:'Bearer viewer'}}));
  return {store,core,state,get};
}
test('published artwork returns exact stored bytes to authorized viewers, without rendering',async()=>{
  const {store,state,get}=await fixture();const bytes=new Uint8Array(100000).fill(9);
  await saveEventArt(store,id,bytes,true);
  const result=await(await get()).json();assert.equal(result.published,true);assert.equal(result.imageBase64,Buffer.from(bytes).toString('base64'));assert.equal(state.renders,0);
  await saveEventArt(store,id,new Uint8Array([7]),true);
  assert.equal((await(await get()).json()).imageBase64,'Bw==');assert.equal(await store.get(`art:${id}:1`),undefined);
});
test('cached artwork rechecks current access and requires login',async()=>{
  const {store,core,state,get}=await fixture();await saveEventArt(store,id,new Uint8Array([9]),true);
  state.allowed=false;assert.equal((await get()).status,403);
  assert.equal((await core.handle(new Request(env.PUBLIC_ORIGIN+'/events/'+id+'/image'))).status,401);
});
test('older events reconstruct saved data without posting and refresh after edits',async()=>{
  const {state,get}=await fixture();const response=await get();assert.equal(response.status,200);
  assert.equal((await response.json()).published,false);assert.equal(state.renders,1);assert.equal(state.writes,0);
  await get();assert.equal(state.renders,2);
});
