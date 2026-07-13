using FluentAssertions;
using LgymApi.Api.Logging;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Serilog;
using NUnit.Framework;
using System.Collections;
using System.Reflection;

namespace LgymApi.UnitTests;

[TestFixture]
[NonParallelizable]
public sealed class SerilogBootstrapTests
{
    [SetUp]
    public void SetUp()
    {
        Log.Logger = new LoggerConfiguration().CreateLogger();
    }

    [TearDown]
    public void TearDown()
    {
        Log.CloseAndFlush();
    }

    [TestCase(null)]
    [TestCase("")]
    [TestCase("   ")]
    public void ConfigureSerilog_WhenElasticsearchUrlIsMissing_SkipsElasticsearchSink(string? elasticsearchUrl)
    {
        var builder = CreateBuilder(elasticsearchUrl);

        SerilogBootstrap.ConfigureSerilog(builder);

        ContainsTypeName(Log.Logger, "Elastic").Should().BeFalse();
        ContainsTypeName(Log.Logger, "Elasticsearch").Should().BeFalse();
    }

    [Test]
    public void ConfigureSerilog_WhenElasticsearchUrlIsConfigured_WiresElasticsearchSink()
    {
        var builder = CreateBuilder("http://localhost:9200");

        SerilogBootstrap.ConfigureSerilog(builder);

        ContainsTypeName(Log.Logger, "Elastic").Should().BeTrue();
        ContainsTypeName(Log.Logger, "Elasticsearch").Should().BeTrue();
    }

    private static WebApplicationBuilder CreateBuilder(string? elasticsearchUrl)
    {
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions
        {
            EnvironmentName = "Testing"
        });

        if (elasticsearchUrl is not null)
        {
            builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Elasticsearch:Url"] = elasticsearchUrl
            });
        }

        return builder;
    }

    private static bool ContainsTypeName(object? value, string typeNameFragment)
    {
        return ContainsTypeName(value, typeNameFragment, new HashSet<object>(ReferenceEqualityComparer.Instance));
    }

    private static bool ContainsTypeName(object? value, string typeNameFragment, HashSet<object> visited)
    {
        if (value is null)
        {
            return false;
        }

        if (value is string text)
        {
            return text.Contains(typeNameFragment, StringComparison.OrdinalIgnoreCase);
        }

        var type = value.GetType();
        if (type.Name.Contains(typeNameFragment, StringComparison.OrdinalIgnoreCase) ||
            (type.FullName?.Contains(typeNameFragment, StringComparison.OrdinalIgnoreCase) ?? false))
        {
            return true;
        }

        if (type.IsPrimitive ||
            type.IsEnum ||
            type == typeof(string) ||
            type == typeof(decimal) ||
            (type.Namespace == "System" && type.IsValueType && !type.IsGenericType))
        {
            return false;
        }

        if (!visited.Add(value))
        {
            return false;
        }

        if (value is IEnumerable enumerable)
        {
            foreach (var item in enumerable)
            {
                if (ContainsTypeName(item, typeNameFragment, visited))
                {
                    return true;
                }
            }
        }

        for (var current = type; current is not null && current != typeof(object); current = current.BaseType)
        {
            foreach (var field in current.GetFields(BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.DeclaredOnly))
            {
                if (ContainsTypeName(field.GetValue(value), typeNameFragment, visited))
                {
                    return true;
                }
            }
        }

        return false;
    }
}
