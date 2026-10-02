const {
  BlobServiceClient,
  StorageSharedKeyCredential,
  BlobSASPermissions,
  generateBlobSASQueryParameters
} = require("@azure/storage-blob");
const crypto = require("crypto");

const DEFAULT_IMAGE_MODEL = "gemini-3.1-flash-image";
const INTERACTIONS_API_REVISION = "2026-05-20";
const IMAGE_MIME_TYPE = "image/jpeg";
const IMAGE_EXTENSION = "jpg";

function parseAccountFromConnectionString(connectionString) {
  let accountName = "";
  let accountKey = "";

  for (const part of connectionString.split(";")) {
    const separator = part.indexOf("=");
    if (separator < 0) continue;

    const key = part.slice(0, separator);
    const value = part.slice(separator + 1);
    if (key === "AccountName") accountName = value;
    if (key === "AccountKey") accountKey = value;
  }

  return { accountName, accountKey };
}

function extractInteractionImages(data) {
  const images = [];
  for (const step of data?.steps || []) {
    if (step?.type !== "model_output") continue;
    for (const content of step.content || []) {
      if (content?.type === "image" && content.data) {
        images.push({
          data: content.data,
          mimeType: content.mime_type || IMAGE_MIME_TYPE
        });
      }
    }
  }
  return images;
}

function resolveImageModel(requestedModel) {
  const model = (requestedModel || DEFAULT_IMAGE_MODEL).trim();
  if (/^imagen-4(?:\.|-|$)/i.test(model)) {
    return DEFAULT_IMAGE_MODEL;
  }
  return model;
}

function sha256Hex(input) {
  return crypto.createHash("sha256").update(input || "", "utf8").digest("hex");
}

function safeSegment(value, prefix) {
  const raw = (value || "").trim();
  if (!raw) return `${prefix}_unknown`;

  const safe = raw.replace(/[^a-zA-Z0-9._-]/g, "_");
  if (safe.length > 64 || /^_+$/.test(safe)) {
    return `${prefix}_${sha256Hex(raw).slice(0, 32)}`;
  }
  return safe;
}

function buildImageBlobNames(cacheKey, storyId, sceneIndex, count) {
  if (!cacheKey) {
    return Array.from({ length: count }, () => `${crypto.randomUUID()}.${IMAGE_EXTENSION}`);
  }

  const safeStory = safeSegment(storyId, "story");
  const safeScene = Number.isFinite(sceneIndex) ? `scene_${sceneIndex}` : "scene_0";
  const safeKey = safeSegment(cacheKey, "img");

  if (count <= 1) {
    return [`${safeStory}/${safeScene}/${safeKey}.${IMAGE_EXTENSION}`];
  }

  return Array.from(
    { length: count },
    (_, index) => `${safeStory}/${safeScene}/${safeKey}_${index + 1}.${IMAGE_EXTENSION}`
  );
}

function buildStoryIndexBlobName(storyId) {
  return `${safeSegment(storyId, "story")}.json`;
}

function makeSasUrl(blockBlobClient, sharedKeyCredential, containerName, blobName, hours) {
  const expiresOn = new Date();
  expiresOn.setHours(expiresOn.getHours() + hours);

  const sas = generateBlobSASQueryParameters(
    {
      containerName,
      blobName,
      permissions: BlobSASPermissions.parse("r"),
      expiresOn
    },
    sharedKeyCredential
  ).toString();

  return `${blockBlobClient.url}?${sas}`;
}

async function upsertStoryIndex(indexContainerClient, storyId, entry) {
  if (!storyId) return;

  const blobClient = indexContainerClient.getBlockBlobClient(buildStoryIndexBlobName(storyId));
  for (let attempt = 0; attempt < 3; attempt++) {
    let payload = { storyId, images: [], updatedAt: new Date().toISOString() };
    let etag = null;

    try {
      const properties = await blobClient.getProperties();
      etag = properties.etag;
      const buffer = await blobClient.downloadToBuffer();
      const parsed = JSON.parse(buffer.toString("utf8"));
      if (parsed && typeof parsed === "object") payload = parsed;
    } catch (error) {
      if (error.statusCode !== 404) throw error;
    }

    if (!Array.isArray(payload.images)) payload.images = [];
    const exists = payload.images.some(
      item => item && item.cacheKey === entry.cacheKey && item.sceneIndex === entry.sceneIndex
    );
    if (!exists) payload.images.push(entry);
    payload.storyId = storyId;
    payload.updatedAt = new Date().toISOString();

    const data = Buffer.from(JSON.stringify(payload), "utf8");
    try {
      const options = {
        blobHTTPHeaders: { blobContentType: "application/json" },
        conditions: etag ? { ifMatch: etag } : { ifNoneMatch: "*" }
      };
      await blobClient.uploadData(data, options);
      return;
    } catch (error) {
      if (error.statusCode === 409 || error.statusCode === 412) continue;
      throw error;
    }
  }
}

function withTimeout(timeoutMs) {
  const controller = new AbortController();
  const id = setTimeout(() => controller.abort(), timeoutMs);
  return { signal: controller.signal, cancel: () => clearTimeout(id) };
}

