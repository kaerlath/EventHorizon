import test from 'node:test';
import assert from 'node:assert/strict';
import {readFile} from 'node:fs/promises';
import {importFont,requireFonts,fontCss,validateFont} from '../src/fonts.js';
import {announcementStyle,styleCss} from '../src/typography.js';
import {sharedFonts} from '../src/font-catalog.js';
import {RelayCore} from '../src/core.js';
import {renderer} from '../src/card.js';
class Storage {
  data=new Map();
  async get(k){return structuredClone(this.data.get(k));}
  async put(k,v){if(typeof k==='object'){for(const [a,b] of Object.entries(k))this.data.set(a,structuredClone(b));}else this.data.set(k,structuredClone(v));}
  async list({prefix='',limit=Infinity}={}){return new Map([...this.data].filter(([k])=>k.startsWith(prefix)).slice(0,limit));}
}
const bytes=new Uint8Array(await readFile(new URL('../../../assets/fonts/Cinzel.ttf',import.meta.url)));
const data=Buffer.from(bytes).toString('base64'),body={name:'My Cinzel',data,licenseConfirmed:true};
test('real bundled TTF validates; malformed and oversized uploads fail',()=>{
  assert.equal(validateFont(bytes),'ttf');
  for(const value of [new Uint8Array(12),new Uint8Array(1024*1024+1),new TextEncoder().encode('<script>'.repeat(20))])assert.throws(()=>validateFont(value));
  const broken=bytes.slice();new DataView(broken.buffer).setUint32(20,0xffffffff);assert.throws(()=>validateFont(broken));
});
test('import persists chunked font, deduplicates and separates accounts',async()=>{
  const store=new Storage(),a=await importFont(store,'1',body),again=await importFont(store,'1',body),b=await importFont(store,'2',body);
  assert.equal(a.id,again.id);assert.notEqual(a.id,b.id);
  assert.equal((await store.list({prefix:'font-user:1:'})).size,1);
  assert.ok((await store.list({prefix:'font-data:'+a.id})).size>1);
  const style=announcementStyle({title:{font:a.id}});
  await requireFonts(store,'1',style);await assert.rejects(requireFonts(store,'2',style),/unavailable/);
  const restarted=new Storage();restarted.data=structuredClone(store.data);
  assert.match(await fontCss(style,null,restarted),new RegExp(a.id));
  assert.ok((await fontCss(style,null,restarted)).includes(data));
});
test('upload requires license confirmation and limits names, payload and account quota',async()=>{
  const store=new Storage();
  await assert.rejects(importFont(store,'1',{...body,licenseConfirmed:false}),/Confirm/);
  await assert.rejects(importFont(store,'1',{...body,name:'<script>'}),/name/);
  await assert.rejects(importFont(store,'1',{...body,data:'!!!!'}),/Invalid/);
  for(let i=0;i<20;i++)await store.put('font-user:1:'+i,{});
  await assert.rejects(importFont(store,'1',body),/20 imported/);
});
test('shared families survive style normalization, preserve weights and load only selected fonts',async()=>{
  assert.equal(Object.keys(sharedFonts).length,18);
  for(const name of Object.keys(sharedFonts))assert.ok(styleCss(announcementStyle({title:{font:name}}).title).includes(name));
  const calls=[],assets={fetch:async url=>{
    calls.push(url);
    if(url.endsWith('catalog.json'))return Response.json([{name:'Lora',faces:[{file:'lora-0.ttf',style:'normal',weight:'400 700'},{file:'lora-1.ttf',style:'italic',weight:'400 700'}]}]);
    return new Response(bytes);
  }};
  const css=await fontCss(announcementStyle({title:{font:'Lora'},body:{font:'Lora',italic:true}}),assets,new Storage());
  assert.equal(calls.length,3);assert.match(css,/font-style:italic/);assert.match(css,/font-weight:400 700/);
  await assert.rejects(fontCss(announcementStyle({title:{font:'Lora'}}),null,new Storage()),/deployed/);
});
test('font IDs cannot inject CSS or fetch external addresses',()=>{
  for(const font of ['custom-abc',"x';src:url(https://evil)",'https://evil/font.ttf','__proto__'])assert.throws(()=>announcementStyle({title:{font}}));
});
test('font endpoints require authentication and list metadata only for caller',async()=>{
  const store=new Storage(),env={PUBLIC_ORIGIN:'https://relay.example',DISCORD_CLIENT_ID:'1',DISCORD_CLIENT_SECRET:'secret',DISCORD_BOT_TOKEN:'bot'};
  const core=new RelayCore(store,env,()=>{});
  const request=(method,token,payload)=>new Request('https://relay.example/fonts',{method,headers:{Authorization:'Bearer '+token,'Content-Type':'application/json'},...(payload?{body:JSON.stringify(payload)}:{})});
  assert.equal((await core.handle(request('POST','',body))).status,401);
  await store.put('session:a',{userId:'1',expires:Date.now()+100000});await store.put('session:b',{userId:'2',expires:Date.now()+100000});
  const result=await core.handle(request('POST','a',body));assert.equal(result.status,200);
  const mine=await(await core.handle(request('GET','a'))).json();assert.equal(mine.length,1);assert.deepEqual(Object.keys(mine[0]).sort(),['format','id','name']);
  assert.deepEqual(await(await core.handle(request('GET','b'))).json(),[]);
});
test('renderer embeds imported font after restart and waits for font readiness',async()=>{
  const store=new Storage(),font=await importFont(store,'1',body);let options;
  const png=new Uint8Array(24);png.set([137,80,78,71,13,10,26,10]);
  const browser={quickAction:async (name,args)=>{options=args;return new Response(png);}};
  const render=renderer(browser,bytes,new Uint8Array(),null,store);
  await render({title:'Test',description:'A paragraph',announcementStyle:{title:{font:font.id}}},'Organizer','',{start:'2035-06-12T18:00:00Z',end:'2035-06-12T20:00:00Z',zone:'UTC'});
  assert.ok(options.html.includes(data));assert.match(options.waitForSelector.selector,/fonts-ready/);assert.match(options.html,/Promise.all/);
});
