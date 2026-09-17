@echo off
chcp 65001 >nul
title Model Failover Gateway 一键绿色发布打包

echo ======================================================================
echo   正在一键编译并发布 Model Failover Gateway 独立绿色分发包
echo   (打包后无需安装 .NET SDK、Node.js，双击直接运行)
echo ======================================================================
echo.

set "ROOT_DIR=%~dp0.."
pushd "%ROOT_DIR%"
set "ROOT_DIR=%CD%"
popd
set "DIST_DIR=%ROOT_DIR%\dist"

cd /d "%ROOT_DIR%\frontend"
echo [1/3] 正在编译前端 Vue 静态资源...

if not exist "node_modules\vite\bin\vite.js" (
    echo [提示] 检测到尚未安装前端依赖，正在执行 npm install...
    call npm install
    if %errorlevel% neq 0 (
        echo [错误] 前端依赖安装失败！
        pause
        exit /b %errorlevel%
    )
)

if exist "node_modules\vite\bin\vite.js" (
    call node "node_modules\vite\bin\vite.js" build
) else (
    call npm run build
)
if %errorlevel% neq 0 (
    echo [错误] 前端打包失败！
    pause
    exit /b %errorlevel%
)

cd /d "%ROOT_DIR%\backend"
echo.
echo [2/3] 正在发布 .NET 独立单文件可执行程序 (dotnet publish)...
dotnet publish -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -o "%DIST_DIR%"
if %errorlevel% neq 0 (
    echo [错误] 后端发布失败！
    pause
    exit /b %errorlevel%
)

cd /d "%~dp0"
echo.
echo [3/3] 正在复制常用启动与管理脚本到 dist 目录...
copy /y "一键启动.bat" "%DIST_DIR%\一键启动.bat" >nul 2>nul
copy /y "run-ui.vbs" "%DIST_DIR%\run-ui.vbs" >nul
copy /y "run-tray.vbs" "%DIST_DIR%\run-tray.vbs" >nul
copy /y "create-shortcut.ps1" "%DIST_DIR%\create-shortcut.ps1" >nul
copy /y "create-startup.ps1" "%DIST_DIR%\create-startup.ps1" >nul
copy /y "remove-startup.ps1" "%DIST_DIR%\remove-startup.ps1" >nul
copy /y "创建桌面快捷方式.bat" "%DIST_DIR%\创建桌面快捷方式.bat" >nul
copy /y "一键设置开机自启.bat" "%DIST_DIR%\一键设置开机自启.bat" >nul
copy /y "一键取消开机自启.bat" "%DIST_DIR%\一键取消开机自启.bat" >nul
copy /y "停止服务.bat" "%DIST_DIR%\停止服务.bat" >nul

echo.
echo ======================================================================
echo  [大功告成] 独立绿色版已成功打包至 dist 目录！
echo.
echo  使用指南：
echo  1. 你可以直接将根目录下的 dist 文件夹压缩打包为 zip 分发；
echo  2. 目标电脑无需安装任何环境，双击【一键启动.bat】直接运行；
echo  3. 也可以双击【创建桌面快捷方式.bat】生成桌面图标。
echo ======================================================================
echo.
pause
