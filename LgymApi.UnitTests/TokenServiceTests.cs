using System.IdentityModel.Tokens.Jwt;
using LgymApi.Infrastructure.Services;
using Microsoft.Extensions.Configuration;

namespace LgymApi.UnitTests;

[TestFixture]
public sealed class TokenServiceTests
{
    [Test]
    public void CreateToken_WithoutSigningKey_ThrowsInvalidOperationException()
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection().Build();
        var service = new TokenService(configuration);

        Assert.Throws<InvalidOperationException>(() => service.CreateToken(Guid.NewGuid()));
    }

    [Test]
    public void CreateToken_WithSigningKey_ReturnsSignedJwt()
    {
        var settings = new Dictionary<string, string?>
        {
            ["Jwt:SigningKey"] = "unit-test-signing-key-at-least-32-chars"
        };
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(settings).Build();
        var service = new TokenService(configuration);

        var userId = Guid.NewGuid();
        var token = service.CreateToken(userId);

        Assert.That(token, Is.Not.Null.And.Not.Empty);

        var jwt = new JwtSecurityTokenHandler().ReadJwtToken(token);
        Assert.That(jwt.Claims.Any(c => c.Type == "userId" && c.Value == userId.ToString()), Is.True);
    }
}
