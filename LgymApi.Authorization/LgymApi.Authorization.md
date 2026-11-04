# LgymApi.Authorization.csproj

- Purpose: framework-neutral authorization kernel for immutable actor and decision contracts, typed policy evaluation, and closed policy registration.
- Contains: `AuthorizationActor`, `AuthorizationDecision`, `IAuthorizationPolicy<TContext>`, `IAuthorizationEvaluator<TContext>`, the fail-closed evaluator, and the closed registration extension.
- Rules: keep direct references limited to Domain and Platform. Do not add HTTP, persistence, feature types, resource dispatch, reflection dispatch, service location, user-facing errors, or generic role/admin/system bypasses.
- Registration: owners register each concrete `TContext`/`TPolicy` pair through `AddAuthorizationPolicy<TContext, TPolicy>()`; duplicate contexts fail during composition and an evaluator without a registered policy denies.
- Boundary: Identity and API consumers belong to AUTH-K03. This project is published as a standalone container artifact without an API project-reference edge.
