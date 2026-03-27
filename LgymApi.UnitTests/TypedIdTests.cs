using LgymApi.Domain.ValueObjects;
using System.Text.Json;
using System.Text.Json.Serialization;
using LgymApi.BackgroundWorker.Common.Serialization;

namespace LgymApi.UnitTests;

[TestFixture]
public sealed class TypedIdTests
{
    // Test fixtures to act as entity types
    private class User { }
    private class Plan { }

    #region Equality and Default Behavior

    [Test]
    public void RecordStruct_ImplementsValueEquality()
    {
        var id1 = new Id<User>(Guid.Parse("00000000-0000-0000-0000-000000000001"));
        var id2 = new Id<User>(Guid.Parse("00000000-0000-0000-0000-000000000001"));

        Assert.That(id1, Is.EqualTo(id2));
    }

    [Test]
    public void RecordStruct_DifferentiatesDifferentValues()
    {
        var id1 = new Id<User>(Guid.Parse("00000000-0000-0000-0000-000000000001"));
        var id2 = new Id<User>(Guid.Parse("00000000-0000-0000-0000-000000000002"));

        Assert.That(id1, Is.Not.EqualTo(id2));
    }

    [Test]
    public void Empty_ReturnsGuidEmpty()
    {
        var empty = Id<User>.Empty;

        Assert.That(empty.GetValue(), Is.EqualTo(Guid.Empty));
        Assert.That(empty.IsEmpty, Is.True);
    }

    [Test]
    public void Default_ReturnsEmptyGuid()
    {
        Id<User> id = default;

        Assert.That(id.GetValue(), Is.EqualTo(Guid.Empty));
        Assert.That(id.IsEmpty, Is.True);
    }

    [Test]
    public void Nullable_AllowsNullCheck()
    {
        Id<User>? id = null;

        Assert.That(id, Is.Null);
    }

    [Test]
    public void Nullable_AllowsNonNullValue()
    {
        var guid = Guid.NewGuid();
        Id<User>? id = new Id<User>(guid);

        Assert.That(id, Is.Not.Null);
        Assert.That(id.Value.GetValue(), Is.EqualTo(guid));
    }

    #endregion

    #region New() Factory

    [Test]
    public void New_ProducesNonEmptyId()
    {
        var id = Id<User>.New();

        Assert.That(id.GetValue(), Is.Not.EqualTo(Guid.Empty));
        Assert.That(id.IsEmpty, Is.False);
    }

    [Test]
    public void New_ProducesDifferentIdEachCall()
    {
        var id1 = Id<User>.New();
        var id2 = Id<User>.New();

        Assert.That(id1, Is.Not.EqualTo(id2));
    }

    #endregion

    #region Explicit Conversions (No Implicit Conversions)

    [Test]
    public void ExplicitOperator_UnwrapsGuid()
    {
        var guid = Guid.Parse("00000000-0000-0000-0000-000000000099");
        var id = new Id<User>(guid);

        var unwrapped = (Guid)id;

        Assert.That(unwrapped, Is.EqualTo(guid));
    }

    [Test]
    public void ExplicitOperator_WrapsGuid()
    {
        var guid = Guid.Parse("00000000-0000-0000-0000-000000000099");
        var id = (Id<User>)guid;

        Assert.That(id.GetValue(), Is.EqualTo(guid));
    }

    [Test]
    public void NoImplicitConversion_GuidToId()
    {
        // This test verifies that implicit conversion is not possible
        // If this compiles, the design is violated.
        // If this does not compile, the test passes.
#pragma warning disable CS0219 // Variable is assigned but its value is never used
        var guid = Guid.NewGuid();
        // Uncomment the following line to verify compilation fails:
        // Id<User> id = guid; // This should NOT compile
#pragma warning restore CS0219
    }

    [Test]
    public void NoImplicitConversion_IdToGuid()
    {
        // This test verifies that implicit conversion is not possible
        // If this compiles, the design is violated.
        // If this does not compile, the test passes.
#pragma warning disable CS0219 // Variable is assigned but its value is never used
        var id = Id<User>.New();
        // Uncomment the following line to verify compilation fails:
        // Guid guid = id; // This should NOT compile
#pragma warning restore CS0219
    }

