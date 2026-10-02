'use strict';
module.exports = async (context, request) => {
  try {
    let timer;
    const deadline = new Promise((_, reject) => { timer = setTimeout(() => reject(new Error('STORAGE_DEADLINE')), 7000); });
    let body;
    try { body = await Promise.race([require('./azure').getService().handle(request), deadline]); }
    finally { clearTimeout(timer); }
    context.res = { status: 200, headers: { 'Content-Type': 'application/json' }, body };
  } catch (e) {
    const invalid = /^INVALID_/.test(e.message);
    context.res = { status: invalid ? 400 : 503, body: { protocol: 'pocket-story-jobs-v1', status: 'unavailable', error: invalid ? e.message : 'STORAGE_UNAVAILABLE' } };
  }
};
