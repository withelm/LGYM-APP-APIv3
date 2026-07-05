using FluentAssertions;
using LgymApi.Api.Configuration;
using LgymApi.Domain.Entities;
using LgymApi.Domain.ValueObjects;
using LgymApi.Infrastructure.Services;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using NUnit.Framework;

namespace LgymApi.UnitTests;

[TestFixture]
public sealed class LocalPhotoDevelopmentEndpointsTests
{
    private LocalPhotoDevelopmentStore _store = null!;
    private string _testPrefix = null!;

    [SetUp]
    public void SetUp()
    {
        _store = new LocalPhotoDevelopmentStore();
        _testPrefix = $"tests/{Id<User>.New()}";
    }

    [TearDown]
    public void TearDown()
    {
        var rootPath = _store.ResolvePath(_testPrefix);
        if (Directory.Exists(rootPath))
        {
            Directory.Delete(rootPath, recursive: true);
        }
    }

    [Test]
    public async Task Upload_WhenEnvironmentIsNotDevelopment_ReturnsNotFound()
    {
        var request = new DefaultHttpContext().Request;
        request.Body = new MemoryStream(new byte[] { 1 });

        var result = await LocalPhotoDevelopmentEndpoints.UploadAsync(
            $"{_testPrefix}/photo.jpg",
            request,
            _store,
            new StubWebHostEnvironment(isDevelopment: false),
            CancellationToken.None);

        result.Result.Should().BeOfType<Microsoft.AspNetCore.Http.HttpResults.NotFound>();
    }

    [Test]
    public async Task Upload_WhenStorageKeyMissing_ReturnsBadRequest()
    {
        var request = new DefaultHttpContext().Request;
        request.Body = new MemoryStream(new byte[] { 1 });

        var result = await LocalPhotoDevelopmentEndpoints.UploadAsync(
            " ",
            request,
            _store,
            new StubWebHostEnvironment(isDevelopment: true),
            CancellationToken.None);

        result.Result.Should().BeOfType<Microsoft.AspNetCore.Http.HttpResults.BadRequest>();
    }

    [Test]
    public async Task Upload_WhenDevelopment_SavesDecodedFileAndReturnsNoContent()
    {
        var storageKey = $"{_testPrefix}/photos/my image.png";
        var request = new DefaultHttpContext().Request;
        request.Body = new MemoryStream(new byte[] { 10, 20, 30 });

        var result = await LocalPhotoDevelopmentEndpoints.UploadAsync(
            Uri.EscapeDataString(storageKey),
            request,
            _store,
            new StubWebHostEnvironment(isDevelopment: true),
            CancellationToken.None);

        result.Result.Should().BeOfType<Microsoft.AspNetCore.Http.HttpResults.NoContent>();
        (await _store.ReadAsync(storageKey)).Should().Equal(new byte[] { 10, 20, 30 });
    }

    [Test]
    public async Task Read_WhenFileDoesNotExist_ReturnsNotFound()
    {
        var result = await LocalPhotoDevelopmentEndpoints.ReadAsync(
            $"{_testPrefix}/missing.jpg",
            _store,
            new StubWebHostEnvironment(isDevelopment: true),
            CancellationToken.None);

        result.Result.Should().BeOfType<Microsoft.AspNetCore.Http.HttpResults.NotFound>();
    }

    [Test]
    public async Task Read_WhenDevelopment_ReturnsFileResultWithResolvedContentType()
    {
        var storageKey = $"{_testPrefix}/photos/result.png";
        await using (var stream = new MemoryStream(new byte[] { 7, 8, 9 }))
        {
            await _store.SaveAsync(storageKey, stream);
        }

        var result = await LocalPhotoDevelopmentEndpoints.ReadAsync(
            Uri.EscapeDataString(storageKey),
            _store,
            new StubWebHostEnvironment(isDevelopment: true),
            CancellationToken.None);

        result.Result.Should().BeOfType<Microsoft.AspNetCore.Http.HttpResults.FileContentHttpResult>();
        var fileResult = (Microsoft.AspNetCore.Http.HttpResults.FileContentHttpResult)result.Result!;
        fileResult.ContentType.Should().Be("image/png");
        fileResult.FileContents.Should().Equal(new byte[] { 7, 8, 9 });
    }

    private sealed class StubWebHostEnvironment : IWebHostEnvironment
    {
        public StubWebHostEnvironment(bool isDevelopment)
        {
            EnvironmentName = isDevelopment ? Environments.Development : Environments.Production;
        }

        public string ApplicationName { get; set; } = "LgymApi.UnitTests";
        public IFileProvider WebRootFileProvider { get; set; } = new NullFileProvider();
        public string WebRootPath { get; set; } = AppContext.BaseDirectory;
        public string EnvironmentName { get; set; }
        public string ContentRootPath { get; set; } = AppContext.BaseDirectory;
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}
