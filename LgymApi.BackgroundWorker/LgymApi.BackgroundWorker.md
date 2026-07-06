# LgymApi.BackgroundWorker.csproj

- Purpose: Hangfire/background worker module.
- Contains: worker jobs and worker-side integration with Application and Infrastructure services.
- Rules: keep jobs idempotent where practical and register worker services in the worker module.
- Boundary: do not let the worker module become a second composition root for the API.
