import {readFile,writeFile,rename} from 'node:fs/promises';

export function fontSource(name) {
  const slug=name.toLowerCase().replaceAll(' ',''),apache=name==='Special Elite';
  return {slug,base:`https://raw.githubusercontent.com/google/fonts/main/${apache?'apache':'ofl'}/${slug}/`,licenseFile:apache?'LICENSE.txt':'OFL.txt',licenseMarker:apache?'Apache License':'SIL OPEN FONT LICENSE'};
}
export function validFont(bytes) {
  if(bytes.length<12)return false;
  if(bytes.readUInt32BE(0)!==0x10000 && bytes.toString('ascii',0,4)!=='OTTO')return false;
  const count=bytes.readUInt16BE(4);
  if(!count||count>256||12+count*16>bytes.length)return false;
  for(let i=0;i<count;i++) {
    const offset=12+i*16;
    if(bytes.readUInt32BE(offset+8)+bytes.readUInt32BE(offset+12)>bytes.length)return false;
  }
  return true;
}
export async function download(url,{fetcher=fetch,sleep=ms=>new Promise(r=>setTimeout(r,ms)),log=console.warn}={}) {
  for(let attempt=1;attempt<=4;attempt++) {
    try {
      const response=await fetcher(url,{signal:AbortSignal.timeout(60000)});
      if(!response.ok) {
        const error=new Error(`HTTP ${response.status}`);
        error.permanent=response.status>=400&&response.status<500&&![408,429].includes(response.status);
        await response.body?.cancel();throw error;
      }
      return Buffer.from(await response.arrayBuffer());
    } catch(error) {
      if(error.permanent||attempt===4)throw new Error(`Could not download ${url}: ${error.message}. Completed files are preserved; rerun deployment to resume.`,{cause:error});
      log(`Download interrupted (${error.message}); retry ${attempt}/3...`);
      await sleep(1000*2**(attempt-1));
    }
  }
}
export async function cachedDownload(url,path,validate,options) {
  try {const bytes=await readFile(path);if(validate(bytes))return bytes;}catch{}
  const bytes=await download(url,options);
  if(!validate(bytes))throw new Error(`Downloaded file failed validation: ${url}`);
  const temporary=new URL(path.href+'.partial');
  await writeFile(temporary,bytes);await rename(temporary,path);
  return bytes;
}
