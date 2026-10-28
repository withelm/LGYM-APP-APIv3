param(
    [string]$WorkingDirectory = (Join-Path ([System.IO.Path]::GetTempPath()) "lgym-payments-proof-preflight-$([Guid]::NewGuid().ToString('N'))")
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$proofScript = Join-Path $PSScriptRoot "run-payments-stripe-sandbox-proof.ps1"
if (-not (Test-Path -LiteralPath $proofScript -PathType Leaf)) {
    throw "Could not find the Payments Stripe sandbox proof script."
}
$pwshPath = (Get-Command pwsh -ErrorAction Stop).Source

$environmentNames = @(
    "PAYMENTS_PROOF_STRIPE_TEST_API_KEY",
    "PAYMENTS_PROOF_STRIPE_TEST_PRICE_ID",
    "PAYMENTS_PROOF_POSTGRES_CONNECTION",
    "PAYMENTS_PROOF_JWT_SIGNING_KEY",
    "PAYMENTS_PROOF_ACCESS_TOKEN"
)
$originalEnvironment = @{}

function Invoke-Proof {
    $output = @(& $pwshPath -NoProfile -File $proofScript 2>&1)
    return [pscustomobject]@{ ExitCode = $LASTEXITCODE; Output = ($output -join [Environment]::NewLine) }
}

function Assert-Blocked {
    param([string]$Name, [string]$ExpectedPrerequisite, [pscustomobject]$Result)

    if ($Result.ExitCode -eq 0) {
        throw "$Name unexpectedly succeeded."
    }
    if (@([regex]::Matches($Result.Output, "(?m)^BLOCKED\b")).Count -ne 1) {
        throw "$Name did not emit exactly one BLOCKED line. Output: $($Result.Output)"
    }
    if (-not $Result.Output.Contains($ExpectedPrerequisite, [StringComparison]::Ordinal)) {
        throw "$Name did not report '$ExpectedPrerequisite' as blocked."
    }
}

function Assert-MissingExecutable {
    param([string]$Command)

    $originalPath = $env:PATH
    try {
        $fakeBin = New-Item -ItemType Directory -Path (Join-Path $WorkingDirectory "fake-$Command") -Force
        foreach ($availableCommand in @("stripe", "node", "psql") | Where-Object { $_ -ne $Command }) {
            Set-Content -LiteralPath (Join-Path $fakeBin.FullName "$availableCommand.cmd") -Value "@echo off`r`nexit /b 0"
        }
        $env:PATH = $fakeBin.FullName
        Assert-Blocked -Name "missing $Command CLI" -ExpectedPrerequisite "$Command CLI" -Result (Invoke-Proof)
    }
    finally {
        $env:PATH = $originalPath
    }
}

try {
    foreach ($name in $environmentNames) {
        $originalEnvironment[$name] = [Environment]::GetEnvironmentVariable($name, "Process")
        [Environment]::SetEnvironmentVariable($name, $null, "Process")
    }
    Assert-Blocked -Name "missing credentials" -ExpectedPrerequisite "PAYMENTS_PROOF_POSTGRES_CONNECTION" -Result (Invoke-Proof)

    foreach ($name in $environmentNames) {
        [Environment]::SetEnvironmentVariable($name, "fixture", "Process")
    }
    [Environment]::SetEnvironmentVariable("PAYMENTS_PROOF_STRIPE_TEST_API_KEY", "sk_test_fixture", "Process")
    [Environment]::SetEnvironmentVariable("PAYMENTS_PROOF_STRIPE_TEST_PRICE_ID", "price_fixture", "Process")
    Assert-MissingExecutable -Command "stripe"
    Assert-MissingExecutable -Command "node"
    Assert-MissingExecutable -Command "psql"

    [Environment]::SetEnvironmentVariable("PAYMENTS_PROOF_STRIPE_TEST_API_KEY", "sk_live_fixture", "Process")
    Assert-Blocked -Name "live Stripe API key" -ExpectedPrerequisite "PAYMENTS_PROOF_STRIPE_TEST_API_KEY" -Result (Invoke-Proof)
    [Environment]::SetEnvironmentVariable("PAYMENTS_PROOF_STRIPE_TEST_API_KEY", "sk_test_", "Process")
    Assert-Blocked -Name "malformed Stripe API key" -ExpectedPrerequisite "PAYMENTS_PROOF_STRIPE_TEST_API_KEY" -Result (Invoke-Proof)
    [Environment]::SetEnvironmentVariable("PAYMENTS_PROOF_STRIPE_TEST_API_KEY", "sk_test_fixture", "Process")
    [Environment]::SetEnvironmentVariable("PAYMENTS_PROOF_STRIPE_TEST_PRICE_ID", "invalid-price", "Process")
    Assert-Blocked -Name "malformed Stripe price" -ExpectedPrerequisite "PAYMENTS_PROOF_STRIPE_TEST_PRICE_ID" -Result (Invoke-Proof)

    Write-Output "Payments Stripe sandbox proof preflight tests passed."
}
finally {
    foreach ($entry in $originalEnvironment.GetEnumerator()) {
        [Environment]::SetEnvironmentVariable($entry.Key, $entry.Value, "Process")
    }
    if (Test-Path -LiteralPath $WorkingDirectory) {
        Remove-Item -LiteralPath $WorkingDirectory -Recurse -Force
    }
}
