using System.Security.Cryptography;
using System.Text;
using FluentAssertions;
using ImageMagick;
using LgymApi.Application.WorkoutProgress.ExerciseMedia;
using LgymApi.Infrastructure.Services;

namespace LgymApi.UnitTests;

[TestFixture]
public sealed class ExerciseMediaVerifierTests
{
    private static string Hash(byte[] bytes) => Convert.ToHexStringLower(SHA256.HashData(bytes));

    private static byte[] AnimatedGif(int count = 2, uint size = 4)
    {
        using var images = new MagickImageCollection();
        for (var i = 0; i < count; i++)
        {
            var image = new MagickImage(i % 2 == 0 ? MagickColors.Red : MagickColors.Blue, size, size) { AnimationDelay = (uint)(10 + i), AnimationIterations = 0 };
            images.Add(image);
        }
        return images.ToByteArray(MagickFormat.Gif);
    }

    [Test]
    public async Task AnimatedGif_RetainsDistinctFramePixelsTimingAndLoop()
    {
        var original = AnimatedGif();
        var result = await new ExerciseMediaVerifier().VerifyAsync(original, "image/gif", Hash(original), default);
        result.Frames.Should().Be(2);
        result.SourceSha256.Should().Be(Hash(original)); result.PublishedSha256.Should().Be(Hash(result.Bytes));
        using var source = new MagickImageCollection(original); using var published = new MagickImageCollection(result.Bytes);
        source.Coalesce(); published.Coalesce();
        published.Count.Should().Be(source.Count);
        for (var i = 0; i < source.Count; i++)
        {
            source[i].AnimationDelay.Should().Be(published[i].AnimationDelay);
            source[i].AnimationIterations.Should().Be(published[i].AnimationIterations);
            using var a = source[i].GetPixels(); using var b = published[i].GetPixels();
            a.ToByteArray(PixelMapping.RGBA).Should().Equal(b.ToByteArray(PixelMapping.RGBA)!);
        }
    }

    [TestCase(MagickFormat.Png, "image/png")]
    [TestCase(MagickFormat.Jpeg, "image/jpeg")]
    [TestCase(MagickFormat.WebP, "image/webp")]
    public async Task StillImages_ApplyOrientationAndRemovePrivateMetadata(MagickFormat format, string mime)
    {
        using var image = new MagickImage(MagickColors.Red, 7, 3);
        var profile = new ExifProfile(); profile.SetValue(ExifTag.Artist, "Private photographer"); profile.SetValue(ExifTag.Orientation, (ushort)6);
        image.SetProfile(profile);
        image.Orientation = Orientation.RightTop;
        image.SetProfile(new XmpProfile(Encoding.UTF8.GetBytes("<x:xmpmeta xmlns:x='adobe:ns:meta/'><private-location>GPS fixture</private-location></x:xmpmeta>")));
        var original = image.ToByteArray(format);
        using var encodedSource = new MagickImage(original);
        encodedSource.Orientation.Should().Be(Orientation.RightTop, "the fixture must actually carry the orientation being tested");
        var result = await new ExerciseMediaVerifier().VerifyAsync(original, mime, Hash(original), default);
        result.SourceSha256.Should().Be(Hash(original)); result.PublishedSha256.Should().Be(Hash(result.Bytes));
        using var output = new MagickImage(result.Bytes);
        output.ProfileNames.Should().BeEmpty();
        output.Width.Should().Be(3); output.Height.Should().Be(7);
        Encoding.Latin1.GetString(result.Bytes).Should().NotContain("Private photographer");
    }

    [Test]
    public async Task ActualMimeMalformedAndChecksumMismatch_AreRejected()
    {
        var bytes = AnimatedGif(); var verifier = new ExerciseMediaVerifier();
        await Assert.ThrowsAsync<ExerciseMediaRejectedException>(async () => await verifier.VerifyAsync(bytes, "image/jpeg", Hash(bytes), default));
        await Assert.ThrowsAsync<ExerciseMediaRejectedException>(async () => await verifier.VerifyAsync(bytes, "image/gif", new string('0', 64), default));
        await Assert.ThrowsAsync<ExerciseMediaRejectedException>(async () => await verifier.VerifyAsync("GIF89a"u8.ToArray(), "image/gif", Hash("GIF89a"u8.ToArray()), default));
    }

    [Test]
    public async Task FrameDimensionAndAggregateBudgets_AreEnforcedBeforeDecode()
    {
        foreach (var bytes in new[] { ExcessiveFrames(), OversizedHeader(), AnimatedGif(65, 500) })
            await Assert.ThrowsAsync<ExerciseMediaRejectedException>(async () => await new ExerciseMediaVerifier().VerifyAsync(bytes, "image/gif", Hash(bytes), default));
    }

    private static byte[] OversizedHeader()
    {
        var bytes = AnimatedGif();
        bytes[6] = 1; bytes[7] = 8;
        return bytes;
    }

    private static byte[] ExcessiveFrames()
    {
        var bytes = Convert.FromHexString("47494638396101000100800000000000ffffff21f904000a0000002c00000000010001000002024401003b");
        return bytes[..19].Concat(Enumerable.Range(0, 101).SelectMany(_ => bytes[19..^1])).Append((byte)0x3b).ToArray();
    }
    [Test]
    public async Task AnimatedPngAndWebp_AreRejectedRatherThanFlattened()
    {
        using var frames = new MagickImageCollection(AnimatedGif());
        var webp = frames.ToByteArray(MagickFormat.WebP);
        await Assert.ThrowsAsync<ExerciseMediaRejectedException>(async () => await new ExerciseMediaVerifier().VerifyAsync(webp, "image/webp", Hash(webp), default));
        // A valid PNG plus its APNG animation declaration must be rejected even by a static PNG decoder.
        var png = frames[0].ToByteArray(MagickFormat.Png);
        var acTL = Convert.FromHexString("000000086163544c0000000200000000f38d9370");
        var apng = png[..33].Concat(acTL).Concat(png[33..]).ToArray();
        await Assert.ThrowsAsync<ExerciseMediaRejectedException>(async () => await new ExerciseMediaVerifier().VerifyAsync(apng, "image/png", Hash(apng), default));
    }
}
