using FluentAssertions;
using System.Reflection;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Routing;
using LgymApi.Api;
using LgymApi.Api.Features.Exercise.Controllers;
using NUnit.Framework;

namespace LgymApi.UnitTests;

[TestFixture]
public sealed class EndpointResponseTypeTests
{
    [Test]
    public void Endpoints_UseLegacyVerbsOrTheExactReviewedExerciseMediaMutations()
    {
        var assembly = typeof(Program).Assembly;
        var controllerTypes = assembly
            .GetTypes()
            .Where(type => type.IsClass && !type.IsAbstract)
            .Where(type => typeof(ControllerBase).IsAssignableFrom(type))
            .Where(type => type.GetCustomAttribute<ApiControllerAttribute>() != null)
            .Where(IsPublicApiController)
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
                    attribute is not HttpPostAttribute &&
                    !IsApprovedExerciseMediaVerb(controller, method.Name, attribute));

                if (hasInvalidMethod)
                {
                    invalid.Add($"{controller.FullName}.{method.Name}");
                }
            }
        }

        invalid.Should().BeEmpty("Endpoints require GET/POST or the exact reviewed exercise-media PATCH: " + string.Join(", ", invalid));
    }

    private static bool IsApprovedExerciseMediaVerb(Type controller, string action, HttpMethodAttribute attribute) =>
        controller == typeof(ExerciseMediaController) &&
        action == nameof(ExerciseMediaController.ChangeExerciseMedia) && attribute is HttpPatchAttribute { Template: "{id}/media" };

    [Test]
    public void ExerciseMediaVerbApproval_RejectsOtherActionsRoutesAndControllers()
    {
        IsApprovedExerciseMediaVerb(typeof(ExerciseMediaController), nameof(ExerciseMediaController.CreateExerciseWithMedia), new HttpPatchAttribute("{id}/media")).Should().BeFalse();
        IsApprovedExerciseMediaVerb(typeof(ExerciseMediaController), nameof(ExerciseMediaController.ChangeExerciseMedia), new HttpPatchAttribute("other/{id}")).Should().BeFalse();
        IsApprovedExerciseMediaVerb(typeof(EndpointResponseTypeTests), nameof(ExerciseMediaController.ChangeExerciseMedia), new HttpPatchAttribute("{id}/media")).Should().BeFalse();
        IsApprovedExerciseMediaVerb(typeof(ExerciseMediaController), nameof(ExerciseMediaController.ChangeExerciseMedia), new HttpDeleteAttribute("{id}/media")).Should().BeFalse();
        typeof(ExerciseMediaController).GetMethod("CancelExerciseMedia").Should().BeNull();
        typeof(ExerciseMediaController).GetMethod(nameof(ExerciseMediaController.ChangeExerciseMedia))!
            .GetCustomAttributes<HttpMethodAttribute>().Should().ContainSingle().Which.Should().BeOfType<HttpPatchAttribute>();
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
            .Where(IsPublicApiController)
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

        missing.Should().BeEmpty("Endpoints missing ProducesResponseType(StatusCodes.Status200OK or StatusCodes.Status201Created): " +
            string.Join(", ", missing));
    }

    private static bool IsPublicApiController(Type controllerType)
    {
        var routeAttribute = controllerType.GetCustomAttribute<RouteAttribute>();
        if (string.IsNullOrWhiteSpace(routeAttribute?.Template))
        {
            return false;
        }

        return routeAttribute.Template.StartsWith("api", StringComparison.OrdinalIgnoreCase);
    }

}
