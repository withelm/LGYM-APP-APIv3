using FluentAssertions;
using LgymApi.Api.Features.Trainer.Controllers;
using LgymApi.Domain.Entities;
using LgymApi.Domain.ValueObjects;
using LgymApi.Infrastructure.Services;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using NUnit.Framework;

namespace LgymApi.UnitTests;

[TestFixture]
public sealed class LocalPhotoDevelopmentControllerTests
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
        var controller = CreateController(isDevelopment: false);
        controller.ControllerContext.HttpContext.Request.Body = new MemoryStream(new byte[] { 1 });

        var result = await controller.Upload($"{_testPrefix}/photo.jpg", CancellationToken.None);

        result.Should().BeOfType<NotFoundResult>();
    }

    [Test]
    public async Task Upload_WhenStorageKeyMissing_ReturnsBadRequest()
    {
        var controller = CreateController(isDevelopment: true);
        controller.ControllerContext.HttpContext.Request.Body = new MemoryStream(new byte[] { 1 });

        var result = await controller.Upload(" ", CancellationToken.None);

        result.Should().BeOfType<BadRequestResult>();
    }

    [Test]
    public async Task Upload_WhenDevelopment_SavesDecodedFileAndReturnsNoContent()
    {
        var controller = CreateController(isDevelopment: true);
        var storageKey = $"{_testPrefix}/photos/my image.png";
        controller.ControllerContext.HttpContext.Request.Body = new MemoryStream(new byte[] { 10, 20, 30 });

        var result = await controller.Upload(Uri.EscapeDataString(storageKey), CancellationToken.None);

        result.Should().BeOfType<NoContentResult>();
        (await _store.ReadAsync(storageKey)).Should().Equal(new byte[] { 10, 20, 30 });
    }

    [Test]
    public async Task Read_WhenFileDoesNotExist_ReturnsNotFound()
    {
        var controller = CreateController(isDevelopment: true);

        var result = await controller.Read($"{_testPrefix}/missing.jpg", CancellationToken.None);

        result.Should().BeOfType<NotFoundResult>();
    }

    [Test]
    public async Task Read_WhenDevelopment_ReturnsFileResultWithResolvedContentType()
    {
        var storageKey = $"{_testPrefix}/photos/result.png";
        await using (var stream = new MemoryStream(new byte[] { 7, 8, 9 }))
        {
            await _store.SaveAsync(storageKey, stream);
        }

        var controller = CreateController(isDevelopment: true);

        var result = await controller.Read(Uri.EscapeDataString(storageKey), CancellationToken.None);

        result.Should().BeOfType<FileContentResult>();
        var fileResult = (FileContentResult)result;
        fileResult.ContentType.Should().Be("image/png");
        fileResult.FileContents.Should().Equal(new byte[] { 7, 8, 9 });
    }

    private LocalPhotoDevelopmentController CreateController(bool isDevelopment)
        => new(_store, new StubWebHostEnvironment(isDevelopment))
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext()
            }
        };

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
