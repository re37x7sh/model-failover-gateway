@echo off
chcp 65001 >nul
title 停止 Model Failover Gateway

taskkill /F /IM ModelFailoverGateway.exe >nul 2>&1

echo ======================================================================
echo  [成功] Model Failover Gateway 服务已停止。
echo ======================================================================
echo.
pause
