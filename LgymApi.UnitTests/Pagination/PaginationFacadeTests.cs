using LgymApi.Application.BuildingBlocks.Errors;
using LgymApi.Application.BuildingBlocks.Results;
using LgymApi.Application.Pagination;
using NUnit.Framework;

namespace LgymApi.UnitTests;

[TestFixture]
public sealed class PaginationFacadeTests
{
    [Test]
    public void PaginationFacade_ExposesApplicationSafeContract()
    {
        var interfaceType = typeof(IQueryPaginationService);
        Assert.That(interfaceType.Namespace, Is.EqualTo("LgymApi.Application.Pagination"));

        var method = interfaceType.GetMethod(nameof(IQueryPaginationService.ExecuteAsync));
        Assert.That(method, Is.Not.Null);

        Assert.That(method!.ReturnType.IsGenericType, Is.True);
        Assert.That(method.ReturnType.GetGenericTypeDefinition(), Is.EqualTo(typeof(Task<>)));
        var resultType = method.ReturnType.GetGenericArguments()[0];
        Assert.That(resultType.GetGenericTypeDefinition(), Is.EqualTo(typeof(Result<,>)));
        Assert.That(resultType.GetGenericArguments()[0].GetGenericTypeDefinition(), Is.EqualTo(typeof(Pagination<>)));
        Assert.That(resultType.GetGenericArguments()[1], Is.EqualTo(typeof(AppError)));

        var genericArguments = method.GetGenericArguments();
        Assert.That(genericArguments, Has.Exactly(1).Items);

        var parameters = method.GetParameters();
        Assert.That(parameters, Has.Exactly(3).Items);
        Assert.That(parameters[0].ParameterType.GetGenericTypeDefinition(), Is.EqualTo(typeof(Func<>)));
        Assert.That(parameters[0].ParameterType.GetGenericArguments()[0].GetGenericTypeDefinition(), Is.EqualTo(typeof(IQueryable<>)));
        Assert.That(parameters[0].ParameterType.GetGenericArguments()[0].GetGenericArguments()[0], Is.EqualTo(genericArguments[0]));
        Assert.That(parameters[1].ParameterType, Is.EqualTo(typeof(FilterInput)));
        Assert.That(parameters[2].ParameterType, Is.EqualTo(typeof(CancellationToken)));

        Assert.That((typeof(LgymApi.Application.ServiceCollectionExtensions).Assembly.GetReferencedAssemblies()
            .Select(x => x.Name)).Any(name => name != null && name.Contains("Gridify", StringComparison.OrdinalIgnoreCase)), Is.False);
    }
}
