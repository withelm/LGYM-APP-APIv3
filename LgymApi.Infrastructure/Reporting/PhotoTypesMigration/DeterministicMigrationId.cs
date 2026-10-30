using System.Security.Cryptography;
using System.Text;
using LgymApi.Domain.ValueObjects;

namespace LgymApi.Infrastructure.Reporting.PhotoTypesMigration;

internal static class DeterministicMigrationId
{
    private const string NamespaceId = "5e21e11341bd47a595ea01bb483e57eb";

    public static Id<TEntity> For<TEntity>(string purpose, params string[] parts)
        => Create<TEntity>(purpose, parts);

    public static string Slot(string purpose, params string[] parts)
        => Create<MigrationReportPhotoSlot>(purpose, parts).ToString();

    private static Id<TEntity> Create<TEntity>(string purpose, IReadOnlyList<string> parts)
    {
        var input = string.Join('\n', new[] { NamespaceId, purpose }.Concat(parts));
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(input));
        var idBytes = bytes[..16];
        idBytes[6] = (byte)((idBytes[6] & 0x0F) | 0x50);
        idBytes[8] = (byte)((idBytes[8] & 0x3F) | 0x80);
        return Id<TEntity>.FromBytes(idBytes);
    }
}

internal sealed class MigrationReportPhotoSlot;
