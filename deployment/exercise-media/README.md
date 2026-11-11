# Public exercise reads

`worker.mjs` is the runtime source for strict exercise-only GET/HEAD from the
configured private R2 bucket. It enforces the expected HTTPS host, immutable
server key shape, MIME, nosniff, cache/ETag handling, and safe provider errors.
It exposes no listing, writes, report photos or credentials.

This minimal runtime stays in the API's existing deployment directory. System
documentation links here; moving it would not reduce runtime logic.

The owner supplies an existing bucket and working route. `wrangler.toml` contains
placeholders only. Put actual local names/bindings in the ignored local config
as described by `.gitignore`; keep credentials outside Git. Set the backend's
`ExerciseMediaStorage:PublicBaseUrl` to the intended read origin. API upload uses
separate credentials from private Reporting photos and never creates the bucket.

Run `node --test deployment/exercise-media/worker.test.mjs` for synthetic runtime
coverage. No live deployment or Cloudflare changes are performed by this task.
See [the upload contract](../../docs/exercise-media.md) for configuration and acceptance.
