#!/bin/bash
set -euo pipefail

echo "🔐 Generando secretos seguros..."

# Generar contraseña aleatoria para certificados
CERT_PASSWORD=$(openssl rand -base64 32 | tr -d "=+/" | cut -c1-32)

# Generar API Key para admin
ADMIN_API_KEY=$(openssl rand -hex 32)

# Crear archivo .env para broker
cat > docker/broker/.env << EOF
BROKER_CERT_PASSWORD=${CERT_PASSWORD}
ADMIN_API_KEY=${ADMIN_API_KEY}
EOF

# Crear archivo .env para agent
cat > docker/agent/.env << EOF
AGENT_CERT_PASSWORD=${CERT_PASSWORD}
EOF

chmod 600 docker/broker/.env docker/agent/.env

echo "✅ Secretos generados:"
echo "   - docker/broker/.env"
echo "   - docker/agent/.env"
echo ""
echo "⚠️  IMPORTANTE: Estos archivos NO deben subirse a Git"
echo "⚠️  IMPORTANTE: Usa esta contraseña al generar certificados"
echo ""
echo "Contraseña de certificados: ${CERT_PASSWORD}"