# LgymApi.Api.csproj

- Purpose: ASP.NET Core HTTP entrypoint.
- Contains: controllers, DTO contracts, validators, middleware, mapping profiles, auth, JSON setup, Swagger, CORS, rate limits, SignalR, and composition root.
- Rules: keep controllers thin and preserve legacy payload shapes.
- Boundary: do not move application or infrastructure business logic here.
