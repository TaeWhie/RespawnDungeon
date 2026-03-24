@echo off
chcp 65001 >nul
setlocal EnableDelayedExpansion
cd /d "%~dp0"

echo ========================================
echo  GuildDialogue Hub + HuggingFace 실행
echo ========================================
set "HF_ENDPOINT=https://router.huggingface.co/hf-inference/models/black-forest-labs/FLUX.1-schnell"
set "HF_TOKEN="

if not exist "%~dp0Hub\tools" mkdir "%~dp0Hub\tools" >nul 2>nul

if exist "%~dp0Hub\tools\hf_token.local.txt" (
  set /p HF_TOKEN=<"%~dp0Hub\tools\hf_token.local.txt"
)

if "%HF_TOKEN%"=="" (
  echo 오류: HuggingFace 토큰이 없습니다.
  echo 파일을 생성하세요: Hub\tools\hf_token.local.txt
  echo 내용은 토큰 한 줄만 넣으세요.
  pause
  exit /b 1
)

set "HUB_IMAGE_GEN_MODE=hf"
set "HUB_IMAGE_GEN_ENDPOINT=%HF_ENDPOINT%"
set "HUB_IMAGE_GEN_TOKEN=%HF_TOKEN%"
set "HUB_IMAGE_GEN_RETRY=2"
set "HUB_IMAGE_GEN_MAX_CONCURRENCY=1"
set "HUB_IMAGE_CACHE_TTL_HOURS=336"

echo.
echo [ENV]
echo   HUB_IMAGE_GEN_MODE=%HUB_IMAGE_GEN_MODE%
echo   HUB_IMAGE_GEN_ENDPOINT=%HUB_IMAGE_GEN_ENDPOINT%
echo   HUB_IMAGE_GEN_TOKEN=***
echo.

if not exist "GuildDialogue\GuildDialogue.csproj" (
  echo 오류: GuildDialogue\GuildDialogue.csproj 을 찾을 수 없습니다.
  pause
  exit /b 1
)
if not exist "Hub\package.json" (
  echo 오류: Hub\package.json 을 찾을 수 없습니다.
  pause
  exit /b 1
)

echo Hub API 창을 엽니다...
start "GuildDialogue Hub API (HF)" /D "%~dp0GuildDialogue" cmd /k ^
  "set HUB_IMAGE_GEN_MODE=%HUB_IMAGE_GEN_MODE%&& set HUB_IMAGE_GEN_ENDPOINT=%HUB_IMAGE_GEN_ENDPOINT%&& set HUB_IMAGE_GEN_TOKEN=%HUB_IMAGE_GEN_TOKEN%&& set HUB_IMAGE_GEN_RETRY=%HUB_IMAGE_GEN_RETRY%&& set HUB_IMAGE_GEN_MAX_CONCURRENCY=%HUB_IMAGE_GEN_MAX_CONCURRENCY%&& set HUB_IMAGE_CACHE_TTL_HOURS=%HUB_IMAGE_CACHE_TTL_HOURS%&& dotnet run -- --hub-api"

echo 3초 후 Vite 창을 엽니다...
timeout /t 3 /nobreak >nul

echo Vite 개발 서버 창을 엽니다...
start "GuildDialogue Hub Web" /D "%~dp0Hub" cmd /k npm run dev

echo.
echo 준비되면 브라우저에서 열기:  http://localhost:5173
echo Hub API 주소:                http://127.0.0.1:5050
echo.
pause