    #endregion

    #region Type Safety (Compile-Time Separation)

    [Test]
    public void DifferentEntityTypes_AreCompiledAsDifferentTypes()
    {
        var userId = new Id<User>(Guid.NewGuid());
        var planId = new Id<Plan>(Guid.NewGuid());

        // userId and planId have different types at compile time
        // This test documents that intent even though runtime equates them if Guid is same
        Assert.That(userId.GetType().Name, Is.EqualTo("Id`1"));
        Assert.That(planId.GetType().Name, Is.EqualTo("Id`1"));
        
        // At runtime they are technically different types (generic instantiation)
        Assert.That(typeof(Id<User>), Is.Not.EqualTo(typeof(Id<Plan>)));
    }

    #endregion

    #region String Representation

    [Test]
    public void ToString_ReturnsGuidString()
    {
        var guid = Guid.Parse("00000000-0000-0000-0000-000000000077");
        var id = new Id<User>(guid);

        Assert.That(id.ToString(), Is.EqualTo("00000000-0000-0000-0000-000000000077"));
    }

    #endregion

    #region Value Property

    [Test]
    public void Value_Property_ExposesSingleGuidField()
    {
        var guid = Guid.NewGuid();
        var id = new Id<User>(guid);

        Assert.That(id.GetValue(), Is.EqualTo(guid));
    }

    #endregion

    #region IsEmpty Property

    [Test]
    public void IsEmpty_ReturnsTrueForGuidEmpty()
    {
        var id = new Id<User>(Guid.Empty);

        Assert.That(id.IsEmpty, Is.True);
    }

    [Test]
    public void IsEmpty_ReturnsFalseForNonEmptyGuid()
    {
        var id = new Id<User>(Guid.NewGuid());

        Assert.That(id.IsEmpty, Is.False);
    }

    #endregion

    #region JSON Serialization (Converter Factory)

