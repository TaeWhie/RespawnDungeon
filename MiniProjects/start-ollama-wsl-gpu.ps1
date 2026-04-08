#Requires -Version 5.1
<#
.SYNOPSIS
  Ensure Ollama runs in WSL2 with GPU-friendly Linux stack; Hub uses http://localhost:11434

.PARAMETER PullDefaultModels
  Pull exaone3.5:7.8b and nomic-embed-text (matches DialogueSettings.json defaults).

.PARAMETER StopWindowsFirst
  Run stop-windows-ollama.ps1 first so Windows Ollama does not hold port 11434.

.EXAMPLE
  .\start-ollama-wsl-gpu.ps1
  .\start-ollama-wsl-gpu.ps1 -PullDefaultModels
  .\start-ollama-wsl-gpu.ps1 -StopWindowsFirst
#>
param(
    [switch]$PullDefaultModels,
    [switch]$StopWindowsFirst
)

$ErrorActionPreference = 'Stop'

if ($StopWindowsFirst) {
    $stopScript = Join-Path $PSScriptRoot 'stop-windows-ollama.ps1'
    if (Test-Path $stopScript) {
        Write-Host 'Running stop-windows-ollama.ps1 ...' -ForegroundColor Cyan
        & $stopScript
        Write-Host ''
    } else {
        Write-Host "Warning: not found: $stopScript" -ForegroundColor Yellow
    }
}

function Test-OllamaHttp {
    try {
        $r = Invoke-WebRequest -Uri 'http://127.0.0.1:11434/api/tags' -UseBasicParsing -TimeoutSec 4
        return $r.StatusCode -eq 200
    } catch {
        return $false
    }
}

Write-Host '========================================' -ForegroundColor Cyan
Write-Host ' WSL2 Ollama (GPU) - readiness' -ForegroundColor Cyan
Write-Host '========================================' -ForegroundColor Cyan

Write-Host ''
Write-Host '[1] WSL + NVIDIA (nvidia-smi inside WSL => GPU available for Ollama)' -ForegroundColor Yellow
wsl -e bash -lc "nvidia-smi -L"

if ($PullDefaultModels) {
    Write-Host ''
    Write-Host '[1b] Pull default models (DialogueSettings.json)' -ForegroundColor Yellow
    wsl -e bash -lc 'set -e; ollama pull exaone3.5:7.8b; ollama pull nomic-embed-text'
}

if (Test-OllamaHttp) {
    Write-Host ''
    Write-Host '[2] Ollama HTTP: OK (http://127.0.0.1:11434)' -ForegroundColor Green
    $tags = Invoke-RestMethod -Uri 'http://127.0.0.1:11434/api/tags' -TimeoutSec 10
    $names = @($tags.models | ForEach-Object { $_.name })
    Write-Host ('  Models: ' + ($names -join ', '))
} else {
    Write-Host ''
    Write-Host '[2] Ollama not responding. Starting ollama serve in WSL...' -ForegroundColor Yellow
    $oneLine = 'if ! pgrep -x ollama >/dev/null 2>&1; then nohup ollama serve >> /tmp/ollama-wsl.log 2>&1 & sleep 2; fi; echo ok'
    wsl -e bash -lc $oneLine
    Start-Sleep -Seconds 2
    if (-not (Test-OllamaHttp)) {
        Write-Host ''
        Write-Host 'Failed: cannot reach http://127.0.0.1:11434' -ForegroundColor Red
        Write-Host ' - Stop Windows Ollama if it binds 11434.' -ForegroundColor Red
        Write-Host ' - Or run manually: wsl -e bash -lc ollama serve' -ForegroundColor Red
        exit 1
    }
    Write-Host ''
    Write-Host '[2] Ollama HTTP: OK' -ForegroundColor Green
}

Write-Host ''
Write-Host '[3] If Ollama misbehaves: ensure only ONE ollama serve in WSL (wsl -e bash -lc "pgrep -a ollama").' -ForegroundColor Yellow
Write-Host '[4] GPU tip: while inferencing, run: wsl -e bash -lc "nvidia-smi"' -ForegroundColor Yellow
Write-Host ''
Write-Host 'Done. Start Hub with run-hub.bat' -ForegroundColor Green
Write-Host ''
