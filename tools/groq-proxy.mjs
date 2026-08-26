import http from "node:http";

const targetOrigin = "https://api.groq.com";
const listenPort = Number(process.env.GROQ_PROXY_PORT || 8787);

const server = http.createServer(async (request, response) => {
  try {
    const targetUrl = new URL(request.url || "/", targetOrigin);
    const chunks = [];

    for await (const chunk of request) {
      chunks.push(chunk);
    }

    const headers = new Headers();
    for (const [name, value] of Object.entries(request.headers)) {
      if (value === undefined) {
        continue;
      }

      if (name.toLowerCase() === "host" || name.toLowerCase() === "content-length") {
        continue;
      }

      headers.set(name, Array.isArray(value) ? value.join(",") : value);
    }

    const upstream = await fetch(targetUrl, {
      method: request.method,
      headers,
      body: chunks.length === 0 ? undefined : Buffer.concat(chunks)
    });

    response.statusCode = upstream.status;
    for (const [name, value] of upstream.headers) {
      response.setHeader(name, value);
    }

    if (upstream.body) {
      for await (const chunk of upstream.body) {
        response.write(chunk);
      }
    }

    response.end();
  } catch (error) {
    response.statusCode = 502;
    response.setHeader("content-type", "application/json");
    response.end(JSON.stringify({
      error: "groq_proxy_error",
      message: error instanceof Error ? error.message : String(error)
    }));
  }
});

server.listen(listenPort, "127.0.0.1", () => {
  console.log(`Groq proxy listening on http://localhost:${listenPort}`);
});
