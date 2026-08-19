#!/usr/bin/env bash
# ======================================================================
#   ⚡ Model Failover Gateway (macOS / Linux 一键启动 / Starter)
# ======================================================================

DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
BACKEND_DIR="$DIR/backend"

echo "======================================================================"
echo "  ⚡ Model Failover Gateway (macOS / Linux Starting...)"
echo "======================================================================"

if [ -f "$DIR/ModelFailoverGateway" ]; then
    EXE="$DIR/ModelFailoverGateway"
    cd "$DIR"
    chmod +x "$EXE"
    nohup "$EXE" --urls "http://127.0.0.1:5000" > /dev/null 2>&1 &
elif [ -d "$BACKEND_DIR" ]; then
    cd "$BACKEND_DIR"
    nohup dotnet run --urls "http://127.0.0.1:5000" > /dev/null 2>&1 &
else
    echo "[Error] Gateway backend not found!"
    exit 1
fi

sleep 1

# 自动在系统默认浏览器中打开控制台
if command -v open > /dev/null; then
    open "http://127.0.0.1:5000"
elif command -v xdg-open > /dev/null; then
    xdg-open "http://127.0.0.1:5000"
fi

echo ""
echo "  [OK] 网关已在后台运行: http://127.0.0.1:5000"
echo "  [OK] Gateway is running in background at http://127.0.0.1:5000"
echo "======================================================================"
