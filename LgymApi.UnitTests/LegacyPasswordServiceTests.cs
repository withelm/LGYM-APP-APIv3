using FluentAssertions;
using LgymApi.Infrastructure.Services;

namespace LgymApi.UnitTests;

[TestFixture]
public sealed class LegacyPasswordServiceTests
{
    private readonly LegacyPasswordService _service = new();

    [TestCase("", "hash", "salt")]
    [TestCase("password", "", "salt")]
    [TestCase("password", "hash", "")]
    public void Verify_ReturnsFalse_WhenRequiredInputsMissing(string password, string hash, string salt)
    {
        var result = _service.Verify(password, hash, salt, null, null, null);

        result.Should().BeFalse();
    }

    [Test]
    public void Create_ThenVerify_ReturnsTrue()
    {
        var created = _service.Create("s3cret");

        var result = _service.Verify(
            "s3cret",
            created.Hash,
            created.Salt,
            created.Iterations,
            created.KeyLength,
            created.Digest);

        result.Should().BeTrue();
    }

    [Test]
    public void Verify_AllowsKeyLengthInBits()
    {
        var created = _service.Create("s3cret");

        var result = _service.Verify(
            "s3cret",
            created.Hash,
            created.Salt,
            created.Iterations,
            created.KeyLength * 8,
            created.Digest);

        result.Should().BeTrue();
    }

    [Test]
    public void Verify_ReturnsFalse_WhenHashInvalid()
    {
        var result = _service.Verify(
            "password",
            "not-hex",
            "salt",
            LegacyPasswordConstants.Iterations,
            LegacyPasswordConstants.KeyLength,
            LegacyPasswordConstants.Digest);

        result.Should().BeFalse();
    }
}
