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

        Assert.That(() => { action(); }, Throws.InstanceOf<InvalidOperationException>());
    }

    [Test]
    [Category("Task6Baseline")]
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

        Assert.That(savedBytes, Is.EqualTo(fileBytes));
        Assert.That(metadata, Is.Not.Null);
        Assert.That(metadata!.SizeBytes, Is.EqualTo(fileBytes.Length));
        Assert.That(metadata.ContentType, Is.EqualTo("image/png"));
        Assert.That(_store.ResolveContentType(storageKey), Is.EqualTo("image/png"));

        await _store.DeleteAsync(storageKey);

        Assert.That((await _store.ReadAsync(storageKey)), Is.Null);
    }

    [Test]
    public async Task GetMetadataAsync_WhenFileDoesNotExist_ReturnsNull()
    {
        var metadata = await _store.GetMetadataAsync($"{_testPrefix}/missing.jpg");

        Assert.That(metadata, Is.Null);
    }

    [Test]
    [Category("Task6Red")]
    public async Task SaveAsync_WhenCancellationIsAlreadyRequested_LeavesNoFinalOrTemporaryFile()
    {
        var storageKey = $"{_testPrefix}/photos/canceled.jpg";
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        await using var stream = new MemoryStream(new byte[] { 1, 2, 3 });

        var action = () => _store.SaveAsync(storageKey, stream, cancellation.Token);

        await Assert.ThatAsync(async () => { await action(); }, Throws.InstanceOf<OperationCanceledException>());
        AssertNoFilesRemain();
    }

    [Test]
    [Category("Task6Red")]
    public async Task SaveAsync_WhenSourceFailsAfterPartialWrite_LeavesNoFinalOrTemporaryFile()
    {
        var storageKey = $"{_testPrefix}/photos/partial.jpg";
        await using var stream = new PartialThenThrowStream(new byte[] { 1, 2, 3 });

        var action = () => _store.SaveAsync(storageKey, stream);

        await Assert.ThatAsync(async () => { await action(); }, Throws.InstanceOf<IOException>());
        AssertNoFilesRemain();
    }

    private void AssertNoFilesRemain()
    {
        var rootPath = _store.ResolvePath(_testPrefix);
        if (Directory.Exists(rootPath))
        {
            Assert.That(Directory.EnumerateFiles(rootPath, "*", SearchOption.AllDirectories), Is.Empty);
        }
    }

    private sealed class PartialThenThrowStream : Stream
    {
        private readonly byte[] _firstChunk;
        private bool _firstRead = true;

        public PartialThenThrowStream(byte[] firstChunk)
        {
            _firstChunk = firstChunk;
        }

        public override bool CanRead => true;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => throw new NotSupportedException();
        public override long Position
        {
            get => throw new NotSupportedException();
            set => throw new NotSupportedException();
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            if (!_firstRead)
            {
                throw new IOException("Simulated source failure.");
            }

            _firstRead = false;
            var bytesToCopy = Math.Min(count, _firstChunk.Length);
            _firstChunk.AsSpan(0, bytesToCopy).CopyTo(buffer.AsSpan(offset, bytesToCopy));
            return bytesToCopy;
        }

        public override ValueTask<int> ReadAsync(
            Memory<byte> buffer,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!_firstRead)
            {
                return ValueTask.FromException<int>(new IOException("Simulated source failure."));
            }

            _firstRead = false;
            var bytesToCopy = Math.Min(buffer.Length, _firstChunk.Length);
            _firstChunk.AsSpan(0, bytesToCopy).CopyTo(buffer.Span);
            return ValueTask.FromResult(bytesToCopy);
        }

        public override void Flush() => throw new NotSupportedException();
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    }
}
