#Requires -Version 7.4
param(
    [string]$ResultsDirectory = "TestResults/S3Smoke",
    [ValidateRange(1, 300)]
    [int]$StartupTimeoutSeconds = 60,
    [ValidateNotNullOrEmpty()]
    [string]$Image = "chrislusf/seaweedfs@sha256:ccad23e18639c8b98cee17e7100b95830a06077b56d8e5b006d6dcf8ec5e94a0"
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"
$PSNativeCommandUseErrorActionPreference = $false

function Protect-Message([string]$Message) {
    foreach ($value in $sensitiveValues) {
        $Message = $Message.Replace($value, "[redacted]")
    }
    return $Message
}

function Invoke-CheckedCommand([string]$Executable, [string[]]$Arguments, [string]$Operation) {
    $output = @(& $Executable @Arguments 2>&1)
    $commandExitCode = $LASTEXITCODE
    $safeOutput = @($output | ForEach-Object { Protect-Message ([string]$_) })
    foreach ($line in $safeOutput) {
        Add-Content -LiteralPath $logPath -Value $line
        Write-Host $line
    }
    if ($commandExitCode -ne 0) {
        throw "$Operation failed (exit $commandExitCode)."
    }
    return $safeOutput
}

$repositoryRoot = Split-Path -Parent $PSScriptRoot
$sourcePath = Join-Path $PSScriptRoot "smoke/S3StorageSmoke.cs"
$infrastructureProject = Join-Path $repositoryRoot "LgymApi.Infrastructure/LgymApi.Infrastructure.csproj"
if (-not (Test-Path -LiteralPath $sourcePath -PathType Leaf)) {
    throw "S3 smoke source is missing from this checkout."
}
if (-not [IO.Path]::IsPathRooted($ResultsDirectory)) {
    $ResultsDirectory = Join-Path $repositoryRoot $ResultsDirectory
}
$runId = [Guid]::NewGuid().ToString("N")
$resultDirectory = (New-Item -ItemType Directory -Path (Join-Path $ResultsDirectory $runId) -Force).FullName
$logPath = Join-Path $resultDirectory "run.log"
$receiptPath = Join-Path $resultDirectory "smoke.json"
$resultPath = Join-Path $resultDirectory "result.json"
$workDirectory = Join-Path ([IO.Path]::GetTempPath()) "lgym-s3-smoke-$runId"
$projectName = "lgym-s3-smoke-$runId"
$composeFile = Join-Path $workDirectory "compose.json"
$composeArguments = @("compose", "--project-name", $projectName, "--file", $composeFile)
$originalEnvironment = @{}
foreach ($name in @("AWS_ACCESS_KEY_ID", "AWS_SECRET_ACCESS_KEY")) {
    $originalEnvironment[$name] = [Environment]::GetEnvironmentVariable($name, "Process")
}
$sensitiveValues = @()
$composeAttempted = $false
$smokePassed = $false
$cleanupSucceeded = $true
$failure = $null
$imageId = $null
$stage = "prerequisites"
$startedAt = [DateTimeOffset]::UtcNow

try {
    foreach ($command in @("docker", "dotnet")) {
        $null = Get-Command $command -ErrorAction Stop
    }
    $null = Invoke-CheckedCommand "docker" @("version", "--format", "{{.Server.Version}}") "Docker daemon check"
    $null = Invoke-CheckedCommand "docker" @("compose", "version") "Docker Compose check"
    $null = New-Item -ItemType Directory -Path $workDirectory
    $projectPath = Join-Path $workDirectory "S3StorageSmoke.csproj"
    $sourceXml = [Security.SecurityElement]::Escape($sourcePath)
    $referenceXml = [Security.SecurityElement]::Escape($infrastructureProject)
    @"
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <OutputType>Exe</OutputType>
    <TargetFramework>net10.0</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
    <EnableDefaultCompileItems>false</EnableDefaultCompileItems>
  </PropertyGroup>
  <ItemGroup>
    <Compile Include="$sourceXml" />
    <ProjectReference Include="$referenceXml" />
  </ItemGroup>
</Project>
"@ | Set-Content -LiteralPath $projectPath
    $stage = "build"
    $null = Invoke-CheckedCommand "dotnet" @("build", $projectPath, "--configuration", "Release", "--verbosity", "quiet", "--nologo") "Smoke build"
    $executablePath = Join-Path $workDirectory "bin/Release/net10.0/S3StorageSmoke.dll"
    $tlsDirectory = Join-Path $workDirectory "tls"
    $stage = "tls"
    $null = Invoke-CheckedCommand "dotnet" @($executablePath, "prepare-tls", $tlsDirectory) "TLS preparation"

    $accessKey = [Convert]::ToHexString([Security.Cryptography.RandomNumberGenerator]::GetBytes(16))
    $secretKey = [Convert]::ToHexString([Security.Cryptography.RandomNumberGenerator]::GetBytes(32))
    $sensitiveValues = @($accessKey, $secretKey)
    [Environment]::SetEnvironmentVariable("AWS_ACCESS_KEY_ID", $accessKey, "Process")
    [Environment]::SetEnvironmentVariable("AWS_SECRET_ACCESS_KEY", $secretKey, "Process")
    @{
        services = @{
            s3 = @{
                image = $Image
                command = @("mini", "-dir=/data", "-s3.cert.file=/tls/certificate.pem", "-s3.key.file=/tls/key.pem", "-s3.port.https=8443", "-master.telemetry=false")
                environment = @("AWS_ACCESS_KEY_ID", "AWS_SECRET_ACCESS_KEY")
                ports = @("127.0.0.1::8443")
                volumes = @(@{ type = "bind"; source = $tlsDirectory; target = "/tls"; read_only = $true })
                labels = @{ "lgym.developer-smoke" = "s3-storage" }
                healthcheck = @{
                    test = @("CMD-SHELL", "netstat -ltn | grep -q ':8443 .*LISTEN'")
                    interval = "1s"
                    timeout = "3s"
                    retries = $StartupTimeoutSeconds
                }
            }
        }
    } | ConvertTo-Json -Depth 10 | Set-Content -LiteralPath $composeFile

    $stage = "startup"
    $composeAttempted = $true
    $null = Invoke-CheckedCommand "docker" ($composeArguments + @("up", "--wait", "--wait-timeout", "$StartupTimeoutSeconds")) "S3 startup"
    $containerId = (@(Invoke-CheckedCommand "docker" ($composeArguments + @("ps", "--quiet", "s3")) "Container discovery") -join "").Trim()
    $imageId = (@(Invoke-CheckedCommand "docker" @("inspect", "--format", "{{.Image}}", $containerId) "Image identification") -join "").Trim()
    $binding = (@(Invoke-CheckedCommand "docker" ($composeArguments + @("port", "s3", "8443")) "Port discovery") -join "").Trim()
    if ($binding -notmatch '^127\.0\.0\.1:\d+$') {
        throw "Docker did not publish a loopback-only S3 port."
    }
    $stage = "smoke"
    $null = Invoke-CheckedCommand "dotnet" @($executablePath, "run", "https://$binding", (Join-Path $tlsDirectory "certificate.pem"), $receiptPath) "S3 smoke"
    $receipt = Get-Content -LiteralPath $receiptPath -Raw | ConvertFrom-Json
    if (-not $receipt.passed -or $receipt.expectedChecks -le 0 -or @($receipt.checks).Count -ne $receipt.expectedChecks) {
        throw "S3 smoke receipt is incomplete or failed."
    }
    $smokePassed = $true
}
catch {
    $failure = Protect-Message $_.Exception.Message
    Add-Content -LiteralPath $logPath -Value "FAIL [$stage]: $failure"
    Write-Error "FAIL [$stage]: $failure" -ErrorAction Continue
}
finally {
    if ($composeAttempted) {
        try {
            $null = Invoke-CheckedCommand "docker" ($composeArguments + @("down", "--volumes", "--remove-orphans")) "Docker cleanup"
        }
        catch {
            $cleanupSucceeded = $false
            Write-Error (Protect-Message $_.Exception.Message) -ErrorAction Continue
        }
    }
    foreach ($name in $originalEnvironment.Keys) {
        [Environment]::SetEnvironmentVariable($name, $originalEnvironment[$name], "Process")
    }
    try {
        if (Test-Path -LiteralPath $workDirectory) {
            Remove-Item -LiteralPath $workDirectory -Recurse -Force
        }
    }
    catch {
        $cleanupSucceeded = $false
        Write-Error "Could not remove the temporary smoke directory: $workDirectory" -ErrorAction Continue
    }
}

$passed = $smokePassed -and $cleanupSucceeded
@{
    passed = $passed
    smokePassed = $smokePassed
    cleanupSucceeded = $cleanupSucceeded
    startedAtUtc = $startedAt.ToString("O")
    finishedAtUtc = [DateTimeOffset]::UtcNow.ToString("O")
    image = $Image
    imageId = $imageId
    composeProject = $projectName
    workDirectory = $workDirectory
    failedStage = $(if ($smokePassed) { $null } else { $stage })
    error = $failure
} | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath $resultPath
if ($passed) {
    Write-Host "S3 developer smoke passed. Docker resources and temporary TLS/build files removed. Report: $resultPath"
    exit 0
}
Write-Host "S3 developer smoke failed. Report: $resultPath"
exit 1