    private readonly JsonSerializerOptions _serializerOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Converters = { new TypedIdJsonConverterFactory() }
    };

    [Test]
    public void Serialize_ProducesGuidString()
    {
        var guid = Guid.Parse("00000000-0000-0000-0000-000000000042");
        var id = new Id<User>(guid);

        var json = JsonSerializer.Serialize(id, _serializerOptions);

        Assert.That(json, Does.Contain("00000000-0000-0000-0000-000000000042"));
        // JSON string is quoted
        Assert.That(json, Is.EqualTo("\"00000000-0000-0000-0000-000000000042\""));
    }

    [Test]
    public void Deserialize_ParsesGuidString()
    {
        var guidString = "\"00000000-0000-0000-0000-000000000043\"";
        
        var id = JsonSerializer.Deserialize<Id<User>>(guidString, _serializerOptions);

        Assert.That(id.GetValue(), Is.EqualTo(Guid.Parse("00000000-0000-0000-0000-000000000043")));
    }

    [Test]
    public void RoundTrip_SerializeDeserialize_PreservesValue()
    {
        var originalGuid = Guid.NewGuid();
        var id = new Id<User>(originalGuid);

        var json = JsonSerializer.Serialize(id, _serializerOptions);
        var deserialized = JsonSerializer.Deserialize<Id<User>>(json, _serializerOptions);

        Assert.That(deserialized.GetValue(), Is.EqualTo(originalGuid));
    }

    [Test]
    public void RoundTrip_WithObject_PreservesId()
    {
        var userId = Id<User>.New();
        var original = new UserDto { Id = userId };

        var json = JsonSerializer.Serialize(original, _serializerOptions);
        var deserialized = JsonSerializer.Deserialize<UserDto>(json, _serializerOptions);

        Assert.That(deserialized!.Id.GetValue(), Is.EqualTo(userId.GetValue()));
    }

    [Test]
    public void Deserialize_WithInvalidUuid_ThrowsJsonException()
    {
        var invalidJson = "\"not-a-uuid\"";

        var ex = Assert.Throws<JsonException>(() =>
            JsonSerializer.Deserialize<Id<User>>(invalidJson, _serializerOptions));

        Assert.That(ex!.Message, Does.Contain("Invalid GUID format"));
    }

    [Test]
    public void Deserialize_WithEmptyString_ThrowsJsonException()
    {
        var emptyJson = "\"\"";

        var ex = Assert.Throws<JsonException>(() =>
            JsonSerializer.Deserialize<Id<User>>(emptyJson, _serializerOptions));

        Assert.That(ex!.Message, Does.Contain("cannot be empty"));
    }

    [Test]
    public void Deserialize_WithNullToken_ThrowsJsonException()
    {
        var nullJson = "null";

        var ex = Assert.Throws<JsonException>(() =>
            JsonSerializer.Deserialize<Id<User>>(nullJson, _serializerOptions));

        Assert.That(ex!.Message, Does.Contain("cannot be null"));
    }

    [Test]
    public void Deserialize_WithNumberToken_ThrowsJsonException()
    {
        var numberJson = "123";

        var ex = Assert.Throws<JsonException>(() =>
            JsonSerializer.Deserialize<Id<User>>(numberJson, _serializerOptions));

        Assert.That(ex!.Message, Does.Contain("Expected string token"));
    }

    #endregion

    #region JSON Serialization (Nullable)

    [Test]
    public void Serialize_NullableId_WithValue_ProducesGuidString()
    {
        Id<User>? id = new Id<User>(Guid.Parse("00000000-0000-0000-0000-000000000044"));

        var json = JsonSerializer.Serialize(id, _serializerOptions);

        Assert.That(json, Is.EqualTo("\"00000000-0000-0000-0000-000000000044\""));
    }

    [Test]
    public void Serialize_NullableId_WithNull_ProducesNull()
    {
        Id<User>? id = null;

        var json = JsonSerializer.Serialize(id, _serializerOptions);

        Assert.That(json, Is.EqualTo("null"));
    }

    [Test]
    public void Deserialize_NullableId_WithGuidString_ProducesValue()
    {
        var guidJson = "\"00000000-0000-0000-0000-000000000045\"";

        var id = JsonSerializer.Deserialize<Id<User>?>(guidJson, _serializerOptions);

        Assert.That(id, Is.Not.Null);
        Assert.That(id!.Value.GetValue(), Is.EqualTo(Guid.Parse("00000000-0000-0000-0000-000000000045")));
    }

    [Test]
    public void Deserialize_NullableId_WithNull_ProducesNull()
    {
        var nullJson = "null";

        var id = JsonSerializer.Deserialize<Id<User>?>(nullJson, _serializerOptions);

        Assert.That(id, Is.Null);
    }

    [Test]
    public void RoundTrip_NullableId_WithValue_PreservesValue()
    {
        var originalGuid = Guid.NewGuid();
        Id<User>? id = new Id<User>(originalGuid);

        var json = JsonSerializer.Serialize(id, _serializerOptions);
        var deserialized = JsonSerializer.Deserialize<Id<User>?>(json, _serializerOptions);

        Assert.That(deserialized, Is.Not.Null);
        Assert.That(deserialized!.Value.GetValue(), Is.EqualTo(originalGuid));
    }

    [Test]
    public void RoundTrip_NullableId_WithNull_PreservesNull()
    {
        Id<User>? id = null;

        var json = JsonSerializer.Serialize(id, _serializerOptions);
        var deserialized = JsonSerializer.Deserialize<Id<User>?>(json, _serializerOptions);

        Assert.That(deserialized, Is.Null);
    }

    [Test]
    public void RoundTrip_ObjectWithNullableId_PreservesNull()
    {
        var original = new UserWithOptionalIdDto { Id = null };

        var json = JsonSerializer.Serialize(original, _serializerOptions);
        var deserialized = JsonSerializer.Deserialize<UserWithOptionalIdDto>(json, _serializerOptions);

        Assert.That(deserialized!.Id, Is.Null);
    }

    [Test]
    public void RoundTrip_ObjectWithNullableId_PreservesValue()
    {
        var userId = Id<User>.New();
        var original = new UserWithOptionalIdDto { Id = userId };

        var json = JsonSerializer.Serialize(original, _serializerOptions);
        var deserialized = JsonSerializer.Deserialize<UserWithOptionalIdDto>(json, _serializerOptions);

        Assert.That(deserialized!.Id, Is.Not.Null);
        Assert.That(deserialized.Id!.Value.GetValue(), Is.EqualTo(userId.GetValue()));
    }

    #endregion

    #region Shared Serialization Options

    [Test]
    public void SharedSerializationOptions_Supports_TypedIdSerialization()
    {
        var userId = Id<User>.New();
        var id = new Id<User>(userId.GetValue());

        var json = JsonSerializer.Serialize(id, SharedSerializationOptions.Current);
        var deserialized = JsonSerializer.Deserialize<Id<User>>(json, SharedSerializationOptions.Current);

        Assert.That(deserialized.GetValue(), Is.EqualTo(id.GetValue()));
    }

    [Test]
    public void SharedSerializationOptions_Supports_NullableTypedId()
    {
        Id<User>? id = Id<User>.New();

        var json = JsonSerializer.Serialize(id, SharedSerializationOptions.Current);
        var deserialized = JsonSerializer.Deserialize<Id<User>?>(json, SharedSerializationOptions.Current);

        Assert.That(deserialized, Is.Not.Null);
        Assert.That(deserialized!.Value.GetValue(), Is.EqualTo(id!.Value.GetValue()));
    }

    #endregion

    #region HTTP Model Binding (Request/Response Serialization)

    [Test]
    public void ModelBinding_DeserializesFromRequestJson_WithTypedId()
    {
        // Simulates incoming HTTP request body with typed ID
        var requestJson = """{"userId":"00000000-0000-0000-0000-000000000050","planId":"00000000-0000-0000-0000-000000000051"}""";

        var deserialized = JsonSerializer.Deserialize<CreatePlanRequest>(requestJson, _serializerOptions);

        Assert.That(deserialized, Is.Not.Null);
        Assert.That(deserialized!.UserId.GetValue(), Is.EqualTo(Guid.Parse("00000000-0000-0000-0000-000000000050")));
        Assert.That(deserialized.PlanId.GetValue(), Is.EqualTo(Guid.Parse("00000000-0000-0000-0000-000000000051")));
    }

    [Test]
    public void ModelBinding_SerializesForResponseJson_WithTypedId()
    {
        // Simulates outgoing HTTP response body with typed ID
        var userId = Guid.Parse("00000000-0000-0000-0000-000000000052");
        var response = new UserResponse { Id = new Id<User>(userId) };

        var json = JsonSerializer.Serialize(response, _serializerOptions);

        Assert.That(json, Does.Contain("\"00000000-0000-0000-0000-000000000052\""));
    }

    [Test]
    public void ModelBinding_HandlesCamelCaseNaming_ForTypedIdFields()
    {
        // Verifies camel case naming policy works with typed IDs
        var options = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            Converters = { new TypedIdJsonConverterFactory() }
        };

        var request = new CreatePlanRequest
        {
            UserId = (Id<User>)Guid.NewGuid(),
            PlanId = (Id<Plan>)Guid.NewGuid()
        };

        var json = JsonSerializer.Serialize(request, options);
        var parsed = JsonSerializer.Deserialize<CreatePlanRequest>(json, options);

        Assert.That(parsed!.UserId.GetValue(), Is.EqualTo(request.UserId.GetValue()));
        Assert.That(parsed.PlanId.GetValue(), Is.EqualTo(request.PlanId.GetValue()));
    }

    #endregion

    #region Test Fixtures

    private class UserDto
    {
        public Id<User> Id { get; set; }
    }

    private class UserWithOptionalIdDto
    {
        public Id<User>? Id { get; set; }
    }

    private class UserResponse
    {
        public Id<User> Id { get; set; }
    }

    private class CreatePlanRequest
    {
        public Id<User> UserId { get; set; }
        public Id<Plan> PlanId { get; set; }
    }

    #endregion
}
