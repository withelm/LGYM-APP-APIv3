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
// Read-only technical verification. This does not approve movement technique or publish assets.
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
                let parity = frameHashes sourceFrames = frameHashes publishedFrames
                if not parity then failwith ("Decoded frame pixel mismatch: " + entry.FullName)
                yield {| sourceArchiveEntry = entry.FullName; sourceSha256 = hash bytes; publishedSha256 = result.PublishedSha256; sourceBytes = bytes.Length; publishedBytes = result.Bytes.Length; width = result.Width; height = result.Height; frames = result.Frames; decodedFramePixelsEqual = parity; approval = "technical_only_not_human_approved" |} |]
    if records.Length <> 10 then failwith "Expected exactly ten draft GIF entries"
    File.WriteAllText(outputPath, JsonSerializer.Serialize(records, JsonSerializerOptions(WriteIndented = true)))
    printfn "Verified %d technical draft GIFs: decoding, sanitization, bounds and exact coalesced frame pixels. No publication or human technique approval." records.Length
    
if fsi.CommandLineArgs.Length <> 3 then failwith "Usage: dotnet fsi scripts/verify-exercise-media-pilot.fsx ARCHIVE OUTPUT_JSON"
run fsi.CommandLineArgs.[1] fsi.CommandLineArgs.[2]
