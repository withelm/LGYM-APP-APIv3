using System;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using LgymApi.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;

namespace LgymApi.Infrastructure.Extensions;

public static class ExecuteUpdateExtensions
{
    public static async Task<int> StageUpdateAsync<TSource, TProperty>(
        this IQueryable<TSource> source,
        DbContext dbContext,
        Expression<Func<TSource, TProperty>> propertySelector,
        Expression<Func<TSource, TProperty>> valueExpression,
        CancellationToken cancellationToken = default)
        where TSource : class
    {
        if (!CanUseSetBasedUpdate(dbContext))
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

        return await ExecuteSetBasedUpdateAsync(source, propertySelector, valueExpression, cancellationToken);
    }

    [Obsolete("Use StageUpdateAsync for UoW-friendly staged updates.")]
    public static Task<int> ExecuteUpdateAsync<TSource, TProperty>(
        this IQueryable<TSource> source,
        DbContext dbContext,
        Expression<Func<TSource, TProperty>> propertySelector,
        Expression<Func<TSource, TProperty>> valueExpression,
        CancellationToken cancellationToken = default)
        where TSource : class
    {
        return StageUpdateAsync(source, dbContext, propertySelector, valueExpression, cancellationToken);
    }

    public static Task<int> StageUpdateAsync<TSource, TProperty>(
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
                "DbContext is required for StageUpdateAsync fallback. Use overload with DbContext.");
        }

        return StageUpdateAsync(source, dbContext, propertySelector, valueExpression, cancellationToken);
    }

    [Obsolete("Use StageUpdateAsync for UoW-friendly staged updates.")]
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

        return StageUpdateAsync(source, dbContext, propertySelector, valueExpression, cancellationToken);
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

    private static bool CanUseSetBasedUpdate(DbContext dbContext)
    {
        var providerName = dbContext.Database.ProviderName;
        if (providerName?.Contains("InMemory", StringComparison.OrdinalIgnoreCase) == true)
        {
            return false;
        }

        return true;
    }

    private static Task<int> ExecuteSetBasedUpdateAsync<TSource, TProperty>(
        IQueryable<TSource> source,
        Expression<Func<TSource, TProperty>> propertySelector,
        Expression<Func<TSource, TProperty>> valueExpression,
        CancellationToken cancellationToken)
        where TSource : class
    {
        if (typeof(EntityBase).IsAssignableFrom(typeof(TSource))
            && !IsUpdatedAtProperty(propertySelector))
        {
            var utcNow = DateTimeOffset.UtcNow;
            var updatedAtPropertySelector = BuildUpdatedAtSelector<TSource>();
            return source.ExecuteUpdateAsync(
                setters => setters
                    .SetProperty(propertySelector, valueExpression)
                    .SetProperty(updatedAtPropertySelector, _ => utcNow),
                cancellationToken);
        }

        return source.ExecuteUpdateAsync(
            setters => setters.SetProperty(propertySelector, valueExpression),
            cancellationToken);
    }

    private static bool IsUpdatedAtProperty<TSource, TProperty>(Expression<Func<TSource, TProperty>> propertySelector)
    {
        return GetPropertyInfo(propertySelector).Name == nameof(EntityBase.UpdatedAt);
    }

    private static Expression<Func<TSource, DateTimeOffset>> BuildUpdatedAtSelector<TSource>()
    {
        var entityParameter = Expression.Parameter(typeof(TSource), "entity");
        var updatedAtProperty = Expression.Property(entityParameter, nameof(EntityBase.UpdatedAt));
        return Expression.Lambda<Func<TSource, DateTimeOffset>>(updatedAtProperty, entityParameter);
    }
}
