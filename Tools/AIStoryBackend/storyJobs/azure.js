'use strict';
const { BlobServiceClient, StorageSharedKeyCredential, BlobSASPermissions, generateBlobSASQueryParameters } = require('@azure/storage-blob');
const { QueueClient } = require('@azure/storage-queue');
const { createService } = require('./core');
let service;
function getService() {
  if (service) return service;
  const connection = process.env.AZURE_STORAGE_CONNECTION_STRING;
  if (!connection) throw new Error('STORAGE_UNAVAILABLE');
  const blobs = BlobServiceClient.fromConnectionString(connection, { retryOptions: { maxTries: 1, tryTimeoutInMs: 2000 } });
  const jobs = blobs.getContainerClient('pocketstriker-story-jobs');
  const queue = new QueueClient(connection, 'pocketstriker-story-jobs', { retryOptions: { maxTries: 1, tryTimeoutInMs: 2000 } });
  let initialized;
  async function initialize() {
    if (!initialized) initialized = Promise.all([jobs.createIfNotExists(), queue.createIfNotExists()]).catch(e => { initialized = null; throw e; });
    await initialized;
  }
  const client = id => jobs.getBlockBlobClient(`${id}.json`);
  const store = {
    async createIfAbsent(id, value) {
      await initialize();
      try { await client(id).uploadData(Buffer.from(JSON.stringify(value)), { conditions: { ifNoneMatch: '*' }, blobHTTPHeaders: { blobContentType: 'application/json' } }); }
      catch (e) { if (![409, 412].includes(e.statusCode)) throw e; }
    },
    async read(id) { try { return JSON.parse((await client(id).downloadToBuffer()).toString()); } catch (e) { if (e.statusCode === 404) return null; throw e; } },
    async acquire(id) {
      const blob = client(id), lease = blob.getBlobLeaseClient();
      try { await lease.acquireLease(60); } catch(e) { if ([404,409,412].includes(e.statusCode)) return null; throw e; }
      let lost = false;
      const timer = setInterval(() => lease.renewLease().catch(() => { lost = true; }), 20000);
      return {
        async write(value) {
          if (lost) throw new Error('JOB_LEASE_LOST');
          await blob.uploadData(Buffer.from(JSON.stringify(value)), { conditions: { leaseId: lease.leaseId }, blobHTTPHeaders: { blobContentType: 'application/json' } });
        },
        async release() { clearInterval(timer); try { await lease.releaseLease(); } catch (_) {} }
      };
    }
  };
  async function generate(kind, input) {
    const handler = require(kind === 'text' ? '../generateGeminiText' : '../generateGeminiImages');
    const context = { log: Object.assign(() => {}, { error: () => {}, warn: () => {} }) };
    await handler(context, { body: { FunctionArgument: input } });
    if (context.res?.status !== 200) throw new Error('PROVIDER_FAILED');
    if (kind === 'text') {
      if (!context.res.body?.text) throw new Error('EMPTY_TEXT');
      return { text: context.res.body.text };
    }
    const containerName = process.env.IMAGE_CONTAINER_NAME || 'ai-images';
    const expected = new URL(blobs.getContainerClient(containerName).url);
    const images = (context.res.body?.images || []).map(image => {
      const url = new URL(image.url);
      if (url.origin !== expected.origin || !url.pathname.startsWith(expected.pathname + '/')) throw new Error('UNEXPECTED_IMAGE_URL');
      return { blobName: decodeURIComponent(url.pathname.slice(expected.pathname.length + 1)), mimeType: image.mimeType };
    });
    if (!images.length) throw new Error('EMPTY_IMAGE');
    // Signed URLs are neither logged nor persisted in the job record.
    return { images };
  }
  async function materialize(result, kind) {
    if (kind === 'text') return result;
    const { parseAccountFromConnectionString } = require('../generateGeminiImages');
    const { accountName, accountKey } = parseAccountFromConnectionString(connection);
    const credential = new StorageSharedKeyCredential(accountName, accountKey);
    const containerName = process.env.IMAGE_CONTAINER_NAME || 'ai-images';
    return { images: result.images.map(image => {
      const sas = generateBlobSASQueryParameters({ containerName, blobName: image.blobName, permissions: BlobSASPermissions.parse('r'), startsOn: new Date(Date.now() - 300000), expiresOn: new Date(Date.now() + 3600000) }, credential).toString();
      return { url: `${blobs.getContainerClient(containerName).getBlockBlobClient(image.blobName).url}?${sas}`, mimeType: image.mimeType };
    }) };
  }
  service = createService({ store, generate, materialize, enqueue: id => queue.sendMessage(Buffer.from(JSON.stringify({ id })).toString('base64')) });
  return service;
}
module.exports = { getService };
