@echo off
chcp 65001 >nul
title Model Failover Gateway 一键绿色发布打包

echo ======================================================================
echo   正在一键编译并发布 Model Failover Gateway 独立绿色分发包
echo   (打包后无需安装 .NET SDK、Node.js，双击直接运行)
echo ======================================================================
echo.

cd /d "%~dp0frontend"
echo [1/3] 正在编译前端 Vue 静态资源 (npm run build)...
call npm run build
if %errorlevel% neq 0 (
    echo [错误] 前端打包失败！
    pause
    exit /b %errorlevel%
)

cd /d "%~dp0backend"
echo.
echo [2/3] 正在发布 .NET 独立单文件可执行程序 (dotnet publish)...
dotnet publish -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -o "%~dp0dist"
if %errorlevel% neq 0 (
    echo [错误] 后端发布失败！
    pause
    exit /b %errorlevel%
)

cd /d "%~dp0"
echo.
echo [3/3] 正在复制常用启动与管理脚本到 dist 目录...
copy /y "run-ui.vbs" "dist\run-ui.vbs" >nul
copy /y "run-tray.vbs" "dist\run-tray.vbs" >nul
copy /y "create-shortcut.ps1" "dist\create-shortcut.ps1" >nul
copy /y "create-startup.ps1" "dist\create-startup.ps1" >nul
copy /y "remove-startup.ps1" "dist\remove-startup.ps1" >nul
copy /y "创建桌面快捷方式.bat" "dist\创建桌面快捷方式.bat" >nul
copy /y "一键设置开机自启.bat" "dist\一键设置开机自启.bat" >nul
copy /y "一键取消开机自启.bat" "dist\一键取消开机自启.bat" >nul
copy /y "停止服务.bat" "dist\停止服务.bat" >nul

echo.
echo ======================================================================
echo  [大功告成] 独立绿色版已成功打包至 dist 目录！
echo  
echo  说明：
echo  1. 你可以直接将 dist 文件夹打包为 zip 发送给任何 Windows 用户；
echo  2. 对方电脑无需安装任何环境，双击「创建桌面快捷方式.bat」即可一键在桌面生成入口！
echo ======================================================================
echo.
pause
