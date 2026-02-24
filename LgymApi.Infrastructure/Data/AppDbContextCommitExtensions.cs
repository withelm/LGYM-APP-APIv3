using Microsoft.EntityFrameworkCore;

namespace LgymApi.Infrastructure.Data;

public static class AppDbContextCommitExtensions
{
    public static Task CommitChangesAsync(this DbContext context, CancellationToken cancellationToken = default)
    {
        return context.SaveChangesAsync(cancellationToken);
    }
}
