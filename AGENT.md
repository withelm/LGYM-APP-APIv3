# LGYM API - Agent Instructions

See `AGENTS.md` for the existing root AI context. This file was added because the request used the singular `AGENT.md` name.

## Mandatory csproj purpose rule

Every `.csproj` in the solution should have a documented purpose. When adding, renaming, deleting, or materially changing a `.csproj`, update the root agent instructions with what the project is for and review `LgymApi.sln`, project references, test commands, workflows, and `Directory.Packages.props`.
