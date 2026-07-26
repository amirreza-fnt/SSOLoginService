#!/bin/bash
set -e

APP_NAME="shahrdari-central-web"
WEB_DIR="/opt/$APP_NAME"
SERVICE_FILE="/etc/systemd/system/$APP_NAME.service"
DOTNET_ENV="Production"

echo "============================================"
echo "  Deploying $APP_NAME (Linux - port 5002)"
echo "============================================"

echo "[1/4] Publishing Web project..."
dotnet publish src/SSOLoginService.Web/SSOLoginService.Web.csproj \
    -c Release \
    -o "$WEB_DIR" \
    --self-contained false

echo "[2/4] Setting up systemd service..."
sudo tee "$SERVICE_FILE" > /dev/null << EOF
[Unit]
Description=$APP_NAME - Shahrdari Central Auth Web UI
After=network.target

[Service]
Type=simple
WorkingDirectory=$WEB_DIR
ExecStart=/usr/bin/dotnet $WEB_DIR/SSOLoginService.Web.dll
Restart=always
RestartSec=10
KillSignal=SIGINT
SyslogIdentifier=$APP_NAME
User=www-data
Environment=ASPNETCORE_ENVIRONMENT=$DOTNET_ENV

[Install]
WantedBy=multi-user.target
EOF

echo "[3/4] Stopping old API service (moved to Windows)..."
sudo systemctl stop shahrdari-central-auth || true
sudo systemctl disable shahrdari-central-auth || true

echo "[4/4] Starting Web service..."
sudo systemctl daemon-reload
sudo systemctl enable "$APP_NAME"
sudo systemctl restart "$APP_NAME"
sudo systemctl status "$APP_NAME" --no-pager

echo ""
echo "============================================"
echo "  Deploy complete!"
echo "  Web UI: http://0.0.0.0:5002"
echo "============================================"
echo ""
echo "Check logs: sudo journalctl -u $APP_NAME -f"
