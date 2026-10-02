'use strict';
const crypto = require('node:crypto');
const VERSION = 'pocket-story-jobs-v1';
const sha = value => crypto.createHash('sha256').update(value).digest('hex');
function normalize(request) {
  const kind = request.kind;
  if (!['text', 'image'].includes(kind)) throw new Error('INVALID_KIND');
  const p = request.input || {};
  if (typeof p.prompt !== 'string' || !p.prompt.trim() || p.prompt.length > 24000) throw new Error('INVALID_PROMPT');
  // Fixed budget: one image, bounded provider waits, no client-selected paid model.
  const input = { prompt: p.prompt.trim() };
  if (kind === 'text') { input.model = 'gemini-2.5-flash-lite'; input.timeoutMs = 20000; }
  else { input.imageModel = 'gemini-3.1-flash-image'; input.sampleCount = 1; input.aspectRatio = '16:9'; input.timeoutMs = 60000; }
  const id = sha(JSON.stringify({ protocol: VERSION, kind, input }));
  input.cacheKey = `ps_${id}`;
  if (kind === 'image') { input.storyId = 'pocketstriker'; input.sceneIndex = 1; }
  return { id, kind, input };
}
function validId(id) { return typeof id === 'string' && /^[a-f0-9]{64}$/.test(id); }
function createService({ store, enqueue, generate, materialize = async x => x, now = Date.now }) {
  async function response(job) {
    const stale = job.status === "running" && now() - job.updatedAt > 120000;
    const result = { protocol: VERSION, id: job.id, status: stale ? "failed" : job.status, retryAfterMs: 2000, generationAttempts: job.attempts || 0 };
    if (job.status === 'ready') result.result = await materialize(job.result, job.kind);
    if (job.status === 'failed') result.error = job.error;
    if (stale) result.error = 'GENERATION_EXPIRED';
    return result;
  }
  async function handle(request) {
    if (!request || !['start', 'status', 'retry'].includes(request.operation)) throw new Error('INVALID_OPERATION');
    if (request.operation === 'start') {
      const spec = normalize(request);
      const proposed = { ...spec, status: 'queued', attempts: 0, createdAt: now(), updatedAt: now() };
      await store.createIfAbsent(spec.id, proposed);
      const job = await store.read(spec.id);
      // Re-enqueue queued jobs: a crash between blob creation and queue send is recoverable.
      // Duplicate messages are harmless: the worker leases the job and rechecks ready state.
      if (job.status === 'queued') await enqueue(job.id);
      return response(job);
    }
    if (!validId(request.id)) throw new Error('INVALID_ID');
    let job = await store.read(request.id);
    if (!job) return { protocol: VERSION, id: request.id, status: 'missing' };
    const retryable = value => value.status === 'failed' || (value.status === 'running' && now() - value.updatedAt > 120000);
    if (request.operation === 'retry' && retryable(job)) {
      const lock = await store.acquire(request.id);
      if (lock) {
        try {
          job = await store.read(request.id);
          if (retryable(job) && job.attempts < 3 && now() - job.updatedAt >= 30000) {
            job = { ...job, status: 'queued', error: undefined, updatedAt: now() };
            await lock.write(job);
          }
        } finally { await lock.release(); }
      }
      if (job.status === 'queued') await enqueue(job.id);
    }
    return response(job);
  }
  async function work(id) {
    if (!validId(id)) return;
    const lock = await store.acquire(id);
    if (!lock) {
      const existing = await store.read(id);
      if (!existing || existing.status === 'ready' || existing.status === 'failed') return;
      // A crashed worker may leave its lease alive until expiry. Do not acknowledge
      // its redelivered queue message while the lease is still busy.
      throw new Error('JOB_BUSY');
    }
    try {
      let job = await store.read(id);
      if (!job || job.status === 'ready' || job.status === 'failed') return;
      if (job.attempts >= 3) { await lock.write({ ...job, status: 'failed', error: 'ATTEMPT_LIMIT', updatedAt: now() }); return; }
      job = { ...job, status: 'running', attempts: job.attempts + 1, updatedAt: now() };
      await lock.write(job);
      try {
        const result = await generate(job.kind, job.input);
        await lock.write({ ...job, status: 'ready', result, updatedAt: now() });
      } catch (_) {
        // Persist only a stable category. Provider payloads may contain secrets/URLs.
        await lock.write({ ...job, status: 'failed', error: 'GENERATION_FAILED', updatedAt: now() });
      }
    } finally { await lock.release(); }
  }
  return { handle, work };
}
module.exports = { VERSION, normalize, validId, createService };
