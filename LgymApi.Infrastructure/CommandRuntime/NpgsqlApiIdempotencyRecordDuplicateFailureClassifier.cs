using LgymApi.Platform.Persistence;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace LgymApi.Infrastructure.CommandRuntime;

internal sealed class NpgsqlApiIdempotencyRecordDuplicateFailureClassifier
    : IApiIdempotencyRecordDuplicateFailureClassifier
{
    public bool IsDuplicateScopeAndKeyFailure(Exception commitFailure)
    {
        return commitFailure is DbUpdateException
        {
            InnerException: PostgresException postgres
        }
        && postgres.SqlState == PostgresErrorCodes.UniqueViolation
        && postgres.ConstraintName == "IX_ApiIdempotencyRecords_ScopeTuple_IdempotencyKey";
    }
}
