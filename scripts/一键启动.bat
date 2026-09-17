@echo off
chcp 65001 >nul
title Model Failover Gateway
cd /d "%~dp0"

taskkill /F /IM ModelFailoverGateway.exe >nul 2>&1
if exist "%~dp0ModelFailoverGateway.exe" (
    start "" "%~dp0ModelFailoverGateway.exe"
) else (
    start "" "%~dp0..\backend\bin\Debug\net10.0-windows\ModelFailoverGateway.exe"
)
ping -n 2 127.0.0.1 >nul
start "" "http://127.0.0.1:5000"
