const { BlobServiceClient } = require("@azure/storage-blob");
const crypto = require("crypto");

function sha256Hex(input) {
  return crypto.createHash("sha256").update(input || "", "utf8").digest("hex");
}

function extractGeminiText(data) {
  if (!data) return "";
  if (typeof data.text === "string") return data.text;

  const candidates = data.candidates || [];
  if (candidates.length === 0) return "";

  const parts = candidates[0]?.content?.parts || [];
  return parts
    .map(part => (typeof part.text === "string" ? part.text : ""))
    .filter(Boolean)
    .join("")
    .trim();
}

function withTimeout(timeoutMs) {
  const controller = new AbortController();
  const id = setTimeout(() => controller.abort(), timeoutMs);
  return { signal: controller.signal, cancel: () => clearTimeout(id) };
}

module.exports = async function (context, req) {
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

  const model = (args.model || "gemini-2.5-flash-lite").trim();
  const timeoutMs = Math.max(1000, parseInt(args.timeoutMs || "20000", 10));
  const cacheKey = (args.cacheKey || "").trim();
  const storyContainer = process.env.STORY_CONTAINER_NAME || "ai-story-cache";

  const blobServiceClient = BlobServiceClient.fromConnectionString(connectionString);
  const containerClient = blobServiceClient.getContainerClient(storyContainer);
  await containerClient.createIfNotExists();

  if (cacheKey) {
    const storyBlob = containerClient.getBlockBlobClient(`${cacheKey}.json`);
    if (await storyBlob.exists()) {
      try {
        const buffer = await storyBlob.downloadToBuffer();
        const cached = JSON.parse(buffer.toString("utf8"));
        context.res = {
          status: 200,
          headers: { "Content-Type": "application/json" },
          body: { text: cached?.text || "", cacheKey, cached: true }
        };
        return;
      } catch (error) {
        context.log.warn(`[generateGeminiText] Cache read failed: ${error.message}`);
      }
    }
  }

  const requestBody = {
    contents: [{ role: "user", parts: [{ text: prompt }] }]
  };

  let response;
  let responseText;
  const { signal, cancel } = withTimeout(timeoutMs);
  try {
    response = await fetch(
      `https://generativelanguage.googleapis.com/v1beta/models/${model}:generateContent`,
      {
        method: "POST",
        headers: {
          "Content-Type": "application/json",
          "x-goog-api-key": apiKey
        },
        body: JSON.stringify(requestBody),
        signal
      }
    );
    responseText = await response.text();
  } catch (error) {
    context.res = { status: 502, body: `Request error: ${error.message}` };
    return;
  } finally {
    cancel();
  }


  if (!response.ok) {
    context.res = { status: response.status, body: responseText };
    return;
  }

  let data;
  try {
    data = JSON.parse(responseText);
  } catch (error) {
    context.res = { status: 500, body: `Parse error: ${error.message}` };
    return;
  }

  const answer = extractGeminiText(data);
  if (!answer) {
    context.res = { status: 500, body: "No text found in Gemini response" };
    return;
  }

  if (cacheKey) {
    const storyBlob = containerClient.getBlockBlobClient(`${cacheKey}.json`);
    const payload = {
      text: answer,
      model,
      promptHash: sha256Hex(prompt),
      createdAt: new Date().toISOString()
    };
    await storyBlob.uploadData(Buffer.from(JSON.stringify(payload), "utf8"), {
      blobHTTPHeaders: { blobContentType: "application/json" }
    });
  }

  context.res = {
    status: 200,
    headers: { "Content-Type": "application/json" },
    body: { text: answer, cacheKey, cached: false }
  };
};

module.exports.extractGeminiText = extractGeminiText;
