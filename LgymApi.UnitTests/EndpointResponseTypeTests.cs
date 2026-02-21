using System.Reflection;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Routing;
using LgymApi.Api;

namespace LgymApi.UnitTests;

[TestFixture]
public sealed class EndpointResponseTypeTests
{
    [Test]
    public void AllEndpoints_ShouldUseOnlyHttpGetOrHttpPost()
    {
        var assembly = typeof(Program).Assembly;
        var controllerTypes = assembly
            .GetTypes()
            .Where(type => type.IsClass && !type.IsAbstract)
            .Where(type => typeof(ControllerBase).IsAssignableFrom(type))
            .Where(type => type.GetCustomAttribute<ApiControllerAttribute>() != null)
            .ToList();

        var invalid = new List<string>();

        foreach (var controller in controllerTypes)
        {
            var methods = controller
                .GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly)
                .Where(method => method.GetCustomAttributes<HttpMethodAttribute>(inherit: true).Any())
                .ToList();

            foreach (var method in methods)
            {
                var httpMethodAttributes = method.GetCustomAttributes<HttpMethodAttribute>(inherit: true).ToList();
                var hasInvalidMethod = httpMethodAttributes.Any(attribute =>
                    attribute is not HttpGetAttribute &&
                    attribute is not HttpPostAttribute);

                if (hasInvalidMethod)
                {
                    invalid.Add($"{controller.FullName}.{method.Name}");
                }
            }
        }

        Assert.That(
            invalid,
            Is.Empty,
            "Endpoints must use only HttpGet or HttpPost: " + string.Join(", ", invalid));
    }

    [Test]
    public void AllEndpoints_ShouldDeclareProducesResponseType200()
    {
        var assembly = typeof(Program).Assembly;
        var controllerTypes = assembly
            .GetTypes()
            .Where(type => type.IsClass && !type.IsAbstract)
            .Where(type => typeof(ControllerBase).IsAssignableFrom(type))
            .Where(type => type.GetCustomAttribute<ApiControllerAttribute>() != null)
            .ToList();

        var missing = new List<string>();

        foreach (var controller in controllerTypes)
        {
            var methods = controller
                .GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly)
                .Where(method => method.GetCustomAttributes<HttpMethodAttribute>(inherit: true).Any())
                .ToList();

            foreach (var method in methods)
            {
                var attributes = method.GetCustomAttributes<ProducesResponseTypeAttribute>(inherit: true);
                var hasExpected = attributes.Any(attribute =>
                    attribute.StatusCode == StatusCodes.Status200OK ||
                    attribute.StatusCode == StatusCodes.Status201Created);

                if (!hasExpected)
                {
                    missing.Add($"{controller.FullName}.{method.Name}");
                }
            }
        }

        Assert.That(
            missing,
            Is.Empty,
            "Endpoints missing ProducesResponseType(StatusCodes.Status200OK or StatusCodes.Status201Created): " +
            string.Join(", ", missing));
    }

}
