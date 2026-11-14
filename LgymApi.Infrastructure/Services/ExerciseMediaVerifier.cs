using System.Security.Cryptography;
using ImageMagick;
using LgymApi.Application.WorkoutProgress.ExerciseMedia;

namespace LgymApi.Infrastructure.Services;

public sealed class ExerciseMediaVerifier : IExerciseMediaVerifier
{
    static ExerciseMediaVerifier()
    {
        ResourceLimits.Memory = 256 * 1024 * 1024;
        ResourceLimits.Disk = 0;
        ResourceLimits.Width = ExerciseMediaLimits.MaxDimension;
        ResourceLimits.Height = ExerciseMediaLimits.MaxDimension;
        ResourceLimits.ListLength = ExerciseMediaLimits.MaxFrames + 1;
        ResourceLimits.Thread = 1;
        ResourceLimits.Time = 15;
        ResourceLimits.MaxProfileSize = 1024 * 1024;
    }

    public Task<VerifiedExerciseMedia> VerifyAsync(byte[] bytes, CancellationToken cancellationToken)
    {
        if (bytes.Length is <= 0 or > ExerciseMediaLimits.MaxBytes) throw new ExerciseMediaRejectedException();
        var (format, mime, extension) = Detect(bytes);
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            // Explicit format blocks delegates/URL/file coders. Ping precedes pixel allocation.
            var settings = new MagickReadSettings { Format = format, FrameCount = ExerciseMediaLimits.MaxFrames + 1 };
            using var info = new MagickImageCollection();
            info.Ping(bytes, settings);
            ValidateBudgets(info, mime);
            using var images = new MagickImageCollection(bytes, settings);
            ValidateBudgets(images, mime);
            if (images.Count != info.Count) throw new ExerciseMediaRejectedException();
            foreach (var image in images)
            {
                image.AutoOrient();
                image.Strip();
                image.Quality = 90;
            }
            cancellationToken.ThrowIfCancellationRequested();
            var published = images.ToByteArray(format);
            if (published.Length > ExerciseMediaLimits.MaxBytes) throw new ExerciseMediaRejectedException();
            using var check = new MagickImageCollection(published, settings);
            ValidateBudgets(check, mime);
            if (check.Count != images.Count || check.Any(x => x.ProfileNames.Any())) throw new ExerciseMediaRejectedException();
            for (var i = 0; i < check.Count; i++)
                if (check[i].Width != images[i].Width || check[i].Height != images[i].Height || (mime == "image/gif" && (check[i].AnimationDelay != images[i].AnimationDelay || check[i].AnimationIterations != images[i].AnimationIterations))) throw new ExerciseMediaRejectedException();
            return Task.FromResult(new VerifiedExerciseMedia(published, mime, extension, Convert.ToHexStringLower(SHA256.HashData(published))));
        }
        catch (MagickException) { throw new ExerciseMediaRejectedException(); }
    }

    private static (MagickFormat Format, string Mime, string Extension) Detect(byte[] bytes)
    {
        var span = bytes.AsSpan();
        if (span.StartsWith("GIF87a"u8) || span.StartsWith("GIF89a"u8)) return (MagickFormat.Gif, "image/gif", "gif");
        if (span.StartsWith(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 }))
        {
            // The PNG decoder may expose only the first APNG frame; reject its animation declaration.
            var position = 8;
            while (position <= span.Length - 12)
            {
                var size = System.Buffers.Binary.BinaryPrimitives.ReadUInt32BigEndian(span.Slice(position, 4));
                if (size > span.Length - position - 12) throw new ExerciseMediaRejectedException();
                if (span.Slice(position + 4, 4).SequenceEqual("acTL"u8)) throw new ExerciseMediaRejectedException();
                position += checked((int)size + 12);
            }
            return (MagickFormat.Png, "image/png", "png");
        }
        if (span.StartsWith(new byte[] { 255, 216, 255 })) return (MagickFormat.Jpeg, "image/jpeg", "jpg");
        if (span.Length >= 12 && span[..4].SequenceEqual("RIFF"u8) && span.Slice(8, 4).SequenceEqual("WEBP"u8)) return (MagickFormat.WebP, "image/webp", "webp");
        throw new ExerciseMediaRejectedException();
    }

    private static void ValidateBudgets(MagickImageCollection images, string mime)
    {
        if (images.Count == 0 || images.Count > ExerciseMediaLimits.MaxFrames || (mime != "image/gif" && images.Count != 1)) throw new ExerciseMediaRejectedException();
        long pixels = 0;
        foreach (var image in images)
        {
            var width = Math.Max(image.Width, image.Page.Width);
            var height = Math.Max(image.Height, image.Page.Height);
            if (width == 0 || height == 0 || width > ExerciseMediaLimits.MaxDimension || height > ExerciseMediaLimits.MaxDimension) throw new ExerciseMediaRejectedException();
            pixels += (long)width * height;
            if (pixels > ExerciseMediaLimits.MaxDecodedPixels) throw new ExerciseMediaRejectedException();
        }
    }
}
