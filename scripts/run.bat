@echo off
cd /d "%~dp0..\backend"
dotnet run --urls "http://127.0.0.1:5000"
pause
