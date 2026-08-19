@echo off
taskkill /F /IM ModelFailoverGateway.exe >nul 2>&1
echo [OK] Model Failover Gateway stopped.
ping 127.0.0.1 -n 2 >nul
