#!/bin/bash
set -euo pipefail

# --- Funciones de Logging ---
GREEN='\033[0;32m'; YELLOW='\033[1;33m'; RED='\033[0;31m'; NC='\033[0m'
log_info() { echo -e "${GREEN}[INFO]${NC} $1"; }
log_error() { echo -e "${RED}[ERROR]${NC} $1"; }

# --- Validar argumento ---
if [ -z "$1" ]; then log_error "Uso: ./deploy-broker.sh usuario@ip_del_vps"; exit 1; fi
VPS_HOST="$1"
log_info "🎯 Desplegando Broker en VPS: ${VPS_HOST}"
cd "$(dirname "${BASH_SOURCE[0]}")/.."

# --- Leer secretos localmente ---
log_info "🔑 Leyendo secretos del archivo .env local..."
ENV_FILE="docker/broker/.env"
if [ ! -f "$ENV_FILE" ]; then log_error "Archivo .env no encontrado en $ENV_FILE. Ejecuta ./scripts/generate-secrets.sh"; exit 1; fi
export $(grep -v '^#' $ENV_FILE | xargs)
if [ -z "${BROKER_CERT_PASSWORD}" ]; then log_error "Variable BROKER_CERT_PASSWORD no encontrada en .env"; exit 1; fi
log_info "✅ Secretos cargados."

# --- Empaquetar y Subir ---
log_info "📦 Creando paquete de deployment..."
tar --exclude='.git' -czf /tmp/broker-deploy.tar.gz .

log_info "🚀 Subiendo paquete al VPS..."
scp /tmp/broker-deploy.tar.gz "${VPS_HOST}:/tmp/"

# --- Despliegue Remoto ---
log_info "⚙️  Configurando y desplegando en el VPS..."
ssh "$VPS_HOST" "BROKER_CERT_PASSWORD='${BROKER_CERT_PASSWORD}' ADMIN_API_KEY='${ADMIN_API_KEY}'" << 'ENDSSH'
set -e

echo "--- [VPS] Instalando Docker si es necesario ---"
if ! command -v docker &> /dev/null; then
    curl -fsSL https://get.docker.com | sh
fi

echo "--- [VPS] Configurando entorno ---"
rm -rf ~/tfm-selfhosting && mkdir -p ~/tfm-selfhosting
tar xzf /tmp/broker-deploy.tar.gz -C ~/tfm-selfhosting

cd ~/tfm-selfhosting

echo "--- [VPS] Configurando permisos ---"
# Los certificados deben ser legibles por el usuario no-root del contenedor
chmod 644 certs/*.pfx certs/*.crt 2>/dev/null || true
# El directorio config debe ser escribible para que el Broker pueda actualizar domains.json
# (registro automatico de dominios via POST /admin/routes)
chmod 777 config/
chmod 666 config/domains.json 2>/dev/null || true

echo "--- [VPS] Desplegando servicios con Docker Compose ---"
docker compose -f docker/vps-complete.yml up -d --build --force-recreate

echo "--- [VPS] Verificando estado (espera 20s)... ---"
sleep 20
docker compose -f docker/vps-complete.yml ps
echo "--- [VPS] Revisando logs del Broker... ---"
docker logs tfm-broker --tail 50
ENDSSH

log_info "✅ Proceso de despliegue finalizado."
rm /tmp/broker-deploy.tar.gz
