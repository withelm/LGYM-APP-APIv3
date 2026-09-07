using System.Reflection;
using NUnit.Framework;
using Reqnroll;

namespace LgymApi.ExternalE2ETests.Features;

[TestFixture]
[Category("ExternalSmokeContract")]
public sealed class ExternalEnvironmentFeatureBindingTests
{
    [Test]
    public void Test_external_environment_feature_when_generated_has_its_step_binding()
    {
        var binding = typeof(ExternalEnvironmentFeatureBindingTests).Assembly
            .GetTypes()
            .SingleOrDefault(type =>
                type.Name == "ExternalEnvironmentSmokeSteps" &&
                type.GetCustomAttribute<BindingAttribute>() is not null);

        Assert.That(binding, Is.Not.Null);
    }
}
