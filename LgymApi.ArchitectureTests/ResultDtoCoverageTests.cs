using System.Reflection;
using System.Text.Json;
using LgymApi.Api.Interfaces;

namespace LgymApi.ArchitectureTests;

[TestFixture]
public sealed class ResultDtoCoverageTests
{
    [Test]
    public void ResultDto_Properties_ShouldBeReadableAndWritable()
    {
        var dtoTypes = typeof(IResultDto).Assembly
            .GetTypes()
            .Where(type => !type.IsAbstract)
            .Where(type => type.IsClass || (type.IsValueType && !type.IsEnum))
            .Where(type => typeof(IResultDto).IsAssignableFrom(type))
            .Where(type => !type.IsGenericTypeDefinition)
            .ToList();

        Assert.That(dtoTypes, Is.Not.Empty, "No IResultDto types found for coverage.");

        foreach (var dtoType in dtoTypes)
        {
            var instance = CreateInstance(dtoType);
            Assert.That(instance, Is.Not.Null, $"Unable to create instance of {dtoType.FullName}.");

            var properties = dtoType
                .GetProperties(BindingFlags.Instance | BindingFlags.Public)
                .Where(property => property.CanWrite)
                .ToList();

            foreach (var property in properties)
            {
                if (property.GetIndexParameters().Length > 0)
                {
                    continue;
                }

                var value = CreateValue(property.PropertyType, dtoType);
                if (value == null && property.PropertyType.IsValueType && Nullable.GetUnderlyingType(property.PropertyType) == null)
                {
                    value = Activator.CreateInstance(property.PropertyType);
                }

                try
                {
                    property.SetValue(instance, value);
                }
                catch (TargetException)
                {
                    continue;
                }
                catch (ArgumentException)
                {
                    continue;
                }

                _ = property.GetValue(instance);
            }

            _ = JsonSerializer.Serialize(instance, dtoType);
        }
    }

    private static object? CreateInstance(Type type)
    {
        if (type == typeof(string))
        {
            return "value";
        }

        if (type.IsEnum)
        {
            return Enum.GetValues(type).GetValue(0);
        }

        if (type.IsValueType)
        {
            return Activator.CreateInstance(type);
        }

        if (type.IsArray)
        {
            return Array.CreateInstance(type.GetElementType()!, 0);
        }

        var parameterlessConstructor = type.GetConstructor(Type.EmptyTypes);
        if (parameterlessConstructor != null)
        {
            return parameterlessConstructor.Invoke(null);
        }

        var constructor = type.GetConstructors(BindingFlags.Public | BindingFlags.Instance)
            .OrderBy(ctor => ctor.GetParameters().Length)
            .FirstOrDefault();

        if (constructor == null)
        {
            return null;
        }

        var parameters = constructor.GetParameters();
        var args = new object?[parameters.Length];
        for (var i = 0; i < parameters.Length; i++)
        {
            args[i] = CreateValue(parameters[i].ParameterType, type);
        }

        return constructor.Invoke(args);
    }

    private static object? CreateValue(Type type, Type ownerType)
    {
        if (type == typeof(string))
        {
            return "value";
        }

        if (type.IsEnum)
        {
            return Enum.GetValues(type).GetValue(0);
        }

        if (type.IsValueType)
        {
            return Activator.CreateInstance(type);
        }

        if (type == ownerType)
        {
            return null;
        }

        if (type.IsArray)
        {
            return Array.CreateInstance(type.GetElementType()!, 0);
        }

        var elementType = ExtractListElementType(type);
        if (elementType != null)
        {
            var listType = typeof(List<>).MakeGenericType(elementType);
            return Activator.CreateInstance(listType);
        }

        return CreateInstance(type);
    }

    private static Type? ExtractListElementType(Type type)
    {
        if (!type.IsGenericType)
        {
            return null;
        }

        var genericDefinition = type.GetGenericTypeDefinition();
        if (genericDefinition == typeof(List<>) || genericDefinition == typeof(IList<>) ||
            genericDefinition == typeof(ICollection<>) || genericDefinition == typeof(IEnumerable<>) ||
            genericDefinition == typeof(IReadOnlyList<>) || genericDefinition == typeof(IReadOnlyCollection<>))
        {
            return type.GetGenericArguments()[0];
        }

        return null;
    }
}
