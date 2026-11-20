using LgymApi.Domain.Entities;
using LgymApi.Domain.ValueObjects;
using LgymApi.Infrastructure.Data;
using LgymApi.Infrastructure.Services;
using LgymApi.TestUtils;
using Microsoft.EntityFrameworkCore;

namespace LgymApi.UnitTests;

[TestFixture]
public sealed class TestDataFactoryLegacyPasswordCompatibilityTests
{
    [Test]
    public async Task SeedUserAsync_Creates_LegacyPasswordData_Compatible_With_The_LegacyVerifier()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Id<User>.New().ToString())
            .Options;
        await using var context = new AppDbContext(options);
        const string password = "legacy-compatible-password";

        var user = await TestDataFactory.SeedUserAsync(context, password: password);
        var verifier = new LegacyPasswordService();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(user.LegacyIterations, Is.EqualTo(25000));
            Assert.That(user.LegacyKeyLength, Is.EqualTo(512));
            Assert.That(user.LegacyDigest, Is.EqualTo("sha256"));
            Assert.That(user.LegacyHash, Does.Match("^[0-9a-f]+$"));
            Assert.That(user.LegacySalt, Does.Match("^[0-9a-f]+$"));
            Assert.That(verifier.Verify(password, user.LegacyHash!, user.LegacySalt!, user.LegacyIterations, user.LegacyKeyLength, user.LegacyDigest), Is.True);
            Assert.That(verifier.Verify("wrong-password", user.LegacyHash!, user.LegacySalt!, user.LegacyIterations, user.LegacyKeyLength, user.LegacyDigest), Is.False);
        }
    }
}
