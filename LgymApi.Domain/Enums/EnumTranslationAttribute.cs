namespace LgymApi.Domain.Enums;

[AttributeUsage(AttributeTargets.Field, AllowMultiple = false)]
public sealed class EnumTranslationAttribute(string resourceKey) : Attribute
{
    public string ResourceKey { get; } = resourceKey;
}
