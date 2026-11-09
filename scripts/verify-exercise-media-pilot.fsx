#I @"../LgymApi.Api/bin/Release/net10.0"
#r "Magick.NET.Core.dll"
#r "Magick.NET-Q8-AnyCPU.dll"
#r "LgymApi.Domain.dll"
#r "LgymApi.Platform.dll"
#r "LgymApi.Identity.dll"
#r "LgymApi.Application.dll"
#r "LgymApi.Infrastructure.dll"
open System
open System.IO
open System.IO.Compression
open System.Security.Cryptography
open System.Text.Json
open System.Threading
open ImageMagick
open LgymApi.Infrastructure.Services
let hash (bytes:byte[]) = Convert.ToHexStringLower(SHA256.HashData(bytes))
let frameHashes (images:MagickImageCollection) =
    images.Coalesce()
    [| for image in images do
        use pixels = image.GetPixels()
        yield hash (pixels.ToByteArray(PixelMapping.RGBA)) |]
// Read-only technical verification; no publication or verification of catalog associations.
let timing (images:MagickImageCollection) =
    [| for image in images do yield {| delay = int image.AnimationDelay; ticksPerSecond = int image.AnimationTicksPerSecond; loopCount = uint32 image.AnimationIterations |} |]
let durationMs (timings: {| delay: int; ticksPerSecond: int; loopCount: uint32 |} array) =
    timings |> Array.sumBy (fun frame ->
        if frame.ticksPerSecond <= 0 then failwith "GIF timing has no positive time base"
        float frame.delay * 1000.0 / float frame.ticksPerSecond)
let run (archivePath:string) (outputPath:string) =
    use archive = ZipFile.OpenRead(archivePath)
    let verifier = ExerciseMediaVerifier()
    let records =
        [| for entry in archive.Entries do
            if entry.FullName.StartsWith("LGYM/gif_pilot/") && entry.FullName.EndsWith("/animacja.gif") then
                use input = entry.Open()
                use buffer = new MemoryStream()
                input.CopyTo(buffer)
                let bytes = buffer.ToArray()
                let result = verifier.VerifyAsync(bytes, "image/gif", hash bytes, CancellationToken.None).GetAwaiter().GetResult()
                use sourceFrames = new MagickImageCollection(bytes)
                use publishedFrames = new MagickImageCollection(result.Bytes)
                let sourceTiming = timing sourceFrames
                let publishedTiming = timing publishedFrames
                let timingParity = sourceTiming = publishedTiming
                let sourceDuration = durationMs sourceTiming
                let publishedDuration = durationMs publishedTiming
                let durationParity = abs (sourceDuration - publishedDuration) < 0.0001
                let loopParity = (sourceTiming |> Array.map (fun frame -> frame.loopCount)) = (publishedTiming |> Array.map (fun frame -> frame.loopCount))
                let parity = frameHashes sourceFrames = frameHashes publishedFrames
                if not (parity && timingParity && durationParity && loopParity) then failwith ("Decoded frame/timing/loop mismatch: " + entry.FullName)
                yield {| sourceArchiveEntry = entry.FullName; sourceSha256 = hash bytes; publishedSha256 = result.PublishedSha256; sourceBytes = bytes.Length; publishedBytes = result.Bytes.Length; width = result.Width; height = result.Height; frames = result.Frames; sourceFrameTiming = sourceTiming; publishedFrameTiming = publishedTiming; sourceDurationMs = sourceDuration; publishedDurationMs = publishedDuration; durationParity = durationParity; loopParity = loopParity; frameTimingParity = timingParity; decodedFramePixelsEqual = parity; verificationScope = "technical_only_no_publication"; decoderAssemblyVersion = typeof<MagickImage>.Assembly.GetName().Version.ToString(); verifierAssemblySha256 = hash (File.ReadAllBytes(typeof<ExerciseMediaVerifier>.Assembly.Location)); verifierSourceSha256 = hash (File.ReadAllBytes(Path.Combine(__SOURCE_DIRECTORY__, "../LgymApi.Infrastructure/Services/ExerciseMediaVerifier.cs"))) |} |]
    if records.Length <> 10 then failwith "Expected exactly ten draft GIF entries"
    File.WriteAllText(outputPath, JsonSerializer.Serialize(records, JsonSerializerOptions(WriteIndented = true)))
    printfn "Verified %d technical draft GIFs: decoding, sanitization, bounds, exact coalesced frame pixels, frame timings, duration and loops. No publication; catalog associations unverified." records.Length

if fsi.CommandLineArgs.Length <> 3 then failwith "Usage: dotnet fsi scripts/verify-exercise-media-pilot.fsx ARCHIVE OUTPUT_JSON"
run fsi.CommandLineArgs.[1] fsi.CommandLineArgs.[2]
