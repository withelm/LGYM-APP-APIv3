using Microsoft.EntityFrameworkCore;
using System.Linq.Expressions;

namespace LgymApi.Infrastructure.Data.Conventions;

internal static class SoftDeleteFilterApplier
{
    public static void Apply(ModelBuilder modelBuilder)
    {
        foreach (var entityType in modelBuilder.Model.GetEntityTypes())
        {
            if (entityType.IsOwned())
            {
                continue;
            }

            var clrType = entityType.ClrType;
            if (!IsEntityBase(clrType))
            {
                continue;
            }

            var parameter = Expression.Parameter(clrType, "entity");
            var isDeletedProperty = Expression.Call(
                typeof(EF),
                nameof(EF.Property),
                new[] { typeof(bool) },
                parameter,
                Expression.Constant("IsDeleted"));
            var compareExpression = Expression.Equal(isDeletedProperty, Expression.Constant(false));
            var lambda = Expression.Lambda(compareExpression, parameter);

            modelBuilder.Entity(clrType).HasQueryFilter(lambda);
        }
    }

    private static bool IsEntityBase(Type type)
    {
        var baseType = type.BaseType;
        while (baseType != null)
        {
            if (baseType.IsGenericType && baseType.GetGenericTypeDefinition().Name.StartsWith("EntityBase"))
            {
                return true;
            }
            baseType = baseType.BaseType;
        }
        return false;
    }
}
