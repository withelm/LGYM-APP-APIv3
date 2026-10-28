param(
    [string]$ApiBaseUrl = "http://127.0.0.1:5067",
    [ValidateRange(30, 600)]
    [int]$TimeoutSeconds = 180
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

function Protect-Message {
    param(
        [string]$Message,
        [string[]]$SensitiveValues
    )

    $protectedMessage = $Message
    foreach ($sensitiveValue in $SensitiveValues) {
        if (-not [string]::IsNullOrWhiteSpace($sensitiveValue)) {
            $protectedMessage = $protectedMessage.Replace($sensitiveValue, "[redacted]")
        }
    }

    return $protectedMessage
}

function Get-RequiredEnvironmentValue {
    param([string]$Name)

    return [Environment]::GetEnvironmentVariable($Name, "Process")
}

function Test-StripeTestApiKey {
    param([string]$Value)

    return -not [string]::IsNullOrWhiteSpace($Value) -and $Value -match "^sk_test_[A-Za-z0-9_]+$"
}

function Get-BlockedPrerequisites {
    $blocked = [System.Collections.Generic.List[string]]::new()
    $apiKey = Get-RequiredEnvironmentValue "PAYMENTS_PROOF_STRIPE_TEST_API_KEY"
    $priceId = Get-RequiredEnvironmentValue "PAYMENTS_PROOF_STRIPE_TEST_PRICE_ID"

    foreach ($name in @(
            "PAYMENTS_PROOF_POSTGRES_CONNECTION",
            "PAYMENTS_PROOF_JWT_SIGNING_KEY",
            "PAYMENTS_PROOF_ACCESS_TOKEN")) {
        if ([string]::IsNullOrWhiteSpace((Get-RequiredEnvironmentValue $name))) {
            $blocked.Add($name)
        }
    }

    if (-not (Test-StripeTestApiKey $apiKey)) {
        $blocked.Add("PAYMENTS_PROOF_STRIPE_TEST_API_KEY")
    }

    if ([string]::IsNullOrWhiteSpace($priceId) -or -not $priceId.StartsWith("price_", [StringComparison]::Ordinal)) {
        $blocked.Add("PAYMENTS_PROOF_STRIPE_TEST_PRICE_ID")
    }

    foreach ($command in @("stripe", "node", "psql")) {
        if ($null -eq (Get-Command $command -ErrorAction SilentlyContinue)) {
            $blocked.Add("$command CLI")
        }
    }

    if ($blocked.Count -eq 0) {
        $playwrightProbe = & node -e "require.resolve('playwright')" 2>$null
        if ($LASTEXITCODE -ne 0 -or [string]::IsNullOrWhiteSpace(($playwrightProbe -join ""))) {
            $blocked.Add("Node Playwright module")
        }
    }

    return $blocked
}

function Invoke-ApiRequest {
    param(
        [Parameter(Mandatory)] [System.Net.Http.HttpClient]$Client,
        [Parameter(Mandatory)] [string]$Method,
        [Parameter(Mandatory)] [string]$Uri,
        [hashtable]$Headers = @{},
        [string]$JsonBody = "",
        [int[]]$ExpectedStatusCodes = @(200)
    )

    $request = [System.Net.Http.HttpRequestMessage]::new([System.Net.Http.HttpMethod]::$Method, $Uri)
    foreach ($header in $Headers.GetEnumerator()) {
        $null = $request.Headers.TryAddWithoutValidation($header.Key, [string]$header.Value)
    }
    if (-not [string]::IsNullOrEmpty($JsonBody)) {
        $request.Content = [System.Net.Http.StringContent]::new($JsonBody, [System.Text.Encoding]::UTF8, "application/json")
    }

    $response = $Client.Send($request)
    $content = $response.Content.ReadAsStringAsync().GetAwaiter().GetResult()
    $statusCode = [int]$response.StatusCode
    if ($statusCode -notin $ExpectedStatusCodes) {
        throw "API request $Method $Uri returned HTTP $statusCode."
    }

    return [pscustomobject]@{ StatusCode = $statusCode; Content = $content }
}

function Wait-ForApi {
    param(
        [Parameter(Mandatory)] [System.Diagnostics.Process]$ApiProcess,
        [Parameter(Mandatory)] [System.Net.Http.HttpClient]$Client,
        [Parameter(Mandatory)] [string]$BaseUrl,
        [Parameter(Mandatory)] [int]$Timeout
    )

    $deadline = [DateTimeOffset]::UtcNow.AddSeconds($Timeout)
    do {
        if ($ApiProcess.HasExited) {
            throw "The API exited before the sandbox proof could start."
        }

        try {
            $response = $Client.GetAsync("$BaseUrl/health/live").GetAwaiter().GetResult()
            if ($response.IsSuccessStatusCode) {
                return
            }
        }
        catch {
        }
        Start-Sleep -Milliseconds 500
    } while ([DateTimeOffset]::UtcNow -lt $deadline)

    throw "Timed out waiting for the API health endpoint."
}

function Wait-ForStripeListenerSecret {
    param(
        [Parameter(Mandatory)] [System.Diagnostics.Process]$ListenerProcess,
        [Parameter(Mandatory)] [string]$ListenerLog,
        [Parameter(Mandatory)] [int]$Timeout
    )

    $deadline = [DateTimeOffset]::UtcNow.AddSeconds($Timeout)
    do {
        if ($ListenerProcess.HasExited) {
            throw "The Stripe CLI listener exited before exposing a webhook signing secret."
        }

        $log = if (Test-Path -LiteralPath $ListenerLog) { Get-Content -LiteralPath $ListenerLog -Raw } else { "" }
        $match = [regex]::Match($log, "whsec_[A-Za-z0-9_]+")
        if ($match.Success) {
            return $match.Value
        }
        Start-Sleep -Milliseconds 500
    } while ([DateTimeOffset]::UtcNow -lt $deadline)

    throw "Timed out waiting for the Stripe CLI listener signing secret."
}

function Wait-ForCheckoutStatus {
    param(
        [Parameter(Mandatory)] [System.Net.Http.HttpClient]$Client,
        [Parameter(Mandatory)] [string]$StatusUrl,
        [Parameter(Mandatory)] [hashtable]$Headers,
        [Parameter(Mandatory)] [string]$ExpectedStatus,
        [Parameter(Mandatory)] [int]$Timeout
    )

    $deadline = [DateTimeOffset]::UtcNow.AddSeconds($Timeout)
    do {
        $response = Invoke-ApiRequest -Client $Client -Method Get -Uri $StatusUrl -Headers $Headers
        $status = ($response.Content | ConvertFrom-Json).status
        if ($status -eq $ExpectedStatus) {
            return
        }
        Start-Sleep -Seconds 1
    } while ([DateTimeOffset]::UtcNow -lt $deadline)

    throw "Timed out waiting for checkout status '$ExpectedStatus'."
}

function Get-StripeCheckoutEvent {
    param(
        [Parameter(Mandatory)] [string]$ApiKey,
        [Parameter(Mandatory)] [string]$CheckoutId,
        [Parameter(Mandatory)] [int]$Timeout
    )

    $deadline = [DateTimeOffset]::UtcNow.AddSeconds($Timeout)
    do {
        $eventsOutput = & stripe --api-key $ApiKey events list --limit 100 --type checkout.session.completed 2>$null
        if ($LASTEXITCODE -eq 0) {
            $events = ($eventsOutput -join [Environment]::NewLine) | ConvertFrom-Json
            $event = @($events.data | Where-Object {
                    $_.data.object.metadata.lgym_payment_checkout_id -eq $CheckoutId
                } | Select-Object -First 1)
            if ($event.Count -eq 1) {
                return $event[0]
            }
        }
        Start-Sleep -Seconds 1
    } while ([DateTimeOffset]::UtcNow -lt $deadline)

    throw "Timed out waiting for the Stripe checkout.session.completed event."
}

function Get-StripeSignature {
    param(
        [Parameter(Mandatory)] [string]$Payload,
        [Parameter(Mandatory)] [string]$WebhookSigningSecret
    )

    $timestamp = [DateTimeOffset]::UtcNow.ToUnixTimeSeconds()
    $signingPayload = "$timestamp.$Payload"
    $hmac = [System.Security.Cryptography.HMACSHA256]::new([System.Text.Encoding]::UTF8.GetBytes($WebhookSigningSecret))
    try {
        $signature = [Convert]::ToHexStringLower($hmac.ComputeHash([System.Text.Encoding]::UTF8.GetBytes($signingPayload)))
    }
    finally {
        $hmac.Dispose()
    }

    return "t=$timestamp,v1=$signature"
}

$blocked = @(Get-BlockedPrerequisites)
if ($blocked.Count -ne 0) {
    Write-Output "BLOCKED Payments Stripe sandbox proof prerequisites unavailable: $($blocked -join ', ')."
    exit 2
}

$repositoryRoot = Split-Path -Parent $PSScriptRoot
$apiProject = Join-Path $repositoryRoot "LgymApi.Api/LgymApi.Api.csproj"
if (-not (Test-Path -LiteralPath $apiProject -PathType Leaf)) {
    Write-Output "BLOCKED Payments Stripe sandbox proof must run from a repository checkout."
    exit 2
}

$stripeApiKey = Get-RequiredEnvironmentValue "PAYMENTS_PROOF_STRIPE_TEST_API_KEY"
$priceId = Get-RequiredEnvironmentValue "PAYMENTS_PROOF_STRIPE_TEST_PRICE_ID"
$connectionString = Get-RequiredEnvironmentValue "PAYMENTS_PROOF_POSTGRES_CONNECTION"
$jwtSigningKey = Get-RequiredEnvironmentValue "PAYMENTS_PROOF_JWT_SIGNING_KEY"
$accessToken = Get-RequiredEnvironmentValue "PAYMENTS_PROOF_ACCESS_TOKEN"
$customerEmail = "payments-proof-$([Guid]::NewGuid().ToString('N'))@example.test"
$sensitiveValues = @($stripeApiKey, $connectionString, $jwtSigningKey, $accessToken)
$artifactDirectory = Join-Path ([System.IO.Path]::GetTempPath()) "lgym-payments-proof-$([Guid]::NewGuid().ToString('N'))"
$listenerProcess = $null
$apiProcess = $null
$httpClient = [System.Net.Http.HttpClient]::new()
$httpClient.Timeout = [TimeSpan]::FromSeconds(30)
$originalEnvironment = @{}
$environmentOverrides = @{
    "ASPNETCORE_ENVIRONMENT" = "Development"
    "ASPNETCORE_URLS" = $ApiBaseUrl
    "ConnectionStrings__Postgres" = $connectionString
    "Jwt__SigningKey" = $jwtSigningKey
    "Cors__AllowedOrigins__0" = $ApiBaseUrl
    "Payments__Enabled" = "true"
    "Payments__PriceId" = $priceId
    "Payments__SuccessUrl" = "$ApiBaseUrl/payments-proof/success"
    "Payments__CancelUrl" = "$ApiBaseUrl/payments-proof/cancel"
    "Payments__ApiKey" = $stripeApiKey
}
$exitCode = 0

try {
    $null = New-Item -ItemType Directory -Path $artifactDirectory -Force
    $listenerLog = Join-Path $artifactDirectory "stripe-listener.log"
    $listenerErrorLog = Join-Path $artifactDirectory "stripe-listener-error.log"
    $apiLog = Join-Path $artifactDirectory "api.log"
    $apiErrorLog = Join-Path $artifactDirectory "api-error.log"
    $playwrightScript = Join-Path $artifactDirectory "complete-checkout.js"

    $stripeProbe = & stripe --api-key $stripeApiKey events list --limit 1 2>$null
    if ($LASTEXITCODE -ne 0) {
        throw "Stripe CLI could not authenticate with the supplied test API key."
    }

    $listenerProcess = Start-Process -FilePath "stripe" -ArgumentList @(
        "--api-key", $stripeApiKey,
        "listen",
        "--events", "checkout.session.completed",
        "--forward-to", "$ApiBaseUrl/api/payments/webhooks/stripe"
    ) -RedirectStandardOutput $listenerLog -RedirectStandardError $listenerErrorLog -PassThru
    $webhookSigningSecret = Wait-ForStripeListenerSecret -ListenerProcess $listenerProcess -ListenerLog $listenerLog -Timeout $TimeoutSeconds
    $environmentOverrides["Payments__WebhookSigningSecret"] = $webhookSigningSecret
    $sensitiveValues += $webhookSigningSecret

    foreach ($entry in $environmentOverrides.GetEnumerator()) {
        $originalEnvironment[$entry.Key] = [Environment]::GetEnvironmentVariable($entry.Key, "Process")
        [Environment]::SetEnvironmentVariable($entry.Key, $entry.Value, "Process")
    }

    $apiProcess = Start-Process -FilePath "dotnet" -ArgumentList @("run", "--no-build", "--project", $apiProject) -WorkingDirectory $repositoryRoot -RedirectStandardOutput $apiLog -RedirectStandardError $apiErrorLog -PassThru
    Wait-ForApi -ApiProcess $apiProcess -Client $httpClient -BaseUrl $ApiBaseUrl -Timeout $TimeoutSeconds

    $headers = @{ Authorization = "Bearer $accessToken"; "Idempotency-Key" = "payments-proof-$([Guid]::NewGuid().ToString('N'))" }
    $created = Invoke-ApiRequest -Client $httpClient -Method Post -Uri "$ApiBaseUrl/api/payments/checkout" -Headers $headers -JsonBody "{}" -ExpectedStatusCodes @(201)
    $checkout = $created.Content | ConvertFrom-Json
    if ([string]::IsNullOrWhiteSpace($checkout.checkoutId) -or [string]::IsNullOrWhiteSpace($checkout.checkoutUrl)) {
        throw "The checkout response did not contain the expected provider-neutral fields."
    }

    $statusUrl = "$ApiBaseUrl/api/payments/checkouts/$($checkout.checkoutId)"
    Wait-ForCheckoutStatus -Client $httpClient -StatusUrl $statusUrl -Headers @{ Authorization = "Bearer $accessToken" } -ExpectedStatus "Created" -Timeout 30

    @'
const { chromium } = require('playwright');

const checkoutUrl = process.env.PAYMENTS_PROOF_CHECKOUT_URL;
const customerEmail = process.env.PAYMENTS_PROOF_CUSTOMER_EMAIL;
const testCard = '4242424242424242';
const timeout = 30000;

async function fillInAnyFrame(page, selectors, value, required) {
  for (const frame of page.frames()) {
    for (const selector of selectors) {
      const locator = frame.locator(selector).first();
      if (await locator.count() > 0) {
        await locator.fill(value, { timeout });
        return true;
      }
    }
  }
  if (required) throw new Error(`Could not find checkout field: ${selectors.join(', ')}`);
  return false;
}

async function clickInAnyFrame(page, selectors) {
  for (const frame of page.frames()) {
    for (const selector of selectors) {
      const locator = frame.locator(selector).first();
      if (await locator.count() > 0) {
        await locator.click({ timeout });
        return;
      }
    }
  }
  throw new Error('Could not find the Stripe Checkout submit button.');
}

(async () => {
  const browser = await chromium.launch({ headless: true });
  try {
    const page = await browser.newPage();
    await page.goto(checkoutUrl, { waitUntil: 'domcontentloaded', timeout });
    await fillInAnyFrame(page, ['input[type="email"]', 'input[name="email"]'], customerEmail, false);
    await fillInAnyFrame(page, ['input[name="cardNumber"]', 'input[name="cardnumber"]', 'input[autocomplete="cc-number"]'], testCard, true);
    await fillInAnyFrame(page, ['input[name="cardExpiry"]', 'input[name="exp-date"]', 'input[autocomplete="cc-exp"]'], '12/34', true);
    await fillInAnyFrame(page, ['input[name="cardCvc"]', 'input[name="cvc"]', 'input[autocomplete="cc-csc"]'], '123', true);
    await clickInAnyFrame(page, ['button[type="submit"]', 'button:has-text("Pay")', 'button:has-text("Submit")']);
    await page.waitForTimeout(1500);
  } finally {
    await browser.close();
  }
})().catch(error => {
  console.error(error.stack || error.message);
  process.exit(1);
});
'@ | Set-Content -LiteralPath $playwrightScript -NoNewline

    $env:PAYMENTS_PROOF_CHECKOUT_URL = $checkout.checkoutUrl
    $env:PAYMENTS_PROOF_CUSTOMER_EMAIL = $customerEmail
    & node $playwrightScript
    if ($LASTEXITCODE -ne 0) {
        throw "Playwright could not complete the Stripe test checkout."
    }

    Wait-ForCheckoutStatus -Client $httpClient -StatusUrl $statusUrl -Headers @{ Authorization = "Bearer $accessToken" } -ExpectedStatus "Paid" -Timeout $TimeoutSeconds
    $stripeEvent = Get-StripeCheckoutEvent -ApiKey $stripeApiKey -CheckoutId $checkout.checkoutId -Timeout $TimeoutSeconds
    if ($stripeEvent.data.object.customer_details.email -ne $customerEmail) {
        throw "The completed Stripe event did not retain the proof checkout email."
    }
    $eventOutput = & stripe --api-key $stripeApiKey events retrieve $stripeEvent.id 2>$null
    if ($LASTEXITCODE -ne 0) {
        throw "Stripe CLI could not retrieve the completed checkout event for replay."
    }
    $eventPayload = $eventOutput -join [Environment]::NewLine
    $signature = Get-StripeSignature -Payload $eventPayload -WebhookSigningSecret $webhookSigningSecret
    $replay = Invoke-ApiRequest -Client $httpClient -Method Post -Uri "$ApiBaseUrl/api/payments/webhooks/stripe" -Headers @{ "Stripe-Signature" = $signature } -JsonBody $eventPayload
    if ($replay.StatusCode -ne 200) {
        throw "The replayed Stripe event was not accepted."
    }
    Wait-ForCheckoutStatus -Client $httpClient -StatusUrl $statusUrl -Headers @{ Authorization = "Bearer $accessToken" } -ExpectedStatus "Paid" -Timeout 30

    $databaseProof = @(& psql -X --tuples-only --no-align --set ON_ERROR_STOP=1 --dbname=$connectionString --set "checkout_id=$($checkout.checkoutId)" --set "event_id=$($stripeEvent.id)" --command 'SELECT (SELECT count(*) FROM "PaymentCheckouts" WHERE "Id" = :''checkout_id''::uuid), (SELECT count(*) FROM "PaymentCheckouts" WHERE "Id" = :''checkout_id''::uuid AND "Status" = 1), (SELECT count(*) FROM "PaymentEventReceipts" WHERE "ProviderEventId" = :''event_id''), (SELECT count(*) FROM "PaymentEventReceipts" WHERE "ProviderEventId" = :''event_id'' AND "Status" = 1);' 2>$null)
    if (($databaseProof -join "").Trim() -ne "1|1|1|1") {
        throw "PostgreSQL proof did not find exactly one paid checkout and one processed event receipt after replay."
    }

    $capturedApiLogs = @(
        @($apiLog, $apiErrorLog) |
            Where-Object { Test-Path -LiteralPath $_ } |
            ForEach-Object { Get-Content -LiteralPath $_ -Raw }
    ) -join [Environment]::NewLine
    foreach ($sensitiveValue in $sensitiveValues) {
        if ($capturedApiLogs.Contains($sensitiveValue, [StringComparison]::Ordinal)) {
            throw "Captured API logs contain a supplied sensitive value."
        }
    }

    Write-Output "Payments Stripe sandbox proof passed: Created -> Paid, replay retained one paid checkout and one processed receipt, and API logs contain no supplied sensitive values."
}
catch {
    Write-Error (Protect-Message -Message "Payments Stripe sandbox proof failed: $($_.Exception.Message)" -SensitiveValues $sensitiveValues)
    $exitCode = 1
}
finally {
    if ($null -ne $apiProcess -and -not $apiProcess.HasExited) {
        Stop-Process -Id $apiProcess.Id -Force
    }
    if ($null -ne $listenerProcess -and -not $listenerProcess.HasExited) {
        Stop-Process -Id $listenerProcess.Id -Force
    }
    foreach ($entry in $originalEnvironment.GetEnumerator()) {
        [Environment]::SetEnvironmentVariable($entry.Key, $entry.Value, "Process")
    }
    [Environment]::SetEnvironmentVariable("PAYMENTS_PROOF_CHECKOUT_URL", $null, "Process")
    [Environment]::SetEnvironmentVariable("PAYMENTS_PROOF_CUSTOMER_EMAIL", $null, "Process")
    $httpClient.Dispose()
    if (Test-Path -LiteralPath $artifactDirectory) {
        Remove-Item -LiteralPath $artifactDirectory -Recurse -Force
    }
}

exit $exitCode
