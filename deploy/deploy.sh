#!/bin/bash

# ============================================
# Deploy Script - BK.ShahrdariCentralAuth
# ============================================
set -e

APP_NAME="shahrdari-central-auth"
API_DIR="/opt/$APP_NAME/api"
SERVICE_FILE="/etc/systemd/system/$APP_NAME.service"
NGINX_CONF="/etc/nginx/sites-available/$APP_NAME"
DOTNET_ENV="Production"

echo "============================================"
echo "   Deploying $APP_NAME"
echo "============================================"

# 1. Build & Publish
echo "[1/4] Publishing application..."
dotnet publish src/SSOLoginService.Api/SSOLoginService.Api.csproj \
    -c Release \
    -o "$API_DIR" \
    --self-contained false

# 2. Copy nginx config
echo "[2/4] Setting up nginx..."
if [ -f "$NGINX_CONF" ]; then
    echo "  nginx config already exists, skipping..."
else
    sudo cp deploy/nginx.conf "$NGINX_CONF"
    sudo ln -sf "$NGINX_CONF" /etc/nginx/sites-enabled/
fi

# 3. Setup systemd service
echo "[3/4] Setting up systemd service..."
sudo tee "$SERVICE_FILE" > /dev/null << EOF
[Unit]
Description=$APP_NAME - Shahrdari Central Auth SSO
After=network.target

[Service]
Type=simple
WorkingDirectory=$API_DIR
ExecStart=/usr/bin/dotnet $API_DIR/SSOLoginService.Api.dll
Restart=always
RestartSec=10
KillSignal=SIGINT
SyslogIdentifier=$APP_NAME
User=www-data
Environment=ASPNETCORE_ENVIRONMENT=$DOTNET_ENV
Environment=ASPNETCORE_URLS=http://0.0.0.0:5001

# Connection string - set this in production
# Environment=ConnectionStrings__DefaultConnection=Server=...;Database=...;User Id=...;Password=...;

[Install]
WantedBy=multi-user.target
EOF

# 4. Enable & restart service
echo "[4/4] Starting service..."
sudo systemctl daemon-reload
sudo systemctl enable "$APP_NAME"
sudo systemctl restart "$APP_NAME"
sudo systemctl status "$APP_NAME" --no-pager

echo ""
echo "============================================"
echo "   Deploy complete!"
echo "   Service: $APP_NAME"
echo "   API: http://localhost:5001"
echo "============================================"
echo ""
echo "Check logs: sudo journalctl -u $APP_NAME -f"
