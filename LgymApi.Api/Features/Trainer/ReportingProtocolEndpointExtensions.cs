using LgymApi.Api.Features.Trainer.Contracts;
using LgymApi.Api.Middleware;
using LgymApi.Domain.Security;

namespace LgymApi.Api.Features.Trainer;

public static class ReportingProtocolEndpointExtensions
{
    public static IEndpointRouteBuilder MapReportingProtocolCapabilities(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet(
                "/api/trainer/reporting/capabilities",
                static () => Results.Ok(CreateResponse()))
            .RequireAuthorization(AuthConstants.Policies.TrainerAccess)
            .Produces<ReportingCapabilitiesResponse>()
            .WithMetadata(new ReportingProtocolAttribute());

        endpoints.MapGet(
                "/api/trainee/reporting/capabilities",
                static () => Results.Ok(CreateResponse()))
            .RequireAuthorization()
            .Produces<ReportingCapabilitiesResponse>()
            .WithMetadata(new ReportingProtocolAttribute());

        return endpoints;
    }

    private static ReportingCapabilitiesResponse CreateResponse()
    {
        return new ReportingCapabilitiesResponse
        {
            SupportedProtocols = [1, 2],
            PhotoTypesEnabled = true
        };
    }
}
