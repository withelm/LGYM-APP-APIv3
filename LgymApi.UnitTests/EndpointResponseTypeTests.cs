using System.Reflection;
using System.Text.RegularExpressions;
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

    [Test]
    public void Controllers_ShouldNotConstructResponseDtosDirectly()
    {
        var repoRoot = ResolveRepositoryRoot();
        var controllersRoot = Path.Combine(repoRoot, "LgymApi.Api", "Features");

        Assert.That(
            Directory.Exists(controllersRoot),
            Is.True,
            $"Controllers root directory '{controllersRoot}' does not exist. Check the repository layout and controllersRoot path.");

        var controllerFiles = Directory
            .EnumerateFiles(controllersRoot, "*Controller.cs", SearchOption.AllDirectories)
            .ToList();

        Assert.That(
            controllerFiles,
            Is.Not.Empty,
            $"No controller files found in '{controllersRoot}'. Check the controllersRoot path and *Controller.cs naming.");

        var dtoConstructorPattern = new Regex(
            @"new\s+([A-Za-z_][A-Za-z0-9_]*\.)*[A-Za-z_][A-Za-z0-9_]*Dto\b",
            RegexOptions.Compiled);
        // This regex intentionally targets explicit constructions (`new SomeDto(...)`), including namespace-qualified names.
        // Detecting target-typed `new()` requires syntax-tree analysis and is outside this lightweight guard.
        var violations = new List<string>();

        foreach (var file in controllerFiles)
        {
            var fileText = File.ReadAllText(file);
            if (string.IsNullOrWhiteSpace(fileText))
            {
                continue;
            }

            var matches = dtoConstructorPattern.Matches(fileText);
            foreach (Match match in matches)
            {
                var lineNumber = GetLineNumber(fileText, match.Index);
                var lineText = GetLineText(fileText, lineNumber);
                var relativePath = Path.GetRelativePath(repoRoot, file);
                violations.Add($"{relativePath}:{lineNumber}: {lineText.Trim()}");
            }
        }

        Assert.That(
            violations,
            Is.Empty,
            "Controllers must use mapper profiles for response DTO mapping. Violations: " +
            string.Join(" | ", violations));
    }

    private static string ResolveRepositoryRoot()
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current != null)
        {
            var apiProjectDir = Path.Combine(current.FullName, "LgymApi.Api");
            if (Directory.Exists(apiProjectDir))
            {
                return current.FullName;
            }

            current = current.Parent;
        }

        throw new InvalidOperationException("Unable to locate repository root for architecture checks.");
    }

    private static int GetLineNumber(string text, int charIndex)
    {
        var line = 1;
        for (var i = 0; i < charIndex && i < text.Length; i++)
        {
            if (text[i] == '\n')
            {
                line++;
            }
        }

        return line;
    }

    private static string GetLineText(string text, int lineNumber)
    {
        var currentLine = 1;
        var lineStart = 0;

        for (var i = 0; i < text.Length; i++)
        {
            if (currentLine == lineNumber)
            {
                lineStart = i;
                break;
            }

            if (text[i] == '\n')
            {
                currentLine++;
            }
        }

        if (currentLine != lineNumber)
        {
            return string.Empty;
        }

        var lineEnd = text.IndexOf('\n', lineStart);
        if (lineEnd == -1)
        {
            lineEnd = text.Length;
        }

        return text.Substring(lineStart, lineEnd - lineStart);
    }
}
