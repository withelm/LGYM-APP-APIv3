using LgymApi.Domain.Enums;

namespace LgymApi.Infrastructure.Reporting.PhotoTypesMigration;

internal static class LegacyPhotoTypeCatalog
{
    private static readonly IReadOnlyDictionary<PhotoViewType, MigrationPhotoTypeLabels> Labels =
        new Dictionary<PhotoViewType, MigrationPhotoTypeLabels>
        {
            [PhotoViewType.Front] = new("Przód", "Front"),
            [PhotoViewType.Side] = new("Bok", "Side"),
            [PhotoViewType.Back] = new("Tył", "Back"),
            [PhotoViewType.SideLeft] = new("Lewy bok", "Left side"),
            [PhotoViewType.SideRight] = new("Prawy bok", "Right side")
        };

    public static bool TryResolve(string? rawValue, out string systemKey, out MigrationPhotoTypeLabels labels)
    {
        systemKey = string.Empty;
        labels = new MigrationPhotoTypeLabels(string.Empty, string.Empty);
        if (string.IsNullOrWhiteSpace(rawValue)
            || !Enum.TryParse<PhotoViewType>(rawValue.Trim(), true, out var view)
            || !Enum.IsDefined(view))
        {
            return false;
        }

        systemKey = view.ToString();
        labels = Labels[view];
        return true;
    }

    public static MigrationPhotoTypeLabels CurrentLabels(string? systemKey, string name, PhotoTypeNameMode nameMode)
        => nameMode == PhotoTypeNameMode.SystemLocalized && TryResolve(systemKey, out _, out var labels)
            ? labels
            : new MigrationPhotoTypeLabels(name, name);
}
