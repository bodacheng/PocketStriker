'use strict';
const assert=require('node:assert/strict');
const {EMULATOR_ACCOUNT_NAME:account,EMULATOR_ACCOUNT_KEY:key}=require('azurite/dist/src/common/utils/constants');
// Always override cloud configuration: this test uses ONLY the local emulator and fake provider.
process.env.AZURE_STORAGE_CONNECTION_STRING=`DefaultEndpointsProtocol=http;AccountName=${account};AccountKey=${key.toString('base64')};BlobEndpoint=http://127.0.0.1:11000/${account};QueueEndpoint=http://127.0.0.1:11001/${account};`;
Object.assign(process.env,{GEMINI_API_KEY:'local-fixture-not-a-key',IMAGE_CONTAINER_NAME:'fixture-images',STORY_INDEX_CONTAINER_NAME:'fixture-index',STORY_CONTAINER_NAME:'fixture-text'});
const {BlobServiceClient}=require('@azure/storage-blob');const {QueueClient}=require('@azure/storage-queue');
const {getService}=require('../storyJobs/azure');const http=require('../storyJobs/http');const worker=require('../processStoryJob');const {normalize}=require('../storyJobs/core');
const nativeFetch=global.fetch;let providerCalls=0,fail=false;
const png='iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVQIHWP4z8DwHwAFgAI/ScLbtAAAAABJRU5ErkJggg==';
global.fetch=async url=>{assert.equal(new URL(url).hostname,'generativelanguage.googleapis.com');providerCalls++;if(fail)throw new Error('fixture private failure');return {ok:true,status:200,text:async()=>JSON.stringify(String(url).endsWith('/interactions')?{steps:[{type:'model_output',content:[{type:'image',data:png,mime_type:'image/png'}]}]}:{candidates:[{content:{parts:[{text:'{"title":"Fixture","lines":["Magic stones unite rivals."],"visualPrompt":"friendly fighters"}'}]}}]})};};
(async()=>{
const service=getService(),prefix='local fixture '+Date.now(),checks=[];
const queue=new QueueClient(process.env.AZURE_STORAGE_CONNECTION_STRING,'pocketstriker-story-jobs');const blobs=BlobServiceClient.fromConnectionString(process.env.AZURE_STORAGE_CONNECTION_STRING);
for(const kind of ['text','image']){
 const request={operation:'start',kind,input:{prompt:prefix+' '+kind}},context={},begin=Date.now();await http(context,request);assert.equal(context.res.status,200);assert.equal(context.res.body.status,'queued');assert.ok(Date.now()-begin<7000);
 const id=context.res.body.id,calls=providerCalls;let found=false;
 for(const m of (await queue.receiveMessages({numberOfMessages:16})).receivedMessageItems){const payload=JSON.parse(Buffer.from(m.messageText,'base64').toString());if(payload.id===id){found=true;const attempts=await Promise.allSettled([worker({},payload),worker({},payload)]);assert.ok(attempts.some(x=>x.status==='fulfilled'));assert.ok(attempts.every(x=>x.status==='fulfilled'||x.reason.message==='JOB_BUSY'));}await queue.deleteMessage(m.messageId,m.popReceipt);}
 assert.ok(found);assert.equal(providerCalls,calls+1);const ready=await service.handle({operation:'status',id});assert.equal(ready.status,'ready');
 const job=(await blobs.getContainerClient('pocketstriker-story-jobs').getBlockBlobClient(id+'.json').downloadToBuffer()).toString();assert.ok(!/[?&]sig=/.test(job));
 assert.equal((await service.handle(request)).status,'ready');assert.equal(providerCalls,calls+1);
 if(kind==='image'){assert.equal(new URL(ready.result.images[0].url).hostname,'127.0.0.1');const response=await nativeFetch(ready.result.images[0].url);assert.equal(response.status,200);assert.equal((await response.arrayBuffer()).byteLength,Buffer.from(png,'base64').length);}
 const legacy=require(kind==='text'?'../generateGeminiText':'../generateGeminiImages'),old={log:{error(){},warn(){}}};await legacy(old,{body:{FunctionArgument:normalize(request).input}});assert.equal(old.res.status,200);assert.ok(kind==='text'?old.res.body.text:old.res.body.images);assert.equal(providerCalls,calls+1);
 checks.push(kind+': bounded HTTP; actual queue; duplicate leases; warm cache; legacy compatibility; no persisted SAS');
}
fail=true;const failed=await service.handle({operation:'start',kind:'text',input:{prompt:prefix+' fail'}});await service.work(failed.id);assert.equal((await service.handle({operation:'status',id:failed.id})).error,'GENERATION_FAILED');checks.push('sanitized terminal provider failure');
// Slow body delivery must remain under the provider deadline after response headers arrive.
for (const kind of ['text','image']) {
 global.fetch=async(url,options)=>({ok:true,status:200,text:()=>new Promise((resolve,reject)=>{const timer=setTimeout(()=>reject(new Error('fixture outer limit')),2200);options.signal.addEventListener('abort',()=>{clearTimeout(timer);reject(new Error('aborted'));},{once:true});})});
 const legacy=require(kind==='text'?'../generateGeminiText':'../generateGeminiImages'),context={log:{error(){},warn(){}}},begin=Date.now();
 await legacy(context,{body:{FunctionArgument:{...normalize({kind,input:{prompt:prefix+' slow body'}}).input,timeoutMs:1000}}});
 assert.equal(context.res.status,502);assert.ok(Date.now()-begin<1800);checks.push(kind+': response-body timeout remains active after headers');
}
const report={passed:true,checks,providerCalls,limitation:'Actual local Azurite Blob/Queue SDK integration. Gemini stubbed; no Azure deployment or live cold/warm generation claimed.'};require('node:fs').writeFileSync('../../Azure/emulator-report.json',JSON.stringify(report,null,2));console.log(JSON.stringify(report,null,2));
})().catch(e=>{console.error(e.name+': '+e.message);process.exitCode=1;});
