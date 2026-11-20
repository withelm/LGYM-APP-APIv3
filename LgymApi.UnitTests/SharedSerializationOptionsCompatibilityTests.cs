using LgymApi.Application.Platform.Contracts.Serialization;
using LgymApi.Domain.Entities;
using LgymApi.Domain.ValueObjects;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace LgymApi.UnitTests;

[TestFixture]
public sealed class SharedSerializationOptionsCompatibilityTests
{
    [Test]
public void Current_Is_Owned_By_Platform()
    {
        var owner = typeof(SharedSerializationOptions);

        Assert.Multiple(() =>
        {
            Assert.That(owner.Namespace, Is.EqualTo("LgymApi.Application.Platform.Contracts.Serialization"));
Assert.That(owner.Assembly.GetName().Name, Is.EqualTo("LgymApi.Platform"));
        });
    }

    [Test]
    public void Current_Preserves_Persisted_Contract_Option_Values()
    {
        var options = SharedSerializationOptions.Current;

        Assert.Multiple(() =>
        {
            Assert.That(options.PropertyNamingPolicy, Is.EqualTo(JsonNamingPolicy.CamelCase));
            Assert.That(options.DictionaryKeyPolicy, Is.EqualTo(JsonNamingPolicy.CamelCase));
            Assert.That(options.DefaultIgnoreCondition, Is.EqualTo(JsonIgnoreCondition.WhenWritingNull));
            Assert.That(options.PropertyNameCaseInsensitive, Is.True);
        });
    }

    [Test]
    public void Current_Registers_TypedId_Converter_Before_String_Enum_Converter()
    {
        var converters = SharedSerializationOptions.Current.Converters;

        Assert.Multiple(() =>
        {
            Assert.That(converters, Has.Exactly(2).Items);
            Assert.That(converters[0], Is.TypeOf<TypedIdJsonConverterFactory>());
            Assert.That(converters[1], Is.TypeOf<JsonStringEnumConverter>());
        });
    }

    [Test]
    public void Current_Preserves_CamelCase_Null_Omission_TypedId_And_Enum_Compatibility()
    {
        var userId = ParseUserId("a1f5f3b3-8bc9-4ec4-aedc-8070c6438bb1");
        var payload = new PersistedPayload
        {
            UserId = userId,
            Values = new Dictionary<string, string> { ["LegacyKey"] = "value" },
            Status = PersistedStatus.Ready,
            OptionalValue = null
        };

        var json = JsonSerializer.Serialize(payload, SharedSerializationOptions.Current);
        var legacyJson = "{\"UserId\":\"" + userId + "\",\"Values\":{\"LegacyKey\":\"value\"},\"Status\":1,\"OptionalValue\":null}";
        var legacyPayload = JsonSerializer.Deserialize<PersistedPayload>(legacyJson, SharedSerializationOptions.Current);

        Assert.Multiple(() =>
        {
            Assert.That(json, Is.EqualTo("{\"userId\":\"" + userId + "\",\"values\":{\"legacyKey\":\"value\"},\"status\":\"Ready\"}"));
            Assert.That(legacyPayload, Is.Not.Null);
            Assert.That(legacyPayload!.UserId, Is.EqualTo(userId));
            var assertedValue153 = (legacyPayload.Values).ToArray();
            Assert.That(assertedValue153, Has.Exactly(1).Items);
            Assert.That(assertedValue153.Single(), Is.EqualTo(new KeyValuePair<string, string>("LegacyKey", "value")));
            Assert.That(legacyPayload.Status, Is.EqualTo(PersistedStatus.Ready));
            Assert.That(legacyPayload.OptionalValue, Is.Null);
        });
    }

    [Test]
    public void Current_Rejects_Malformed_TypedId_And_Enum_Values()
    {
        Action invalidTypedId = () => JsonSerializer.Deserialize<PersistedPayload>(
            "{\"userId\":\"not-a-guid\",\"status\":\"Ready\"}",
            SharedSerializationOptions.Current);
        Action invalidEnum = () => JsonSerializer.Deserialize<PersistedPayload>(
            "{\"userId\":\"a1f5f3b3-8bc9-4ec4-aedc-8070c6438bb1\",\"status\":\"UnknownLegacyStatus\"}",
            SharedSerializationOptions.Current);

        Assert.Multiple(() =>
        {
            Assert.That(() => { invalidTypedId(); }, Throws.InstanceOf<JsonException>());
            Assert.That(() => { invalidEnum(); }, Throws.InstanceOf<JsonException>());
        });
    }

    private static Id<User> ParseUserId(string value)
    {
        Assert.That(Id<User>.TryParse(value, out var userId), Is.True);
        return userId;
    }

    private sealed class PersistedPayload
    {
        public Id<User> UserId { get; init; }

        public Dictionary<string, string> Values { get; init; } = new();

        public PersistedStatus Status { get; init; }

        public string? OptionalValue { get; init; }
    }

    private enum PersistedStatus
    {
        Unknown = 0,
        Ready = 1
    }
}
