#!/bin/bash
set -e
echo "╔════════════════════════════════════════════════════╗"
echo "║ Setting up SelfHosting Agent ║"
echo "╚════════════════════════════════════════════════════╝"
echo ""
# Crear directorios
echo "[1/5] Creating directories..."
sudo mkdir -p /opt/selfhosting/{certs,config,logs}
# Copiar certificados
echo "[2/5] Copying certificates..."
sudo cp ../certs/agent.pfx /opt/selfhosting/certs/
sudo cp ../certs/ca.crt /opt/selfhosting/certs/
sudo chmod 600 /opt/selfhosting/certs/*
# Crear configuración
echo "[3/5] Creating configuration..."
cat > /tmp/agent-config.json << 'ENDCONFIG'
{
"Agent": {
"AgentId": "my-agent",
"BrokerUrl": "https://YOUR_VPS_IP:50051",
"CertificatePath": "/opt/selfhosting/certs/agent.pfx",
"CertificatePassword": "dev123",
"CaCertPath": "/opt/selfhosting/certs/ca.crt",
"TraefikUrl": "http://localhost:80",
"HeartbeatIntervalSeconds": 30
}
}
ENDCONFIG
sudo mv /tmp/agent-config.json /opt/selfhosting/config/appsettings.Production.json
# Compilar agent
echo "[4/5] Building agent..."
cd ../src/TFM.Agent
dotnet publish -c Release -o /tmp/agent-publish
# Copiar binarios
sudo cp -r /tmp/agent-publish/* /opt/selfhosting/
sudo chmod +x /opt/selfhosting/TFM.Agent
# Crear servicio systemd
echo "[5/5] Creating systemd service..."
sudo tee /etc/systemd/system/selfhosting-agent.service > /dev/null << 'ENDSERVICE'
[Unit]
Description=SelfHosting Agent
After=network.target docker.service
Wants=docker.service
[Service]
Type=notify
WorkingDirectory=/opt/selfhosting
ExecStart=/opt/selfhosting/TFM.Agent
Restart=always
RestartSec=10
Environment="DOTNET_ENVIRONMENT=Production"
StandardOutput=journal
StandardError=journal
[Install]
WantedBy=multi-user.target
ENDSERVICE
sudo systemctl daemon-reload
sudo systemctl enable selfhosting-agent
echo ""
echo "􀀀 Setup complete!"
echo ""
echo "Next steps:"
echo " 1. Edit /opt/selfhosting/config/appsettings.Production.json"
echo " - Update BrokerUrl with your VPS IP"
echo " 2. Start agent: sudo systemctl start selfhosting-agent"
echo " 3. Check logs: sudo journalctl -u selfhosting-agent -f"
echo ""
