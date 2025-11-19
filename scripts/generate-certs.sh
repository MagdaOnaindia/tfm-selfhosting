#!/bin/bash
set -euo pipefail

CERT_DIR="../certs"
mkdir -p $CERT_DIR

# Leer contraseña del .env generado
if [ ! -f "../docker/broker/.env" ]; then
    echo "❌ Error: Ejecuta primero ./generate-secrets.sh"
    exit 1
fi

source ../docker/broker/.env
CERT_PASS="${BROKER_CERT_PASSWORD}"

echo "🔐 Generando certificados con contraseña segura..."

# 1. CA Root
echo "[1/3] Generando CA..."
openssl genrsa -out $CERT_DIR/ca.key 4096 2>/dev/null
openssl req -new -x509 -days 3650 -key $CERT_DIR/ca.key -out $CERT_DIR/ca.crt \
    -subj "/C=ES/ST=Madrid/L=Madrid/O=TFM-SelfHosting/CN=TFM-CA" 2>/dev/null

# 2. Broker certificate
echo "[2/3] Generando certificado del Broker..."
openssl genrsa -out $CERT_DIR/broker.key 4096 2>/dev/null
openssl req -new -key $CERT_DIR/broker.key -out $CERT_DIR/broker.csr \
    -subj "/C=ES/ST=Madrid/O=TFM-SelfHosting/CN=broker" 2>/dev/null
openssl x509 -req -days 365 -in $CERT_DIR/broker.csr \
    -CA $CERT_DIR/ca.crt -CAkey $CERT_DIR/ca.key -CAcreateserial \
    -out $CERT_DIR/broker.crt 2>/dev/null

openssl pkcs12 -export -out $CERT_DIR/broker.pfx \
    -inkey $CERT_DIR/broker.key -in $CERT_DIR/broker.crt \
    -password pass:${CERT_PASS} 2>/dev/null

# 3. Agent certificate
echo "[3/3] Generando certificado del Agente..."
openssl genrsa -out $CERT_DIR/agent.key 4096 2>/dev/null
openssl req -new -key $CERT_DIR/agent.key -out $CERT_DIR/agent.csr \
    -subj "/C=ES/ST=Madrid/O=TFM-SelfHosting/CN=my-agent" 2>/dev/null
openssl x509 -req -days 365 -in $CERT_DIR/agent.csr \
    -CA $CERT_DIR/ca.crt -CAkey $CERT_DIR/ca.key -CAcreateserial \
    -out $CERT_DIR/agent.crt 2>/dev/null

openssl pkcs12 -export -out $CERT_DIR/agent.pfx \
    -inkey $CERT_DIR/agent.key -in $CERT_DIR/agent.crt \
    -password pass:${CERT_PASS} 2>/dev/null

# Permisos restrictivos
chmod 600 $CERT_DIR/*.key $CERT_DIR/*.pfx
chmod 644 $CERT_DIR/*.crt

# Cleanup
rm -f $CERT_DIR/*.csr $CERT_DIR/*.srl

echo "✅ Certificados generados con contraseña segura"
echo ""
echo "Archivos creados en $CERT_DIR/:"
ls -lh $CERT_DIR/