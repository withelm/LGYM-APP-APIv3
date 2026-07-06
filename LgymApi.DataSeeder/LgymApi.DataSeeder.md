# LgymApi.DataSeeder.csproj

- Purpose: deterministic bootstrap and data seeding executable.
- Contains: infrastructure and EF tooling used to seed or initialize data.
- Rules: do not make API startup depend on this executable.
- Boundary: keep this as a console entrypoint, not a web host.
