using System;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;

namespace LgymApi.Infrastructure.Extensions;

public static class ExecuteUpdateExtensions
{
    public static async Task<int> ExecuteUpdateAsync<TSource, TProperty>(
        this IQueryable<TSource> source,
        DbContext dbContext,
        Expression<Func<TSource, TProperty>> propertySelector,
        Expression<Func<TSource, TProperty>> valueExpression,
        CancellationToken cancellationToken = default)
        where TSource : class
    {

        var entities = await source.ToListAsync(cancellationToken);
        var propertyInfo = GetPropertyInfo(propertySelector);
        var valueFunc = valueExpression.Compile();

        foreach (var entity in entities)
        {
            propertyInfo.SetValue(entity, valueFunc(entity));
        }

        return entities.Count;
    }

    public static Task<int> ExecuteUpdateAsync<TSource, TProperty>(
        this IQueryable<TSource> source,
        Expression<Func<TSource, TProperty>> propertySelector,
        Expression<Func<TSource, TProperty>> valueExpression,
        CancellationToken cancellationToken = default)
        where TSource : class
    {
        var dbContext = TryGetDbContext(source);
        if (dbContext == null)
        {
            throw new InvalidOperationException(
                "DbContext is required for ExecuteUpdateAsync fallback. Use overload with DbContext.");
        }

        return ExecuteUpdateAsync(source, dbContext, propertySelector, valueExpression, cancellationToken);
    }

    private static DbContext? TryGetDbContext<TSource>(IQueryable<TSource> source)
    {
        if (source is not IInfrastructure<IServiceProvider> infrastructure)
        {
            return null;
        }

        var currentDbContext = infrastructure.Instance.GetService<ICurrentDbContext>();
        return currentDbContext?.Context;
    }

    private static PropertyInfo GetPropertyInfo<TSource, TProperty>(
        Expression<Func<TSource, TProperty>> propertySelector)
    {
        var memberExpression = propertySelector.Body switch
        {
            MemberExpression member => member,
            UnaryExpression unary when unary.Operand is MemberExpression member => member,
            _ => null
        };

        if (memberExpression?.Member is PropertyInfo propertyInfo)
        {
            return propertyInfo;
        }

        throw new InvalidOperationException("Property selector must target a property.");
    }
}
