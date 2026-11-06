# LgymApi.Authorization.csproj

- Purpose: framework-neutral authorization kernel for immutable actor and decision contracts, typed policy evaluation, and closed policy registration.
- Contains: `AuthorizationActor`, `AuthorizationDecision`, `IAuthorizationPolicy<TContext>`, `IAuthorizationEvaluator<TContext>`, the fail-closed evaluator, and the closed registration extension.
- Rules: keep direct references limited to Domain and Platform. Do not add HTTP, persistence, feature types, resource dispatch, reflection dispatch, service location, user-facing errors, or generic role/admin/system bypasses.
- Registration: owners register each concrete `TContext`/`TPolicy` pair through `AddAuthorizationPolicy<TContext, TPolicy>()`; duplicate contexts fail during composition and an evaluator without a registered policy denies.
- Boundary: Identity owns AUTH-K03 adoption through its actor bridge and global permission policy; API remains the ASP.NET authorization adapter. This project is published as a standalone container artifact without an API project-reference edge.
