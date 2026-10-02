'use strict';
module.exports = async (context, message) => {
  const payload = typeof message === 'string' ? JSON.parse(message) : message;
  await require('../storyJobs/azure').getService().work(payload?.id);
};
