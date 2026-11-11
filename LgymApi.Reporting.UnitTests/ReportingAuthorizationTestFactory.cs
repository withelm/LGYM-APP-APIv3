using LgymApi.Application.Reporting.Authorization;
using LgymApi.Authorization;
using LgymApi.Authorization.Contracts;

namespace LgymApi.UnitTests;

internal static class ReportingAuthorizationTestFactory
{
    public static IReportingAuthorizationCoordinator Create()
    {
        var policy = new ReportingAuthorizationPolicy();
        var evaluator = new AuthorizationPolicyEvaluator<ReportingAuthorizationContext>([policy]);
        return new ReportingAuthorizationCoordinator(evaluator);
    }
}
