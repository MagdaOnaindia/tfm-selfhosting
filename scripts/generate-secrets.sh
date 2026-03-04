# #!/bin/bash
# set -euo pipefail

# echo "🔐 Generando secretos seguros..."

# # Generar contraseña aleatoria para certificados
# CERT_PASSWORD=$(openssl rand -base64 32 | tr -d "=+/" | cut -c1-32)

# # Generar API Key para admin
# ADMIN_API_KEY=$(openssl rand -hex 32)

# # Crear archivo .env para broker
# cat > docker/broker/.env << EOF
# BROKER_CERT_PASSWORD=${CERT_PASSWORD}
# ADMIN_API_KEY=${ADMIN_API_KEY}
# EOF

# # Crear archivo .env para agent
# cat > docker/agent/.env << EOF
# AGENT_CERT_PASSWORD=${CERT_PASSWORD}
# EOF

# chmod 600 docker/broker/.env docker/agent/.env

# echo "✅ Secretos generados:"
# echo "   - docker/broker/.env"
# echo "   - docker/agent/.env"
# echo ""
# echo "⚠️  IMPORTANTE: Estos archivos NO deben subirse a Git"
# echo "⚠️  IMPORTANTE: Usa esta contraseña al generar certificados"
# echo ""
# echo "Contraseña de certificados: ${CERT_PASSWORD}"

#!/bin/bash
set -euo pipefail

# --- Inicio del bloque de robustez ---
# Moverse al directorio raíz del proyecto (un nivel por encima de la carpeta 'scripts')
# para asegurar que todas las rutas relativas funcionen correctamente.
SCRIPT_DIR=$( cd -- "$( dirname -- "${BASH_SOURCE[0]}" )" &> /dev/null && pwd )
PROJECT_ROOT=$( cd -- "$SCRIPT_DIR/.." &> /dev/null && pwd )
cd "$PROJECT_ROOT"
# --- Fin del bloque de robustez ---

echo "🔐 Generando secretos seguros..."

# --- Inicio del bloque de creación de directorios ---
# Definir las rutas de los directorios para los secretos
BROKER_DIR="docker/broker"
AGENT_DIR="docker/agent"

# Crear los directorios si no existen
mkdir -p "$BROKER_DIR"
mkdir -p "$AGENT_DIR"
# --- Fin del bloque de creación de directorios ---

# Generar contraseña aleatoria para certificados
CERT_PASSWORD=$(openssl rand -base64 32 | tr -d "=+/" | cut -c1-32)

# Generar API Key para admin (si aplica a tu proyecto)
ADMIN_API_KEY=$(openssl rand -hex 32)

# Crear archivo .env para broker
# Incluye las variables en formato .NET (con __) para que el contenedor las lea directamente
cat > "${BROKER_DIR}/.env" << EOF
BROKER_CERT_PASSWORD=${CERT_PASSWORD}
ADMIN_API_KEY=${ADMIN_API_KEY}
Certificates__BrokerCertPassword=${CERT_PASSWORD}
Security__AdminApiKey=${ADMIN_API_KEY}
EOF

# Crear archivo .env para agent
cat > "${AGENT_DIR}/.env" << EOF
AGENT_CERT_PASSWORD=${CERT_PASSWORD}
EOF

# Establecer permisos restrictivos a los archivos de secretos
chmod 600 "${BROKER_DIR}/.env" "${AGENT_DIR}/.env"

echo "✅ Secretos generados con éxito:"
echo "   - ${BROKER_DIR}/.env"
echo "   - ${AGENT_DIR}/.env"
echo ""
echo "⚠️  IMPORTANTE: Estos archivos están excluidos de Git y contienen información sensible."
echo "   La contraseña para los certificados generados es: ${CERT_PASSWORD}"
