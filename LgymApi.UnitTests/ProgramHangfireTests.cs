using LgymApi.Api;
using Microsoft.AspNetCore.Builder;

namespace LgymApi.UnitTests;

[TestFixture]
public sealed class ProgramHangfireTests
{
    [Test]
    public void ConfigureRecurringJobs_WhenEnvironmentMatchesTesting_ReturnsWithoutThrowing()
    {
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions
        {
            EnvironmentName = "Testing"
        });
        using var app = builder.Build();

        var action = () => ProgramHangfire.ConfigureRecurringJobs(app, "Testing");

        Assert.That(() => { action(); }, Throws.Nothing);
    }

    [Test]
    public void ConfigureDashboard_WhenEnvironmentMatchesTesting_ReturnsWithoutThrowing()
    {
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions
        {
            EnvironmentName = "Testing"
        });
        using var app = builder.Build();

        var action = () => ProgramHangfire.ConfigureDashboard(app, "Testing");

        Assert.That(() => { action(); }, Throws.Nothing);
    }
}
