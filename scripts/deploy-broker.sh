#!/bin/bash
set -e
echo "￿￿￿￿￿￿￿￿￿￿￿￿￿￿￿￿￿￿￿￿￿￿￿￿￿￿￿￿￿￿￿￿￿￿￿￿￿￿￿￿￿￿￿￿￿￿￿￿￿￿￿￿￿￿"
echo "￿ Deploying Broker to VPS ￿"
echo "￿￿￿￿￿￿￿￿￿￿￿￿￿￿￿￿￿￿￿￿￿￿￿￿￿￿￿￿￿￿￿￿￿￿￿￿￿￿￿￿￿￿￿￿￿￿￿￿￿￿￿￿￿￿"
echo ""
# Variables
VPS_HOST="${1:-root@your-vps-ip}"
PROJECT_DIR="~/tfm-selfhosting-single"
echo "￿ Target VPS: $VPS_HOST"
echo ""
# Verificar que existen los certificados
if [ ! -f ../certs/broker.pfx ]; then
echo "￿ Error: Certificados no encontrados"
echo " Ejecuta: ./generate-certs.sh"
exit 1
fi
# Verificar que existe la configuración
if [ ! -f ../config/domains.json ]; then
echo "￿ Error: Configuración de dominios no encontrada"
echo " Crea el archivo: config/domains.json"
exit 1
fi
echo "￿ [1/4] Creando paquete de deployment..."
cd ..
tar czf /tmp/broker-deploy.tar.gz \
src/TFM.Broker/ \
src/TFM.Contracts/ \
docker/broker/ \
docker/caddy/ \
docker/vps-complete.yml \
certs/ \
config/
echo "￿ [2/4] Subiendo al VPS..."
scp /tmp/broker-deploy.tar.gz $VPS_HOST:/tmp/
echo "￿ [3/4] Desplegando en VPS..."
ssh $VPS_HOST << 'ENDSSH'
set -e
# Crear directorio
mkdir -p ~/tfm-selfhosting
cd ~/tfm-selfhosting
# Extraer
tar xzf /tmp/broker-deploy.tar.gz
# Build y deploy
cd docker
# Detener servicios existentes
docker compose -f vps-complete.yml down 2>/dev/null || true
# Iniciar servicios
docker compose -f vps-complete.yml up -d --build
echo ""
echo "￿ Deployment completado"
echo ""
# Verificar estado
docker compose -f vps-complete.yml ps
# Verificar logs
echo ""
echo "￿ Logs del Broker:"
docker logs tfm-broker --tail 20
ENDSSH
echo ""
echo "￿ [4/4] Deployment completo"
echo ""
echo "Próximos pasos:"
echo " 1. Configurar DNS: *.ejemplo.com → IP_VPS"
echo " 2. Verificar firewall: puertos 80, 443, 50051 abiertos"
echo " 3. Test: curl https://ejemplo.com"
echo ""
# Cleanup
rm /tmp/broker-deploy.tar.gz
