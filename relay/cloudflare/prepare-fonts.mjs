// Run automatically before Wrangler builds. Files remain unmodified with their licenses.
import {mkdir,readFile,writeFile,rename} from 'node:fs/promises';
import {fileURLToPath} from 'node:url';
import {sharedFonts} from './src/font-catalog.js';
import {fontSource,validFont,cachedDownload} from './font-download.mjs';
const dest = new URL('../../assets/fonts/shared/',import.meta.url);
const cache = new URL('./.font-cache/',import.meta.url);
await mkdir(dest,{recursive:true});
await mkdir(cache,{recursive:true});
let cached;
try { cached=JSON.parse(await readFile(new URL('catalog.json',dest),'utf8')); } catch {}
if(cached && Object.keys(sharedFonts).every(name=>cached.some(e=>e.name===name))) {
  let intact=true;
  for(const e of cached) for(const file of [e.license,...e.faces.map(f=>f.file)]) {
    try {const bytes=await readFile(new URL(file,dest));if(file.endsWith('.ttf')?!validFont(bytes):!bytes.length)intact=false;}catch{intact=false;}
  }
  if(intact){console.log('Shared font collection ready.');process.exit(0);}
}
const catalog=[];
for(const [name,category] of Object.entries(sharedFonts)) {
  const {slug,base,licenseFile,licenseMarker}=fontSource(name);
  console.log(`Preparing ${name}...`);
  const meta=(await cachedDownload(base+'METADATA.pb',new URL(slug+'-metadata.txt',cache),b=>b.toString().includes(`name: "${name}"`)&&b.toString().includes('fonts {'))).toString('utf8');
  const licenseName=slug+'-'+licenseFile;
  await cachedDownload(base+licenseFile,new URL(licenseName,dest),b=>b.toString().includes(licenseMarker));
  const axis=[...meta.matchAll(/axes \{([\s\S]*?)\n\}/g)].map(m=>m[1]).find(x=>/tag: "wght"/.test(x));
  const range=axis?`${Number(/min_value: ([\d.]+)/.exec(axis)[1])} ${Number(/max_value: ([\d.]+)/.exec(axis)[1])}`:null;
  const faces=[];
  for(const m of meta.matchAll(/fonts \{([\s\S]*?)\n\}/g)) {
    const block=m[1],filename=/filename: "([^"]+)"/.exec(block)[1],style=/style: "([^"]+)"/.exec(block)[1],weight=Number(/weight: (\d+)/.exec(block)[1]);
    if(!filename.includes('[')&&![400,700].includes(weight))continue;
    const file=`${slug}-${faces.length}.ttf`;
    await cachedDownload(base+encodeURIComponent(filename),new URL(file,dest),validFont);
    faces.push({file,style,weight:filename.includes('wght')?range:String(weight)});
  }
  if(!faces.length)throw Error(`No font faces: ${name}`);
  catalog.push({name,category,faces,license:licenseName,source:base});
}
await writeFile(new URL('catalog.tmp',dest),JSON.stringify(catalog,null,2));
await rename(new URL('catalog.tmp',dest),new URL('catalog.json',dest));
console.log(`Ready: ${catalog.length} font families with licenses in ${fileURLToPath(dest)}`);
