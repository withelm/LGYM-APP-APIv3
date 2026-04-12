using FluentAssertions;
using LgymApi.Api;
using LgymApi.Api.Mapping.Profiles;
using LgymApi.Application.Mapping;
using LgymApi.Application.Mapping.Core;
using Microsoft.Extensions.DependencyInjection;

namespace LgymApi.UnitTests;

[TestFixture]
public sealed class MapperConfigurationTests
{
    [Test]
    public void Mapper_Should_Load_All_Profiles_And_Validate()
    {
        var services = new ServiceCollection();
        services.AddApplicationMapping(typeof(Program).Assembly, typeof(IMappingProfile).Assembly);

        using var provider = services.BuildServiceProvider();
        var mapper = provider.GetRequiredService<IMapper>();

        mapper.Should().NotBeNull();

        var concrete = mapper as Mapper;
        concrete.Should().NotBeNull("Mapper should be concrete implementation for validation");

        concrete!.RegisteredMappings.Count.Should().BeGreaterThan(0, "No mappings registered");
        var action = () => concrete.ValidateMappings();
        action.Should().NotThrow("Mapping validation failed");
    }

    [Test]
    public void MappingContext_Should_Reject_Unknown_Key()
    {
        var services = new ServiceCollection();
        services.AddApplicationMapping(typeof(Program).Assembly, typeof(IMappingProfile).Assembly);

        using var provider = services.BuildServiceProvider();
        var mapper = provider.GetRequiredService<IMapper>();

        var context = mapper.CreateContext();

        var action = () => context.Set(new ContextKey<string>("Unknown.Key"), "value");
        action.Should().Throw<InvalidOperationException>();
    }

    [Test]
    public void Duplicate_ContextKeys_Should_Throw()
    {
        var configuration = new MappingConfiguration();
        var key = new ContextKey<string>("Duplicate.Key");

        configuration.AllowContextKey(key);

        var action = () => configuration.AllowContextKey(key);
        action.Should().Throw<InvalidOperationException>();
    }
}
