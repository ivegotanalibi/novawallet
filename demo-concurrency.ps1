# ─────────────────────────────────────────────────────────────────────
#  NovaWallet — Live Concurrency Demo
#
#  Scenario : Fund a wallet with N10,000, then fire 20 SIMULTANEOUS
#             N1,000 transfers from 20 different users.
#             N20,000 of demand against N10,000 of funds.
#  Expected : EXACTLY 10 succeed (200), EXACTLY 10 rejected (422
#             Insufficient Funds), no kobo created or destroyed.
#
#  Prereqs  : MySQL up  ->  docker compose up mysql -d
#             API up    ->  dotnet run   (in src\NovaWallet.Ledger.Api)
#
#  Run      ->  powershell -ExecutionPolicy Bypass -File .\demo-concurrency.ps1
#
#  Re-runnable: every run creates fresh wallets, so run it as many
#  times as you like — old demo wallets are harmless leftovers.
# ─────────────────────────────────────────────────────────────────────

$base = "http://localhost:5099"
$ErrorActionPreference = "Stop"
$runId = [Guid]::NewGuid().ToString("N").Substring(0, 8)

# ── 0. Pre-flight: is the API (and its DB health check) reachable? ──
try {
    Invoke-RestMethod -Uri "$base/health" -Method GET | Out-Null
} catch {
    Write-Host "Pre-flight failed - API not reachable at $base/health" -ForegroundColor Red
    Write-Host "  1. MySQL : docker compose up mysql -d      (wait for 'healthy')"
    Write-Host "  2. API   : cd 'c:\NovaWallet Ledger Service\src\NovaWallet.Ledger.Api'"
    Write-Host "             dotnet run"
    exit 1
}

Write-Host ""
Write-Host "======== NovaWallet Concurrency Demo ========" -ForegroundColor Cyan

# ── 1. Arrange: create two wallets, fund the source with N10,000 ──
$setupToken = (Invoke-RestMethod -Uri "$base/api/auth/token" -Method POST -ContentType "application/json" `
        -Body ('{{"customerId":"DEMO-SETUP-{0}","customerName":"Demo"}}' -f $runId)).token
$auth = @{ Authorization = "Bearer $setupToken" }

$walletA = (Invoke-RestMethod -Uri "$base/api/wallets" -Method POST -ContentType "application/json" -Headers $auth `
        -Body ('{{"customerId":"DEMO-SOURCE-{0}"}}' -f $runId)).walletId
$walletB = (Invoke-RestMethod -Uri "$base/api/wallets" -Method POST -ContentType "application/json" -Headers $auth `
        -Body ('{{"customerId":"DEMO-RECEIVER-{0}"}}' -f $runId)).walletId

Invoke-RestMethod -Uri "$base/api/wallets/$walletA/credit" -Method POST -ContentType "application/json" -Headers $auth `
    -Body '{"amountKobo":1000000,"reference":"FUND-DEMO"}' | Out-Null

Write-Host ""
Write-Host ("Source wallet funded with N10,000.00 : {0}" -f $walletA)
Write-Host ("Receiver wallet                      : {0}" -f $walletB)
Write-Host ""
Write-Host "Firing 20 SIMULTANEOUS transfers of N1,000 each..." -ForegroundColor Yellow
Write-Host "(N20,000 of demand vs N10,000 of funds)" -ForegroundColor Yellow
Write-Host ""

# ── 2. 20 distinct user tokens — one per request, so per-user
#       throttling is not involved. This isolates the LOCKING. ──
$tokens = @(1..20 | ForEach-Object {
        (Invoke-RestMethod -Uri "$base/api/auth/token" -Method POST -ContentType "application/json" `
                -Body ('{{"customerId":"DEMO-USER-{0}-{1}","customerName":"U{0}"}}' -f $_, $runId)).token
    })

# ── 3. Act: fire all 20 at once via HttpClient (truly parallel) ──
Add-Type -AssemblyName System.Net.Http
[System.Net.ServicePointManager]::DefaultConnectionLimit = 100

$httpClient = New-Object System.Net.Http.HttpClient
$tasks = for ($i = 1; $i -le 20; $i++) {
    $req = New-Object System.Net.Http.HttpRequestMessage([System.Net.Http.HttpMethod]::Post, "$base/api/wallets/$walletA/transfer")
    $req.Headers.Authorization = New-Object System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", $tokens[$i - 1])
    $req.Headers.Add("Idempotency-Key", "demo-$runId-$i")
    $body = '{{"toWalletId":"{0}","amountKobo":100000,"reference":"DEMO-{1}"}}' -f $walletB, $i
    $req.Content = New-Object System.Net.Http.StringContent($body, [System.Text.Encoding]::UTF8, "application/json")
    $httpClient.SendAsync($req)
}
[System.Threading.Tasks.Task]::WaitAll([System.Threading.Tasks.Task[]]$tasks)

# ── 4. Report ──
$codes = @($tasks | ForEach-Object { [int]$_.Result.StatusCode })
for ($i = 0; $i -lt 20; $i++) {
    if ($codes[$i] -eq 200) {
        Write-Host ("Request {0,2}  ->  200  (transferred)" -f ($i + 1)) -ForegroundColor Green
    } else {
        Write-Host ("Request {0,2}  ->  {1}  (rejected: insufficient funds)" -f ($i + 1), $codes[$i]) -ForegroundColor DarkYellow
    }
}

$ok       = @($codes | Where-Object { $_ -eq 200 }).Count
$rejected = @($codes | Where-Object { $_ -eq 422 }).Count
$other    = @($codes | Where-Object { $_ -ne 200 -and $_ -ne 422 }).Count

# ── 5. Assert: money is conserved ──
$balA = (Invoke-RestMethod -Uri "$base/api/wallets/$walletA/balance" -Headers $auth).balanceKobo
$balB = (Invoke-RestMethod -Uri "$base/api/wallets/$walletB/balance" -Headers $auth).balanceKobo

Write-Host ""
Write-Host ("Succeeded (200): {0}     Rejected (422): {1}     Unexpected: {2}" -f $ok, $rejected, $other) -ForegroundColor Cyan
Write-Host ("Source balance   : N{0:N2}" -f ($balA / 100))
Write-Host ("Receiver balance : N{0:N2}" -f ($balB / 100))
Write-Host ("Total            : N{0:N2}   (must equal the original N10,000.00)" -f (($balA + $balB) / 100)) -ForegroundColor Cyan
Write-Host ""

if ($ok -eq 10 -and $rejected -eq 10 -and $other -eq 0 -and ($balA + $balB) -eq 1000000 -and $balA -ge 0) {
    Write-Host "RESULT: Exactly 10 of 20 succeeded. No double-spend. No negative balance. Money conserved." -ForegroundColor Green
} else {
    Write-Host "RESULT: Unexpected outcome - inspect the API logs." -ForegroundColor Red
}
