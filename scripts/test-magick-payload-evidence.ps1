Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
$root = Join-Path ([IO.Path]::GetTempPath()) ([guid]::NewGuid().ToString('N'))
$package = Join-Path $root 'package'
$publish = Join-Path $root 'publish'
$asset = 'runtimes/linux-x64/native/Magick.Native-Q8-x64.dll.so'
$exporter = Join-Path $PSScriptRoot 'export-magick-payload-evidence.ps1'
try {
    New-Item -ItemType Directory -Path (Join-Path $package 'runtimes/linux-x64/native'), (Join-Path $publish 'runtimes/linux-x64/native') -Force | Out-Null
    '<package><metadata><id>Magick.Fixture</id><version>1.0.0</version></metadata></package>' |
        Set-Content (Join-Path $package 'fixture.nuspec')
    '[ codec 1.0 (2026-01-01) (https://example.org/source) ]' |
        Set-Content (Join-Path $package 'Notice.txt')
    '{"libraries":{"Magick.Fixture/1.0.0":{}},"runtimeTarget":{"name":"fixture"}}' |
        Set-Content (Join-Path $publish 'fixture.deps.json')
    [IO.File]::WriteAllBytes((Join-Path $package $asset), [byte[]](1, 2, 3))
    Copy-Item (Join-Path $package $asset) (Join-Path $publish $asset)
    $output = Join-Path $root 'valid'
    & $exporter -PublishDirectory $publish -PackageDirectory $package -OutputDirectory $output
    $result = Get-Content (Join-Path $output 'magick-payload.json') -Raw | ConvertFrom-Json
    if ($result.nativeAssets.Count -ne 1 -or $result.noticeComponents.Count -ne 1 -or
        $result.nativeAssets[0].matchingPackageAssets[0] -ne $asset) {
        throw 'Valid fixture inventory was incorrect.'
    }
    $rejected = $false
    try { & $exporter -PublishDirectory $publish -PackageDirectory $package -OutputDirectory $output }
    catch { $rejected = $_.Exception.Message -like '*new output directory*' }
    if (-not $rejected) { throw 'Existing evidence must never be overwritten.' }
    [IO.File]::WriteAllBytes((Join-Path $publish $asset), [byte[]](4, 5, 6))
    $rejected = $false
    try { & $exporter -PublishDirectory $publish -PackageDirectory $package -OutputDirectory (Join-Path $root 'tampered') }
    catch { $rejected = $_.Exception.Message -like '*does not match*' }
    if (-not $rejected) { throw 'Tampered native payload was not rejected.' }
    Write-Output 'Magick inventory fixtures passed: matching payload, overwrite refusal, tamper rejection.'
}
finally {
    Remove-Item -LiteralPath $root -Recurse -Force
}
