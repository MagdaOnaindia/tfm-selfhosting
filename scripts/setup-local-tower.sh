#!/bin/bash
set -euo pipefail

# --- Configuración de Variables y Funciones de Logging ---
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
RED='\033[0;31m'
BLUE='\033[0;34m'
NC='\033[0m'

log_info() { echo -e "${GREEN}[INFO]${NC} $1"; }
log_warn() { echo -e "${YELLOW}[WARN]${NC} $1"; }
log_error() { echo -e "${RED}[ERROR]${NC} $1"; }
log_step() { echo -e "\n${BLUE}--- $1 ---${NC}"; }

# --- Verificación de Permisos ---
if [ "$EUID" -eq 0 ]; then
  log_error "No ejecutes este script como root directamente. Ejecútalo como tu usuario normal (ej. 'magda'). Te pedirá la contraseña de sudo cuando sea necesario."
  exit 1
fi

echo "╔══════════════════════════════════════════════════════════╗"
echo "║ TFM Self-Hosting - Asistente de Configuración de la Torre ║"
echo "╚══════════════════════════════════════════════════════════╝"

# ==============================================================================
# --- FASE 1: PROVISIONING DEL SERVIDOR BASE ---
# ==============================================================================
log_step "FASE 1: Aprovisionando el servidor base..."

# 1.1 --- Actualizar Sistema e Instalar Prerrequisitos ---
log_info "Actualizando el sistema e instalando herramientas (git, docker, ansible)..."
sudo apt-get update
sudo apt-get install -y ca-certificates curl gnupg git ansible

# Añadir repositorio oficial de Docker
sudo install -m 0755 -d /etc/apt/keyrings
curl -fsSL https://download.docker.com/linux/ubuntu/gpg | sudo gpg --dearmor -o /etc/apt/keyrings/docker.gpg
sudo chmod a+r /etc/apt/keyrings/docker.gpg
echo \
  "deb [arch=$(dpkg --print-architecture) signed-by=/etc/apt/keyrings/docker.gpg] https://download.docker.com/linux/ubuntu \
  $(. /etc/os-release && echo "$VERSION_CODENAME") stable" | \
  sudo tee /etc/apt/sources.list.d/docker.list > /dev/null
sudo apt-get update
sudo apt-get install -y docker-ce docker-ce-cli containerd.io docker-buildx-plugin docker-compose-plugin

# Añadir usuario actual al grupo de docker
if ! groups $USER | grep &>/dev/null '\bdocker\b'; then
    log_info "Añadiendo el usuario '$USER' al grupo de Docker..."
    sudo usermod -aG docker $USER
    log_warn "¡Acción requerida! Debes salir y volver a entrar en la sesión SSH para que los permisos de Docker se apliquen."
    log_warn "Ejecuta 'exit', vuelve a conectar con SSH y lanza este script de nuevo. El script continuará desde donde lo dejó."
    exit 1
fi

# 1.2 --- Configurar Firewall (UFW) ---
log_info "Configurando el firewall (UFW)..."
sudo ufw default deny incoming
sudo ufw default allow outgoing
sudo ufw allow ssh
sudo ufw --force enable
log_info "Firewall activado. Regla base para SSH permitida."

# ==============================================================================
# --- FASE 2: DESPLIEGUE DE LA PLATAFORMA ---
# ==============================================================================
log_step "FASE 2: Desplegando la plataforma Self-Hosting..."

# Asegurarse de que estamos en la carpeta raíz del proyecto
cd "$(dirname "${BASH_SOURCE[0]}")/.."

# 2.1 --- Generar Secretos y Certificados ---
log_info "Generando secretos y certificados..."
if [ -f "docker/broker/.env" ]; then
    log_warn "Los archivos de secretos .env ya existen. Omitiendo generación."
else
    ./scripts/generate-secrets.sh
fi

if [ -f "certs/agent.pfx" ]; then
    log_warn "Los certificados ya existen. Omitiendo generación."
else
    ./scripts/generate-certs.sh
fi

# 2.2 --- Ejecutar Playbook de Ansible para crear estructura y configurar ---
log_info "Ejecutando playbook de Ansible para crear la estructura de directorios y el firewall..."
ansible-playbook ../ansible/playbooks/setup-local-system.yml

# 2.3 --- Configurar el Agente y Docker Compose ---
log_info "Configurando el Agente y Docker Compose..."
read -p "Introduce la dirección IP de tu VPS Broker: " VPS_IP

# Leer la contraseña del certificado desde el .env
source docker/agent/.env
AGENT_PASS="${AGENT_CERT_PASSWORD}"

# Crear el appsettings.Production.json para el Agente
log_info "Creando archivo de configuración para el Agente..."
sudo bash -c "cat > /opt/selfhosting/config/appsettings.Production.json << EOF
{
  \"Agent\": {
    \"AgentId\": \"my-agent\",
    \"BrokerUrl\": \"https://\${VPS_IP}:50051\",
    \"CertificatePath\": \"/opt/selfhosting/certs/agent.pfx\",
    \"CertificatePassword\": \"${AGENT_PASS}\",
    \"CaCertPath\": \"/opt/selfhosting/certs/ca.crt\",
    \"TraefikUrl\": \"http://traefik:80\"
  }
}
EOF"

# Crear el .env para docker-compose
log_info "Creando archivo .env para Docker Compose..."
cat > docker/.env << EOF
# URL del Broker en tu VPS
BROKER_URL=https://\${VPS_IP}:50051

# Contraseña de los certificados
CERT_PASSWORD=\${AGENT_PASS}
EOF

# 2.4 --- Iniciar el Stack Completo ---
log_step "Iniciando el stack de la plataforma (Traefik, Agent, Dashboard)..."
docker compose -f docker/local-stack-complete.yml up -d --build

# --- Verificación Final ---
log_step "Verificación Final"
log_info "Esperando a que los contenedores arranquen..."
sleep 15

docker ps

FINAL_IP=$(hostname -I | awk '{print $1}')
echo -e "\n${GREEN}=======================================================${NC}"
log_info "¡INSTALACIÓN DE LA TORRE COMPLETA!"
log_info "La torre está configurada con la IP: ${FINAL_IP}"
log_info "El Agente está intentando conectar al Broker en: ${VPS_IP}"
log_info "Puedes acceder al Dashboard en: http://${FINAL_IP}:5500"
log_warn "Asegúrate de que el Broker esté desplegado en el VPS."
log_info "Para ver los logs del Agente, ejecuta: docker logs selfhosting-agent -f"
echo -e "${GREEN}=======================================================${NC}"
