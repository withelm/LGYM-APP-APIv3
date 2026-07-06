# LgymApi.Resources.Generator.csproj

- Purpose: Roslyn source generator and analyzer used by `LgymApi.Resources`.
- Contains: build-time generator code targeting `netstandard2.0` for analyzer compatibility.
- Rules: keep it deterministic and free of runtime app dependencies.
- Boundary: do not couple it to application runtime services.
