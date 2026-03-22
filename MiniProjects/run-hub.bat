@echo off
chcp 65001 >nul
setlocal
cd /d "%~dp0"

echo ========================================
echo  GuildDialogue Hub — 한 번에 실행
echo ========================================
echo [1] Hub API  (dotnet --hub-api, 포트 5050)
echo [2] 웹 UI    (npm run dev, 보통 5173)
echo.

if not exist "GuildDialogue\GuildDialogue.csproj" (
  echo 오류: GuildDialogue\GuildDialogue.csproj 을 찾을 수 없습니다.
  echo 이 .bat 파일은 MiniProjects 폴더에 두고 실행하세요.
  pause
  exit /b 1
)
if not exist "Hub\package.json" (
  echo 오류: Hub\package.json 을 찾을 수 없습니다.
  pause
  exit /b 1
)

echo Hub API 창을 엽니다...
start "GuildDialogue Hub API" /D "%~dp0GuildDialogue" cmd /k dotnet run -- --hub-api

echo 3초 후 Vite 창을 엽니다...
timeout /t 3 /nobreak >nul

echo Vite 개발 서버 창을 엽니다...
start "GuildDialogue Hub Web" /D "%~dp0Hub" cmd /k npm run dev

echo.
echo 준비되면 브라우저에서 열기:  http://localhost:5173
echo Hub API 주소:                http://127.0.0.1:5050
echo.
echo 두 창을 닫으면 각각 종료됩니다.
pause
