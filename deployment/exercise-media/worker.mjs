const publishedPath = /^\/exercises\/[0-9a-f]{8}-(?:[0-9a-f]{4}-){3}[0-9a-f]{12}\/([0-9a-f]{64})\/(animation\.gif|image\.(?:png|jpg|webp))$/;
const contentTypes = { gif: "image/gif", png: "image/png", jpg: "image/jpeg", webp: "image/webp" };
const isUnchanged = (request, etag) => request.headers.get("If-None-Match")?.split(",")
    .map(value => value.trim()).some(value => value === "*" || value === etag || value === `W/${etag}`);

/** Public reads of verified exercise objects only. No staging/report bucket binding. */
export default {
    async fetch(request, environment, context) {
        const url = new URL(request.url);
        const match = publishedPath.exec(url.pathname);
        if (url.protocol !== "https:" || url.hostname !== environment.EXPECTED_HOST || url.search || !match) {
            return new Response(null, { status: 404, headers: { "Cache-Control": "no-store" } });
        }
        if (request.method !== "GET" && request.method !== "HEAD") {
            return new Response(null, { status: 405, headers: { Allow: "GET, HEAD", "Cache-Control": "no-store" } });
        }
        const key = url.pathname.slice(1);
        const etag = `"${match[1]}"`;
        const cache = globalThis.caches?.default;
        const cacheKey = new Request(url.toString(), { method: "GET" });
        try {
            const cached = await cache?.match(cacheKey);
            if (cached) {
                if (isUnchanged(request, etag)) {
                    const headers = new Headers(cached.headers);
                    headers.delete("Content-Length");
                    void cached.body?.cancel().catch(() => undefined);
                    return new Response(null, { status: 304, headers });
                }
                if (request.method === "HEAD") {
                    void cached.body?.cancel().catch(() => undefined);
                    return new Response(null, { headers: cached.headers });
                }
                return cached;
            }
        } catch { /* A cache failure falls back to the private published-only origin. */ }
        try {
            const object = request.method === "HEAD"
                ? await environment.PUBLISHED_EXERCISE_MEDIA.head(key)
                : await environment.PUBLISHED_EXERCISE_MEDIA.get(key);
            if (!object) return new Response(null, { status: 404, headers: { "Cache-Control": "no-store" } });
            const headers = new Headers({
                "Content-Type": contentTypes[match[2].split(".").at(-1)],
                "Content-Length": String(object.size),
                "Cache-Control": "public, max-age=31536000, immutable",
                "X-Content-Type-Options": "nosniff",
                "Referrer-Policy": "no-referrer",
                "Access-Control-Allow-Origin": "*",
                ETag: etag,
            });
            if (isUnchanged(request, etag)) {
                headers.delete("Content-Length");
                if (object.body) await object.body.cancel();
                return new Response(null, { status: 304, headers });
            }
            const response = new Response(request.method === "HEAD" ? null : object.body, { headers });
            if (cache && request.method === "GET" && context?.waitUntil) {
                context.waitUntil(cache.put(cacheKey, response.clone()).catch(() => undefined));
            }
            return response;
        } catch {
            // Do not disclose provider errors, object contents, private keys or credentials.
            return new Response(null, { status: 503, headers: { "Cache-Control": "no-store", "Retry-After": "30" } });
        }
    },
};
