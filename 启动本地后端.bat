@echo off
setlocal
title Slime Blind Box - Local Backend

cd /d "%~dp0server"
if errorlevel 1 (
    echo [ERROR] Cannot enter the server directory.
    pause
    exit /b 1
)

where node >nul 2>nul
if errorlevel 1 (
    echo [ERROR] Node.js was not found. Install Node.js 18 or newer first.
    echo https://nodejs.org/
    pause
    exit /b 1
)

if not exist ".env" (
    echo [SETUP] Creating .env...
    copy /y ".env.example" ".env" >nul
    if errorlevel 1 (
        echo [ERROR] Failed to create .env.
        pause
        exit /b 1
    )
)

if not exist "node_modules\" (
    echo [SETUP] Installing backend dependencies...
    call npm install
    if errorlevel 1 (
        echo [ERROR] npm install failed. Check your network and npm settings.
        pause
        exit /b 1
    )
)

echo.
echo [START] Local backend: http://localhost:3000
echo [INFO] Keep this window open. Press Ctrl+C to stop.
echo.
call npm run dev

echo.
echo Backend stopped.
pause
endlocal
