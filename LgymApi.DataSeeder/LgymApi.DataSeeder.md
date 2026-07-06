# LgymApi.DataSeeder.csproj

- Purpose: deterministic bootstrap and data seeding executable.
- Contains: infrastructure and EF tooling used to seed or initialize data.
- CI usage: the GitHub Actions DB-backed test job runs this project before integration tests to apply migrations and seed required rows.
- Rules: do not make API startup depend on this executable.
- Boundary: keep this as a console entrypoint, not a web host.
