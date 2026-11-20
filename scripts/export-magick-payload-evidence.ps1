param(
    [Parameter(Mandatory)]
    [string]$PublishDirectory,
    [Parameter(Mandatory)]
    [string]$PackageDirectory,
    [Parameter(Mandatory)]
    [string]$OutputDirectory
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

# Static inventory only: never loads a managed/native library or launches the API.
$publish = (Resolve-Path -LiteralPath $PublishDirectory).Path
$package = (Resolve-Path -LiteralPath $PackageDirectory).Path
if (Test-Path -LiteralPath $OutputDirectory) {
    throw "Choose a new output directory to preserve earlier evidence."
}
$notice = Join-Path $package "Notice.txt"
$nuspecFiles = @(Get-ChildItem -LiteralPath $package -Filter "*.nuspec" -File)
if ($nuspecFiles.Count -ne 1 -or -not (Test-Path -LiteralPath $notice)) {
    throw "Expected one NuGet manifest and Notice.txt in the extracted package."
}
[xml]$nuspec = Get-Content -LiteralPath $nuspecFiles[0].FullName -Raw
$packageId = [string]$nuspec.package.metadata.id
$version = [string]$nuspec.package.metadata.version
$depsFiles = @(Get-ChildItem -LiteralPath $publish -Filter "*.deps.json" -File)
$matchingDeps = @(
    foreach ($file in $depsFiles) {
        $deps = Get-Content -LiteralPath $file.FullName -Raw | ConvertFrom-Json -AsHashtable
        if ($deps.libraries.ContainsKey("$packageId/$version")) {
            [ordered]@{
                path = $file.Name
                sha256 = (Get-FileHash -LiteralPath $file.FullName -Algorithm SHA256).Hash.ToLowerInvariant()
                runtimeTarget = $deps.runtimeTarget.name
            }
        }
    }
)
if ($matchingDeps.Count -eq 0) {
    throw "Publish dependency manifests do not reference $packageId/$version."
}
$packageAssets = @(
    Get-ChildItem -LiteralPath (Join-Path $package "runtimes") -Recurse -File |
        Where-Object Name -Like "Magick.Native*" |
        ForEach-Object {
            [ordered]@{
                path = [IO.Path]::GetRelativePath($package, $_.FullName).Replace('\', '/')
                sha256 = (Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash.ToLowerInvariant()
            }
        }
)
$publishedAssets = @(
    Get-ChildItem -LiteralPath $publish -Recurse -File |
        Where-Object Name -Like "Magick.Native*" |
        Sort-Object FullName |
        ForEach-Object {
            $hash = (Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash.ToLowerInvariant()
            [ordered]@{
                path = [IO.Path]::GetRelativePath($publish, $_.FullName).Replace('\', '/')
                bytes = $_.Length
                sha256 = $hash
                matchingPackageAssets = @($packageAssets | Where-Object { $_.sha256 -eq $hash } | ForEach-Object { $_.path })
            }
        }
)
if ($publishedAssets.Count -eq 0) { throw "No Magick native assets found in publish." }
if (@($publishedAssets | Where-Object { $_.matchingPackageAssets.Count -eq 0 }).Count -gt 0) {
    throw "A published native asset does not match the supplied NuGet package."
}
$components = @(
    Get-Content -LiteralPath $notice | ForEach-Object {
        if ($_ -match '^\[\s*(?<name>.+?)\s+(?<version>\d[^\s]*)\s+\((?<date>\d{4}-\d{2}-\d{2})\)\s+\((?<source>https://[^)]+)\)\s*\]') {
            [ordered]@{
                name = $Matches.name
                version = $Matches.version
                date = $Matches.date
                source = $Matches.source
            }
        }
    }
)
if ($components.Count -eq 0) { throw "No component headers recognized in Notice.txt." }
$evidence = [ordered]@{
    schemaVersion = 1
    generatedAtUtc = [DateTimeOffset]::UtcNow.ToString("o")
    package = "$packageId/$version"
    dependencyManifests = $matchingDeps
    nativeAssets = $publishedAssets
    noticeSha256 = (Get-FileHash -LiteralPath $notice -Algorithm SHA256).Hash.ToLowerInvariant()
    noticeComponents = $components
    limitations = @(
        "NOTICE component names are not proof of linkage in each RID."
        "Magick.NET 15.1.0 upstream packaging takes NOTICE from libraries/win."
        "This inventories supplied publish files, not a running deployment or container image."
        "No license compliance, patent clearance or decoder compatibility verdict is implied."
    )
}
New-Item -ItemType Directory -Path $OutputDirectory | Out-Null
Copy-Item -LiteralPath $notice -Destination (Join-Path $OutputDirectory "Magick.NET.Notice.txt")
$evidence | ConvertTo-Json -Depth 12 | Set-Content -LiteralPath (Join-Path $OutputDirectory "magick-payload.json") -Encoding utf8
Write-Output "Verified $($publishedAssets.Count) native assets against $packageId/$version; indexed $($components.Count) NOTICE components."
