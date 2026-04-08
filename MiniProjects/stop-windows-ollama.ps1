#Requires -Version 5.1
<#
.SYNOPSIS
  Stops Windows-native Ollama so http://127.0.0.1:11434 can be used by WSL Ollama only.

.DESCRIPTION
  - Stops ollama.exe processes on Windows.
  - Stops Windows services whose name/display name contains "ollama" (may need Administrator).
  - Prints whether TCP 11434 still has a listener (diagnostic).

.EXAMPLE
  .\stop-windows-ollama.ps1
#>

$ErrorActionPreference = 'Continue'

Write-Host '========================================' -ForegroundColor Cyan
Write-Host ' Stop Windows Ollama (WSL-only 11434)' -ForegroundColor Cyan
Write-Host '========================================' -ForegroundColor Cyan
Write-Host ''

$stoppedProc = 0
Get-Process -Name 'ollama' -ErrorAction SilentlyContinue | ForEach-Object {
    Write-Host ('[process] Stopping PID ' + $_.Id + ' (ollama)') -ForegroundColor Yellow
    Stop-Process -Id $_.Id -Force -ErrorAction SilentlyContinue
    $stoppedProc++
}
if ($stoppedProc -eq 0) {
    Write-Host '[process] No ollama.exe process found.' -ForegroundColor Gray
}

$svcStopped = $false
Get-Service -ErrorAction SilentlyContinue |
    Where-Object { $_.Name -like '*ollama*' -or $_.DisplayName -like '*ollama*' } |
    ForEach-Object {
        if ($_.Status -eq 'Running') {
            Write-Host ('[service] Stopping: ' + $_.Name) -ForegroundColor Yellow
            try {
                Stop-Service -Name $_.Name -Force -ErrorAction Stop
                $svcStopped = $true
            } catch {
                Write-Host '  Failed. Run this script in an elevated PowerShell (Run as Administrator).' -ForegroundColor Red
            }
        }
    }

if (-not $svcStopped) {
    $any = @(Get-Service -ErrorAction SilentlyContinue | Where-Object { $_.Name -like '*ollama*' })
    if ($any.Count -eq 0) {
        Write-Host '[service] No Ollama Windows service found (normal for some installs).' -ForegroundColor Gray
    }
}

Start-Sleep -Seconds 1

Write-Host ''
try {
    $listeners = @(Get-NetTCPConnection -State Listen -LocalPort 11434 -ErrorAction SilentlyContinue)
    if ($listeners.Count -gt 0) {
        Write-Host '[port 11434] Still listening — may be WSL forward or another app:' -ForegroundColor Yellow
        $listeners | Select-Object -First 5 LocalAddress, LocalPort, OwningProcess | Format-Table
        Write-Host 'If OwningProcess is not WSL, close that process or reboot tray Ollama.' -ForegroundColor Gray
    } else {
        Write-Host '[port 11434] No listener on Windows stack (WSL can bind via localhost forward).' -ForegroundColor Green
    }
} catch {
    Write-Host '[port 11434] Could not query (optional).' -ForegroundColor Gray
}

Write-Host ''
Write-Host 'Next: .\start-ollama-wsl-gpu.ps1  then  run-hub.bat' -ForegroundColor Green
Write-Host ''
