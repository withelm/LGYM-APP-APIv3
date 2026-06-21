using FluentAssertions;
using LgymApi.Domain.Entities;
using LgymApi.Domain.ValueObjects;
using LgymApi.Infrastructure.Services;
using NUnit.Framework;

namespace LgymApi.UnitTests;

[TestFixture]
public sealed class LocalPhotoDevelopmentStoreTests
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
    public void ResolvePath_WhenStorageKeyContainsTraversal_ThrowsInvalidOperationException()
    {
        var action = () => _store.ResolvePath($"{_testPrefix}/../escape.png");

        action.Should().Throw<InvalidOperationException>();
    }

    [Test]
    public async Task SaveReadDeleteAndMetadataAsync_RoundTripsFile()
    {
        var storageKey = $"{_testPrefix}/photos/sample.png";
        var fileBytes = new byte[] { 1, 2, 3, 4, 5 };

        await using (var stream = new MemoryStream(fileBytes))
        {
            await _store.SaveAsync(storageKey, stream);
        }

        var savedBytes = await _store.ReadAsync(storageKey);
        var metadata = await _store.GetMetadataAsync(storageKey);

        savedBytes.Should().Equal(fileBytes);
        metadata.Should().NotBeNull();
        metadata!.SizeBytes.Should().Be(fileBytes.Length);
        metadata.ContentType.Should().Be("image/png");
        _store.ResolveContentType(storageKey).Should().Be("image/png");

        await _store.DeleteAsync(storageKey);

        (await _store.ReadAsync(storageKey)).Should().BeNull();
    }

    [Test]
    public async Task GetMetadataAsync_WhenFileDoesNotExist_ReturnsNull()
    {
        var metadata = await _store.GetMetadataAsync($"{_testPrefix}/missing.jpg");

        metadata.Should().BeNull();
    }
}
