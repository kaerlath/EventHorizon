import {RelayError,b64} from './model.js';
import {requireViewer} from './community.js';
import {validate} from './model.js';

export async function saveEventArt(store,id,bytes,published=false) {
  const data=b64(bytes),chunks=Math.ceil(data.length/90000),old=await store.get('art:'+id);
  const entries={['art:'+id]:{chunks,published,createdUtc:new Date().toISOString()}};
  for(let i=0;i<chunks;i++)entries[`art:${id}:${i}`]=data.slice(i*90000,(i+1)*90000);
  await store.put(entries);
  for(let i=chunks;i<(old?.chunks??0);i++)await store.delete(`art:${id}:${i}`);
}
export async function eventArt(core,login,id) {
  const saved=await core.store.get('event:'+id);
  await requireViewer(core,login.userId,saved);
  let meta=await core.store.get('art:'+id);
  if(!meta || !meta.published) {
    // Older publications have no retained PNG. Render their saved relay data,
    // without changing Discord, and label this as a reconstruction.
    const {item,start,end,zone}=validate(saved.record,id,0,true);
    const image=await core.render(item,item.organizer??'',await core.loadBanner(id,saved.bannerChunks),{start,end,zone});
    await saveEventArt(core.store,id,image,false);meta=await core.store.get('art:'+id);
  }
  let imageBase64='';
  for(let i=0;i<meta.chunks;i++){
    const chunk=await core.store.get(`art:${id}:${i}`);
    if(typeof chunk!=='string')throw new RelayError('The saved announcement image is incomplete. Save & Sync the event to refresh it.',503);
    imageBase64+=chunk;
  }
  return {imageBase64,published:meta.published,createdUtc:meta.createdUtc};
}
