param(
    [string]$Endpoint = "",
    [string]$Token = "",
    [int]$Retry = 2,
    [int]$MaxConcurrency = 1,
    [int]$CacheTtlHours = 336
)

$ErrorActionPreference = "Stop"

if ([string]::IsNullOrWhiteSpace($Endpoint)) {
    $Endpoint = Read-Host "HUB_IMAGE_GEN_ENDPOINT (e.g. https://xxxx.ngrok-free.app/generate)"
}

if ([string]::IsNullOrWhiteSpace($Token)) {
    $Token = Read-Host "HUB_IMAGE_GEN_TOKEN (optional, press Enter to skip)"
}

if ([string]::IsNullOrWhiteSpace($Endpoint)) {
    Write-Error "Endpoint is required."
}

$env:HUB_IMAGE_GEN_ENDPOINT = $Endpoint.Trim()
if ([string]::IsNullOrWhiteSpace($Token)) {
    Remove-Item Env:HUB_IMAGE_GEN_TOKEN -ErrorAction SilentlyContinue
} else {
    $env:HUB_IMAGE_GEN_TOKEN = $Token.Trim()
}
$env:HUB_IMAGE_GEN_RETRY = [string]$Retry
$env:HUB_IMAGE_GEN_MAX_CONCURRENCY = [string]$MaxConcurrency
$env:HUB_IMAGE_CACHE_TTL_HOURS = [string]$CacheTtlHours

Write-Host "[Hub API] Starting with Flux endpoint: $($env:HUB_IMAGE_GEN_ENDPOINT)"
dotnet run -- --hub-api
