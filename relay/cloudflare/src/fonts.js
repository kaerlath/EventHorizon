import {RelayError,b64} from './model.js';
import {customFontId,usedFonts,sharedFonts} from './font-catalog.js';
const MAX=1024*1024, CHUNK=90000;
export function validateFont(bytes) {
  if(!(bytes instanceof Uint8Array)||bytes.length<48||bytes.length>MAX)throw new RelayError('Choose a font between 48 bytes and 1 MB.');
  const v=new DataView(bytes.buffer,bytes.byteOffset,bytes.byteLength),tag=String.fromCharCode(...bytes.slice(0,4));
  if(tag==='wOF2'||tag==='wOFF') {
    const header=tag==='wOF2'?48:44;
    if(v.getUint32(8)!==bytes.length||!v.getUint16(12)||v.getUint16(12)>256||v.getUint16(14)!==0||v.getUint32(16)>8*MAX||v.getUint32(16)<12||bytes.length<=header)throw new RelayError('Invalid web font header.');
    if(tag==='wOFF') {
      if(header+v.getUint16(12)*20>bytes.length)throw new RelayError('Invalid web font directory.');
      for(let i=0;i<v.getUint16(12);i++) {const p=header+i*20;if(v.getUint32(p+4)+v.getUint32(p+8)>bytes.length||v.getUint32(p+8)>v.getUint32(p+12))throw new RelayError('Invalid web font table.');}
    } else if(v.getUint32(20)>bytes.length-header)throw new RelayError('Invalid compressed font size.');
    return tag==='wOF2'?'woff2':'woff';
  }
  if(v.getUint32(0)!==0x00010000&&tag!=='OTTO')throw new RelayError('Choose a TTF, OTF, WOFF or WOFF2 file. Font collections are not supported.');
  const count=v.getUint16(4);
  if(!count||count>256||12+count*16>bytes.length)throw new RelayError('Invalid font table directory.');
  const tags=new Set();
  for(let i=0;i<count;i++){
    const p=12+i*16,offset=v.getUint32(p+8),length=v.getUint32(p+12);
    if(offset<12+count*16||offset+length>bytes.length)throw new RelayError('Invalid font table bounds.');
    tags.add(String.fromCharCode(...bytes.slice(p,p+4)));
  }
  if(!['head','cmap','name'].every(t=>tags.has(t)))throw new RelayError('The font is missing required tables.');
  return tag==='OTTO'?'otf':'ttf';
}
export async function importFont(store,userId,body) {
  if(body.LicenseConfirmed!==true&&body.licenseConfirmed!==true)throw new RelayError('Confirm that your font permits uploading and embedding.');
  const name=(body.Name??body.name??'').trim(),data=body.Data??body.data;
  if(!name||name.length>64||/[\x00-\x1f<>]/.test(name))throw new RelayError('Use a font name of 1–64 characters.');
  if(typeof data!=='string'||data.length>Math.ceil(MAX/3)*4||!/^[A-Za-z0-9+/]+={0,2}$/.test(data))throw new RelayError('Invalid font upload. Maximum size is 1 MB.');
  let bytes;try{bytes=Uint8Array.from(atob(data),c=>c.charCodeAt(0));}catch{throw new RelayError('Invalid font encoding.');}
  const format=validateFont(bytes);
  const hash=Array.from(new Uint8Array(await crypto.subtle.digest('SHA-256',new TextEncoder().encode(userId+':'+data))),b=>b.toString(16).padStart(2,'0')).join('');
  const id='custom-'+hash,key='font-meta:'+id,existing=await store.get(key);
  if(existing)return {id,name:existing.name,format:existing.format};
  const mine=await store.list({prefix:'font-user:'+userId+':',limit:21});
  if(mine.size>=20)throw new RelayError('This account has reached its 20 imported font limit.');
  if((await store.list({prefix:'font-meta:',limit:2000})).size>=2000)throw new RelayError('The relay font library is full. Contact the relay operator.',503);
  const value={id,name,format,userId,chunks:Math.ceil(data.length/CHUNK)},entries={[key]:value,['font-user:'+userId+':'+id]:{id,name,format}};
  for(let i=0;i<value.chunks;i++)entries['font-data:'+id+':'+i]=data.slice(i*CHUNK,(i+1)*CHUNK);
  await store.put(entries);
  return {id,name,format};
}
export async function requireFonts(store,userId,style) {
  for(const id of usedFonts(style).filter(customFontId)) {
    const meta=await store.get('font-meta:'+id);
    if(!meta||meta.userId!==userId)throw new RelayError('An imported font is unavailable for this account or relay. Import your own copy or choose a shared font.',403);
  }
}
export async function fontCss(style,assets,store) {
  let css='',catalog;
  for(const name of usedFonts(style)) {
    if(sharedFonts[name]) {
      if(!catalog){const r=await assets?.fetch('https://fonts.internal/catalog.json');if(!r?.ok)throw new RelayError('The shared font collection needs to be deployed.',503);catalog=await r.json();}
      const entry=catalog.find(e=>e.name===name);
      if(!entry)throw new RelayError('The selected shared font is unavailable.',503);
      for(const face of entry.faces){
        if(!/^[a-z0-9-]+\.ttf$/.test(face.file)||!/^\d+( \d+)?$/.test(face.weight)||!['normal','italic'].includes(face.style))throw new Error('Invalid bundled font manifest');
        const response=await assets.fetch('https://fonts.internal/'+face.file);
        if(!response.ok)throw new RelayError('The selected font file is unavailable.',503);
        css+=`@font-face{font-family:'${name}';src:url(data:font/ttf;base64,${b64(new Uint8Array(await response.arrayBuffer()))});font-weight:${face.weight};font-style:${face.style};}`;
      }
    } else if(customFontId(name)) {
      const meta=await store.get('font-meta:'+name);
      if(!meta)throw new RelayError('An imported font is missing. Choose another font.',400);
      let data='';for(let i=0;i<meta.chunks;i++){const chunk=await store.get('font-data:'+name+':'+i);if(typeof chunk!=='string')throw new Error('Missing font chunk');data+=chunk;}
      css+=`@font-face{font-family:'${name}';src:url(data:font/${meta.format};base64,${data});}`;
    }
    if(css.length>8*MAX)throw new RelayError('This announcement uses too much font data. Choose fewer different font families.');
  }
  return css;
}
