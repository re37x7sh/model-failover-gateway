@echo off
cd /d "%~dp0backend"
dotnet run --urls "http://127.0.0.1:5000"
pause
