using LgymApi.Api.Features.Account.Controllers;
using Microsoft.OpenApi;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace LgymApi.Api.Serialization;

public sealed class AccountAccessOperationFilter : IOperationFilter
{
    public void Apply(OpenApiOperation operation, OperationFilterContext context)
    {
        if (context.MethodInfo.DeclaringType != typeof(AccountController)
            || context.MethodInfo.Name != nameof(AccountController.GetAccess))
        {
            return;
        }

        operation.Summary = "Current account authorization capabilities (schema version 1)";
        operation.Description = "Self only: no account selector is accepted. Cache-Control: private, no-store. "
            + "Application booleans use current Identity policies. False denies the action; unavailable owner extensions "
            + "(available=false), missing fields and unsupported schema versions never authorize and must not fall back to roles. "
            + "Availability alone is not an action permission. Reporting protocol negotiation is a separate wire-format handshake. "
            + "Future resource capabilities belong only on already-authorized responses: forbidden objects are absent, not canRead=false. "
            + "Collection/create capabilities belong to owner collection-access contracts, never dummy records. "
            + "Capabilities are not grants: commands re-evaluate access and state. Existing resource 403/concealed 404 and "
            + "documented 409 state-conflict semantics remain unchanged; this self read introduces no resource conflict or new error code. "
            + "Missing/invalid/expired sessions retain existing localized 401 msg/req errors; blocked accounts retain 403. "
            + "Errors never expose internal denial reasons or foreign resource existence.";
    }
}
