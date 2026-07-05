using FluentAssertions;
using LgymApi.Infrastructure.Services;
using Microsoft.AspNetCore.Http;
using NSubstitute;
using NUnit.Framework;

namespace LgymApi.UnitTests;

[TestFixture]
public sealed class LocalPhotoStorageProviderTests
{
    [Test]
    public async Task GenerateSignedUploadUrlAsync_WhenRequestHostAvailable_UsesCurrentHost()
    {
        var httpContext = new DefaultHttpContext();
        httpContext.Request.Scheme = "https";
        httpContext.Request.Host = new HostString("api.example.com");
        var accessor = Substitute.For<IHttpContextAccessor>();
        accessor.HttpContext.Returns(httpContext);
        var provider = new LocalPhotoStorageProvider(accessor, new LocalPhotoDevelopmentStore());

        var result = await provider.GenerateSignedUploadUrlAsync("photos/key one.jpg", "image/jpeg", TimeSpan.FromMinutes(10));

        result.Should().StartWith("https://api.example.com/dev/photos/upload/");
        result.Should().Contain("photos%2Fkey%20one.jpg");
    }

    [Test]
    public async Task GenerateSignedReadUrlAsync_WhenRequestMissing_UsesFallbackLocalhost()
    {
        var accessor = Substitute.For<IHttpContextAccessor>();
        accessor.HttpContext.Returns((HttpContext?)null);
        var provider = new LocalPhotoStorageProvider(accessor, new LocalPhotoDevelopmentStore());

        var result = await provider.GenerateSignedReadUrlAsync("photos/key.jpg", TimeSpan.FromMinutes(5));

        result.Should().StartWith("https://localhost:7025/dev/photos/read/");
    }
}
