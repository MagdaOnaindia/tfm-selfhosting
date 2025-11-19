#!/bin/bash
set -euo pipefail

echo "🔍 Ejecutando chequeo de seguridad..."

ERRORS=0

# Check 1: Variables de entorno
echo "[1/6] Verificando variables de entorno..."
if [ ! -f "docker/broker/.env" ] || [ ! -f "docker/agent/.env" ]; then
    echo "❌ Archivos .env faltantes"
    ERRORS=$((ERRORS + 1))
else
    echo "✅ Archivos .env presentes"
fi

# Check 2: Certificados con permisos correctos
echo "[2/6] Verificando permisos de certificados..."
for file in certs/*.key certs/*.pfx; do
    if [ -f "$file" ]; then
        PERMS=$(stat -c %a "$file" 2>/dev/null || stat -f %A "$file" 2>/dev/null)
        if [ "$PERMS" != "600" ]; then
            echo "❌ $file tiene permisos $PERMS (debería ser 600)"
            ERRORS=$((ERRORS + 1))
        fi
    fi
done
echo "✅ Permisos de certificados correctos"

# Check 3: No hay contraseñas hardcodeadas en JSON
echo "[3/6] Verificando ausencia de contraseñas en configuración..."
if grep -r "\"dev123\"" src/ config/ 2>/dev/null; then
    echo "❌ Contraseña de desarrollo encontrada en código"
    ERRORS=$((ERRORS + 1))
else
    echo "✅ No se encontraron contraseñas hardcodeadas"
fi

# Check 4: Validar que AdminApiKey esté configurada
echo "[4/6] Verificando AdminApiKey..."
if grep -q "AdminApiKey.*CAMBIAR" src/TFM.Broker/appsettings*.json 2>/dev/null; then
    echo "❌ AdminApiKey no ha sido cambiada del valor por defecto"
    ERRORS=$((ERRORS + 1))
else
    echo "✅ AdminApiKey configurada"
fi

# Check 5: Dockerfiles con usuario no-root
echo "[5/6] Verificando Dockerfiles usan usuario no-root..."
for dockerfile in $(find . -name "Dockerfile"); do
    if ! grep -q "USER " "$dockerfile"; then
        echo "❌ $dockerfile no especifica USER no-root"
        ERRORS=$((ERRORS + 1))
    fi
done
echo "✅ Dockerfiles configurados con usuario no-root"

# Check 6: .gitignore contiene secretos
echo "[6/6] Verificando .gitignore..."
REQUIRED_IGNORES=("*.pfx" "*.key" ".env" "appsettings.Production.json")
for pattern in "${REQUIRED_IGNORES[@]}"; do
    if ! grep -q "$pattern" .gitignore; then
        echo "❌ .gitignore no contiene: $pattern"
        ERRORS=$((ERRORS + 1))
    fi
done
echo "✅ .gitignore completo"

echo ""
if [ $ERRORS -eq 0 ]; then
    echo "✅ Todos los chequeos de seguridad pasados"
    exit 0
else
    echo "❌ $ERRORS problema(s) de seguridad encontrado(s)"
    exit 1
fi