async function generateImage(apiKey, model, prompt, aspectRatio, timeoutMs) {
  const { signal, cancel } = withTimeout(timeoutMs);
  let response;
  let responseText;

  try {
    response = await fetch("https://generativelanguage.googleapis.com/v1beta/interactions", {
      method: "POST",
      headers: {
        "Content-Type": "application/json",
        "x-goog-api-key": apiKey,
        "Api-Revision": INTERACTIONS_API_REVISION
      },
      body: JSON.stringify({
        model,
        input: prompt,
        response_format: {
          type: "image",
          mime_type: IMAGE_MIME_TYPE,
          aspect_ratio: aspectRatio
        }
      }),
      signal
    });
    responseText = await response.text();
  } catch (error) {
    throw new Error(`Request error: ${error.message}`);
  } finally {
    cancel();
  }


  if (!response.ok) {
    const error = new Error(responseText);
    error.status = response.status;
    throw error;
  }

  let data;
  try {
    data = JSON.parse(responseText);
  } catch (error) {
    throw new Error(`Parse error: ${error.message}`);
  }

  const images = extractInteractionImages(data);
  if (images.length === 0) {
    throw new Error("No image data found in Gemini interaction response");
  }
  return images[0];
}

module.exports = async function (context, req) {
  if (req.body?.FunctionArgument?.storyJob) {
    return require("../storyJobs/http")(context, req.body.FunctionArgument.storyJob);
  }
  const args = (req.body && req.body.FunctionArgument) || {};
  const apiKey = process.env.GEMINI_API_KEY;
  const connectionString = process.env.AZURE_STORAGE_CONNECTION_STRING;

  if (!apiKey || !connectionString) {
    context.res = { status: 500, body: "Server misconfiguration: missing env vars" };
    return;
  }

  const prompt = args.prompt;
  if (!prompt) {
    context.res = { status: 400, body: "Missing 'prompt' field" };
    return;
  }

  const containerName = process.env.IMAGE_CONTAINER_NAME || "ai-images";
  const storyIndexContainerName = process.env.STORY_INDEX_CONTAINER_NAME || "ai-story-index";
  const sasHours = Math.max(1, parseInt(process.env.IMAGE_SAS_HOURS || "24", 10));
  const imageModel = resolveImageModel(args.imageModel);
  const sampleCount = Math.min(4, Math.max(1, parseInt(args.sampleCount || "1", 10) || 1));
  const aspectRatio = (args.aspectRatio || "1:1").trim();
  const timeoutMs = Math.max(1000, parseInt(args.timeoutMs || "60000", 10));
  const cacheKey = (args.cacheKey || "").trim();
  const storyId = (args.storyId || "").trim();
  const parsedSceneIndex = Number.isFinite(args.sceneIndex)
    ? args.sceneIndex
    : parseInt(args.sceneIndex, 10);
  const sceneIndex = Number.isFinite(parsedSceneIndex) ? parsedSceneIndex : null;

  const blobServiceClient = BlobServiceClient.fromConnectionString(connectionString);
  const containerClient = blobServiceClient.getContainerClient(containerName);
  await containerClient.createIfNotExists();

  const { accountName, accountKey } = parseAccountFromConnectionString(connectionString);
  if (!accountName || !accountKey) {
    context.res = { status: 500, body: "Storage connection string is missing account credentials" };
    return;
  }
  const sharedKeyCredential = new StorageSharedKeyCredential(accountName, accountKey);
  const blobNames = buildImageBlobNames(cacheKey, storyId, sceneIndex, sampleCount);

  if (cacheKey) {
    const cachedResults = [];
    let allCached = true;
    for (const blobName of blobNames) {
      const blobClient = containerClient.getBlockBlobClient(blobName);
      if (await blobClient.exists()) {
        cachedResults.push({
          url: makeSasUrl(blobClient, sharedKeyCredential, containerName, blobName, sasHours),
          mimeType: IMAGE_MIME_TYPE
        });
      } else {
        allCached = false;
      }
    }

    if (allCached) {
      context.res = {
        status: 200,
        headers: { "Content-Type": "application/json" },
        body: { images: cachedResults, cached: true }
      };
      return;
    }
  }

  const indexContainerClient = blobServiceClient.getContainerClient(storyIndexContainerName);
  await indexContainerClient.createIfNotExists();
  const results = [];

  try {
    for (let index = 0; index < sampleCount; index++) {
      const generated = await generateImage(apiKey, imageModel, prompt, aspectRatio, timeoutMs);
      const blobName = blobNames[index];
      const blockBlobClient = containerClient.getBlockBlobClient(blobName);
      const buffer = Buffer.from(generated.data, "base64");

      await blockBlobClient.uploadData(buffer, {
        blobHTTPHeaders: { blobContentType: generated.mimeType }
      });

      results.push({
        url: makeSasUrl(blockBlobClient, sharedKeyCredential, containerName, blobName, sasHours),
        mimeType: generated.mimeType
      });

      if (cacheKey && storyId) {
        await upsertStoryIndex(indexContainerClient, storyId, {
          cacheKey,
          blobName,
          sceneIndex,
          model: imageModel,
          aspectRatio,
          promptHash: sha256Hex(prompt),
          createdAt: new Date().toISOString()
        });
      }
    }
  } catch (error) {
    context.log.error(`[generateGeminiImages] ${error.message}`);
    context.res = { status: error.status || 502, body: error.message };
    return;
  }

  context.res = {
    status: 200,
    headers: { "Content-Type": "application/json" },
    body: { images: results, cached: false }
  };
};

module.exports.extractInteractionImages = extractInteractionImages;
module.exports.parseAccountFromConnectionString = parseAccountFromConnectionString;
module.exports.resolveImageModel = resolveImageModel;
