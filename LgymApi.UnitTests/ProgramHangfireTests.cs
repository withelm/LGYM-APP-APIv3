using FluentAssertions;
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

        action.Should().NotThrow();
    }
}
