import assert from "node:assert/strict";
import { test } from "node:test";
import worker from "./worker.mjs";

const host = "exercise-media-dev.test";
const digest = "a".repeat(64);
const path = `/exercises/06d1ebc9-e570-42f3-8b1d-6fe0425a1887/${digest}/animation.gif`;
const original = new Uint8Array([71, 73, 70, 56, 57, 97]);
function fixture() {
    const calls = [];
    return { calls, environment: { EXPECTED_HOST: host, EXERCISE_MEDIA: {
        get: async key => { calls.push(["get", key]); return { body: new Blob([original]).stream(), size: original.length }; },
        head: async key => { calls.push(["head", key]); return { size: original.length }; },
    } } };
}
const request = (route = path, options) => new Request(`https://${host}${route}`, options);

test("published GET serves exact original bytes with immutable type-safe headers", async () => {
    const { environment, calls } = fixture();
    const response = await worker.fetch(request(), environment);
    assert.equal(response.status, 200);
    assert.deepEqual(new Uint8Array(await response.arrayBuffer()), original);
    assert.equal(response.headers.get("Content-Type"), "image/gif");
    assert.match(response.headers.get("Cache-Control"), /immutable/);
    assert.equal(response.headers.get("X-Content-Type-Options"), "nosniff");
    assert.equal(response.headers.get("ETag"), `"${digest}"`);
    assert.deepEqual(calls, [["get", path.slice(1)]]);
});

test("HEAD avoids loading object body", async () => {
    const { environment, calls } = fixture();
    const response = await worker.fetch(request(path, { method: "HEAD" }), environment);
    assert.equal(response.status, 200);
    assert.equal((await response.arrayBuffer()).byteLength, 0);
    assert.deepEqual(calls, [["head", path.slice(1)]]);
});

test("staging, verified snapshots, report photos, listing and malformed keys never access binding", async () => {
    for (const route of ["/", "/staging/private/source", "/verified/private/hash", "/reports/photo.jpg", "/exercises/", path + "?list-type=2", path.replace(digest, "x".repeat(64)), path.replace("animation.gif", "image.svg"), path.replace("exercises", "%65xercises")]) {
        const { environment, calls } = fixture();
        assert.equal((await worker.fetch(request(route), environment)).status, 404, route);
        assert.equal(calls.length, 0);
    }
});

test("anonymous write/delete/list methods are rejected", async () => {
    for (const method of ["PUT", "POST", "DELETE", "PATCH", "OPTIONS"]) {
        const { environment, calls } = fixture();
        assert.equal((await worker.fetch(request(path, { method }), environment)).status, 405);
        assert.equal(calls.length, 0);
    }
});

test("alternate hostname and cleartext origin cannot expose storage", async () => {
    for (const url of [`https://alternate.example.com${path}`, `http://${host}${path}`, `https://${host}:8443${path}`]) {
        const { environment, calls } = fixture();
        assert.equal((await worker.fetch(new Request(url), environment)).status, 404);
        assert.equal(calls.length, 0);
    }
});

test("DEV Worker workers.dev fallback still requires its exact configured host", async () => {
    const { environment, calls } = fixture();
    environment.EXPECTED_HOST = "exercise-worker.operator.workers.dev";
    const response = await worker.fetch(new Request(`https://${environment.EXPECTED_HOST}${path}`), environment);
    assert.equal(response.status, 200);
    await response.body.cancel();
    assert.equal((await worker.fetch(request(), environment)).status, 404);
    assert.equal(calls.length, 1);
});

test("missing object and failed binding are not cached or exposed", async () => {
    const { environment } = fixture();
    environment.EXERCISE_MEDIA.get = async () => null;
    const missing = await worker.fetch(request(), environment);
    assert.equal(missing.status, 404);
    assert.equal(missing.headers.get("Cache-Control"), "no-store");
    delete environment.EXERCISE_MEDIA;
    const unavailable = await worker.fetch(request(), environment);
    assert.equal(unavailable.status, 503);
    assert.equal(await unavailable.text(), "");
});

test("conditional request is 304 only for an existing immutable object", async () => {
    const { environment } = fixture();
    const response = await worker.fetch(request(path, { headers: { "If-None-Match": `W/"${digest}"` } }), environment);
    assert.equal(response.status, 304);
    environment.EXERCISE_MEDIA.get = async () => null;
    assert.equal((await worker.fetch(request(), environment)).status, 404);
});

test("provider failure is retryable and contains no private error detail", async () => {
    const { environment } = fixture();
    environment.EXERCISE_MEDIA.get = async () => { throw new Error("sensitive-provider-url"); };
    const response = await worker.fetch(request(), environment);
    assert.equal(response.status, 503);
    assert.equal(await response.text(), "");
    assert.equal(response.headers.get("Cache-Control"), "no-store");
});

test("every accepted still format gets its correct MIME", async () => {
    for (const [extension, mime] of [["png", "image/png"], ["jpg", "image/jpeg"], ["webp", "image/webp"]]) {
        const { environment } = fixture();
        const response = await worker.fetch(request(path.replace("animation.gif", `image.${extension}`)), environment);
        assert.equal(response.status, 200);
        assert.equal(response.headers.get("Content-Type"), mime);
        await response.body.cancel();
    }
});

test("edge cache reuses immutable GET for GET/HEAD and never accepts writes", async () => {
    const previous = globalThis.caches;
    const entries = new Map();
    const background = [];
    globalThis.caches = { default: {
        match: async key => entries.get(key.url)?.clone(),
        put: async (key, value) => { entries.set(key.url, value); },
    } };
    try {
        const { environment, calls } = fixture();
        const context = { waitUntil: promise => background.push(promise) };
        const initial = await worker.fetch(request(), environment, context);
        await initial.arrayBuffer();
        await Promise.all(background);
        const cached = await worker.fetch(request(), environment, context);
        assert.equal(cached.status, 200);
        await cached.arrayBuffer();
        assert.equal((await worker.fetch(request(path, { method: "HEAD" }), environment, context)).status, 200);
        assert.equal((await worker.fetch(request(path, { method: "PUT" }), environment, context)).status, 405);
        assert.equal(calls.length, 1);
    } finally { globalThis.caches = previous; }
});
