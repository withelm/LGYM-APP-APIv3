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

        Assert.Multiple(() =>
        {
            Assert.That(mapper, Is.Not.Null);

            var concrete = mapper as Mapper;
            Assert.That(concrete, Is.Not.Null, "Mapper should be concrete implementation for validation");

            Assert.That(concrete!.RegisteredMappings.Count, Is.GreaterThan(0), "No mappings registered");
            Assert.DoesNotThrow(concrete.ValidateMappings, "Mapping validation failed");
        });
    }

    [Test]
    public void MappingContext_Should_Reject_Unknown_Key()
    {
        var services = new ServiceCollection();
        services.AddApplicationMapping(typeof(Program).Assembly, typeof(IMappingProfile).Assembly);

        using var provider = services.BuildServiceProvider();
        var mapper = provider.GetRequiredService<IMapper>();

        var context = mapper.CreateContext();

        Assert.Throws<InvalidOperationException>(() => context.Set(new ContextKey<string>("Unknown.Key"), "value"));
    }

    [Test]
    public void Duplicate_ContextKeys_Should_Throw()
    {
        var configuration = new MappingConfiguration();
        var key = new ContextKey<string>("Duplicate.Key");

        configuration.AllowContextKey(key);

        Assert.Throws<InvalidOperationException>(() => configuration.AllowContextKey(key));
    }
}
