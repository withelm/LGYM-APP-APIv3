# LgymApi.BackgroundWorker.Common.csproj

- Purpose: exact bounded Worker/Infrastructure seam for persisted jobs and email wire contracts.
- Contains: nine persisted job interfaces, two scheduler bridges, the idempotency policy, email interfaces/message, and six email payloads.
- Rules: preserve this closed surface and its typed-ID job signatures. Do not add HTTP/controller code, Application-facing contracts, commands, serialization, or push delivery types.
- Boundary: this project references Domain only. Application does not reference it.
- The unchanged job interfaces, including `IPushNotificationJob`, and their scheduler expression targets retain their persisted Hangfire identities. The Worker owns implementations.
