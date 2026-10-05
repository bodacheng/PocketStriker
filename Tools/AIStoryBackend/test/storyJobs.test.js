'use strict';
const { test } = require('node:test');
const assert = require('node:assert/strict');
const crypto = require('node:crypto');
const { createService, normalize } = require('../storyJobs/core');
function fixture(generate = async () => ({ images: [{blobName:'test.jpg'}] })) {
 const rows = new Map(), locks = new Set(), queue = []; let time = 1000, calls = 0;
 const store = {
  async createIfAbsent(id, value) { if (!rows.has(id)) rows.set(id, structuredClone(value)); },
  async read(id) { return structuredClone(rows.get(id)); },
  async acquire(id) { if (locks.has(id) || !rows.has(id)) return null; locks.add(id); return { async write(v){ rows.set(id,structuredClone(v)); }, async release(){locks.delete(id);} }; }
 };
 const service=createService({store,enqueue:async id=>queue.push(id),generate:async(...a)=>{calls++;return generate(...a);},now:()=>time});
 return {...service,rows,queue,get calls(){return calls;},advance(){time+=31000;}};
}
const start = {operation:'start',kind:'image',input:{prompt:'red-crested fighter with a magic stone'}};
test('cold request returns queued without waiting for provider, then status returns completed result',async()=>{const f=fixture();const a=await f.handle(start);assert.equal(a.status,'queued');assert.equal(f.calls,0);await f.work(a.id);const b=await f.handle({operation:'status',id:a.id});assert.equal(b.status,'ready');assert.equal(b.result.images.length,1);assert.equal(f.calls,1);});
test('parallel duplicate deliveries generate once; warm start does not enqueue or regenerate',async()=>{let release;const wait=new Promise(r=>release=r);const f=fixture(async()=>{await wait;return {text:'ok'};});const a=await f.handle(start);const first=f.work(a.id);await new Promise(r=>setImmediate(r));await assert.rejects(f.work(a.id), /JOB_BUSY/);release();await first;const count=f.queue.length;assert.equal((await f.handle(start)).status,'ready');assert.equal(f.calls,1);assert.equal(f.queue.length,count);});
test('failed work is sanitized, ordinary status/start cannot resend; explicit retry is bounded and delayed',async()=>{const f=fixture(async()=>{throw new Error('SECRET provider URL');});const a=await f.handle(start);for(let n=1;n<=3;n++){await f.work(a.id);assert.equal(f.calls,n);assert.equal((await f.handle({operation:'status',id:a.id})).error,'GENERATION_FAILED');await f.handle(start);assert.equal(f.calls,n);const early=await f.handle({operation:'retry',id:a.id});assert.equal(early.status,'failed');f.advance();const retry=await f.handle({operation:'retry',id:a.id});assert.equal(retry.status,n<3?'queued':'failed');}assert.ok(!JSON.stringify([...f.rows.values()]).includes('SECRET'));});
test('queued record survives an interrupted enqueue and can be recovered on repeated start',async()=>{const f=fixture();const spec=normalize(start);f.rows.set(spec.id,{...spec,status:'queued',attempts:0});await f.handle(start);assert.equal(f.queue[0],spec.id);await f.work(spec.id);assert.equal(f.calls,1);});
test('new prompt is a true cold key; polling missing job never generates',async()=>{const f=fixture();const a=await f.handle(start);const b=await f.handle({...start,input:{prompt:'new story'}});assert.notEqual(a.id,b.id);assert.equal((await f.handle({operation:'status',id:'a'.repeat(64)})).status,'missing');assert.equal(f.calls,0);});
test('provider budget cannot be raised by client, malformed requests rejected',async()=>{const s=normalize({...start,input:{...start.input,sampleCount:99,timeoutMs:999999,imageModel:'expensive'}});assert.equal(s.input.sampleCount,1);assert.equal(s.input.timeoutMs,60000);assert.equal(s.input.imageModel,'gemini-3.1-flash-image');const f=fixture();await assert.rejects(f.handle({operation:'status',id:'../secret'}));await assert.rejects(f.handle({operation:'delete'}));await assert.rejects(f.handle({...start,input:{prompt:'x'.repeat(24001)}}));});

test('stale crashed worker can be explicitly retried, with a hard attempt ceiling',async()=>{const f=fixture();const a=await f.handle(start);const job=f.rows.get(a.id);f.rows.set(a.id,{...job,status:'running',attempts:1,updatedAt:-200000});assert.equal((await f.handle({operation:'status',id:a.id})).error,'GENERATION_EXPIRED');assert.equal((await f.handle({operation:'retry',id:a.id})).status,'queued');await f.work(a.id);assert.equal(f.calls,1);f.rows.set(a.id,{...job,status:'running',attempts:3,updatedAt:-200000});await f.work(a.id);assert.equal(f.calls,1);assert.equal((await f.handle({operation:'status',id:a.id})).error,'ATTEMPT_LIMIT');});

test('fresh image jobs use separate story indexes while preserving protocol and content IDs', async () => {
 const generated = [];
 const f = fixture(async (kind, input) => { generated.push(input); return { images: [{blobName:'test.jpg'}] }; });
 const a = await f.handle(start);
 const b = await f.handle({...start,input:{prompt:'a tiny robot at a moon carnival'}});
 await f.work(a.id); await f.work(b.id);
 assert.equal(generated.length,2);
 assert.notEqual(generated[0].storyId,generated[1].storyId);
 for (const input of generated) { assert.equal(input.storyId,input.cacheKey); assert.equal(input.sceneIndex,1); }
 const originalIdentityInput = {prompt:start.input.prompt,imageModel:'gemini-3.1-flash-image',sampleCount:1,aspectRatio:'9:16',timeoutMs:60000};
 const originalId = crypto.createHash('sha256').update(JSON.stringify({protocol:'pocket-story-jobs-v1',kind:'image',input:originalIdentityInput})).digest('hex');
 assert.equal(a.id,originalId);
 assert.equal(a.protocol,'pocket-story-jobs-v1');
});

test('portrait image jobs cannot reuse a cached landscape image or override the phone format', () => {
 const portrait = normalize({...start,input:{...start.input,aspectRatio:'16:9'}});
 assert.equal(portrait.input.aspectRatio,'9:16');
 const landscapeIdentityInput = {...portrait.input,aspectRatio:'16:9'};
 delete landscapeIdentityInput.cacheKey; delete landscapeIdentityInput.storyId; delete landscapeIdentityInput.sceneIndex;
 const landscapeId = crypto.createHash('sha256').update(JSON.stringify({protocol:'pocket-story-jobs-v1',kind:'image',input:landscapeIdentityInput})).digest('hex');
 assert.notEqual(portrait.id,landscapeId);
});

test('preexisting ready jobs retain their legacy image locations without regeneration', async () => {
 const f = fixture(); const spec = normalize(start);
 f.rows.set(spec.id,{...spec,input:{...spec.input,storyId:'pocketstriker'},status:'ready',attempts:1,updatedAt:1000,
  result:{images:[{blobName:'pocketstriker/scene_1/legacy.jpg'}]}});
 const replay = await f.handle(start);
 assert.equal(replay.status,'ready'); assert.equal(replay.id,spec.id); assert.equal(replay.generationAttempts,1);
 assert.equal(replay.result.images[0].blobName,'pocketstriker/scene_1/legacy.jpg');
 assert.equal(f.calls,0); assert.equal(f.queue.length,0);
});
