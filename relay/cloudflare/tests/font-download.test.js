import test from 'node:test';
import assert from 'node:assert/strict';
import {mkdtemp,readFile,writeFile} from 'node:fs/promises';
import {tmpdir} from 'node:os';
import {join} from 'node:path';
import {pathToFileURL} from 'node:url';
import {fontSource,validFont,download,cachedDownload} from '../font-download.mjs';

test('Special Elite uses its Apache source and license; other families retain OFL',()=>{
  assert.match(fontSource('Special Elite').base,/\/apache\/specialelite\/$/);
  assert.equal(fontSource('Special Elite').licenseFile,'LICENSE.txt');
  assert.match(fontSource('Orbitron').base,/\/ofl\/orbitron\/$/);
});
test('transient failures retry, permanent 404 stops immediately',async()=>{
  let calls=0;const options={sleep:async()=>{},log:()=>{},fetcher:async()=>++calls<3?new Response('',{status:503}):new Response('ready')};
  assert.equal((await download('https://example.test/font',options)).toString(),'ready');assert.equal(calls,3);
  calls=0;options.fetcher=async()=>{calls++;return new Response('',{status:404});};
  await assert.rejects(download('https://example.test/missing',options),/HTTP 404/);assert.equal(calls,1);
});
test('cached valid fonts resume without network, truncated files are replaced',async()=>{
  const dir=await mkdtemp(join(tmpdir(),'eh-font-download-')),path=pathToFileURL(join(dir,'font.ttf'));
  const bytes=await readFile(new URL('../../../assets/fonts/Cinzel.ttf',import.meta.url));
  await writeFile(path,bytes);let calls=0;
  const options={fetcher:async()=>{calls++;return new Response(bytes);}};
  assert.deepEqual(await cachedDownload('https://example.test/font',path,validFont,options),bytes);assert.equal(calls,0);
  await writeFile(path,bytes.subarray(0,100));
  assert.deepEqual(await cachedDownload('https://example.test/font',path,validFont,options),bytes);assert.equal(calls,1);
});
