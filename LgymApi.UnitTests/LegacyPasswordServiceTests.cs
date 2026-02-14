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

        Assert.That(result, Is.False);
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

        Assert.That(result, Is.True);
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

        Assert.That(result, Is.True);
    }

    [Test]
    public void Verify_ReturnsFalse_WhenHashInvalid()
    {
        var result = _service.Verify("password", "not-hex", "salt", 25000, 512, "sha256");

        Assert.That(result, Is.False);
    }
}
