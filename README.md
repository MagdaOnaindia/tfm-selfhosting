# TFM Self-Hosting Platform

![Build Status](https://img.shields.io/github/actions/workflow/status/MagdaOnaindia/tfm-selfhosting/build.yml?branch=main&style=for-the-badge)
![License](https://img.shields.io/github/license/MagdaOnaindia/tfm-selfhosting?style=for-the-badge)
![.NET Version](https://img.shields.io/badge/.NET-8.0-blueviolet?style=for-the-badge&logo=dotnet)
![Docker](https://img.shields.io/badge/Docker-Required-blue?style=for-the-badge&logo=docker)

Una plataforma de auto-alojamiento de "Configuración Cero" que permite a los desarrolladores exponer sus aplicaciones locales a Internet de forma segura y sencilla, sin necesidad de configurar routers, IPs estáticas o certificados SSL.

---

## ✨ Visión General

Este proyecto nace para resolver el principal obstáculo del auto-alojamiento (*self-hosting*): la complejidad de la red. Mientras que herramientas como Docker han simplificado la ejecución de aplicaciones, hacerlas accesibles desde una red doméstica sigue siendo una tarea para expertos.

Esta plataforma transforma ese proceso en una experiencia de "un solo clic", permitiendo a cualquier desarrollador:
- **Reutilizar hardware personal** como un potente servidor de aplicaciones.
- **Exponer servicios a Internet de forma segura**, incluso detrás de firewalls restrictivos o CG-NAT.
- **Gestionar todo el ciclo de vida** de sus aplicaciones desde un dashboard web intuitivo.
- **Aprender sobre conceptos de despliegue** en un entorno real y controlado.

## 🚀 Arquitectura en Pocas Palabras

La solución se basa en una arquitectura de **túneles inversos** con dos componentes principales:

1.  **Broker (Servidor VPS):** Un servicio central ligero que actúa como punto de entrada público. Gestiona la autenticación de agentes y enruta el tráfico, pero **no aloja las aplicaciones**.
2.  **Agente (Servidor Local):** Un servicio que se instala en la torre del usuario. Establece una conexión de salida segura y persistente con el Broker, recibe las peticiones y las reenvía a los contenedores Docker locales.

![Diagrama de Arquitectura](https://github.com/MagdaOnaindia/tfm-selfhosting/blob/main/docs/diagrams/architecture-overview.png?raw=true)
*(Nota: Deberás subir tu diagrama de arquitectura a la carpeta `docs/diagrams/` y ajustar la ruta).*

## 🛠️ Pila Tecnológica

| Componente | Tecnología | Justificación |
| :--- | :--- | :--- |
| **Backend (Broker & Agente)** | **.NET 8** | Multiplataforma, alto rendimiento y un ecosistema maduro. |
| **Protocolo de Túnel** | **gRPC (Streaming Bidireccional)** | Eficiente, fuertemente tipado y perfecto para conexiones persistentes. |
| **Seguridad del Túnel** | **mTLS (Mutual TLS)** | Autenticación criptográfica bidireccional entre Agente y Broker. |
| **Contenerización** | **Docker + Docker Compose** | Estándar de la industria para empaquetar y ejecutar aplicaciones. |
| **Orquestación de Despliegues** | **Ansible** | Automatización robusta y declarativa para configuraciones complejas. |
| **Reverse Proxy (VPS)** | **Caddy Server** | Simplicidad y gestión automática de certificados SSL/TLS. |
| **Reverse Proxy (Local)** | **Traefik Proxy** | Descubrimiento automático de servicios para una integración perfecta con Docker. |
| **Base de Datos (Multi-Tenant)** | **PostgreSQL** | Potencia, soporte para JSONB y características de seguridad como RLS. |
| **Caché** | **Redis** | Alto rendimiento para rate limiting, protección anti-replay y cache de rutas. |
| **Portal Web & Dashboards**| **Blazor Server** / **Vue.js** | Creación de interfaces web modernas e interactivas. |

## 🏁 Primeros Pasos (Quick Start)

> **⚠️ Advertencia:** Este proyecto se encuentra en una fase activa de desarrollo. Las instrucciones pueden cambiar.

### Requisitos Previos
- Un dominio propio.
- Una cuenta en Cloudflare.
- Un servidor VPS (ej. Hetzner, DigitalOcean) con Docker instalado.
- Un servidor local (tu torre) con acceso a la terminal.

### 1. Desplegar el Broker en el VPS

#### 1. Clona el repositorio en tu VPS
```bash
git clone https://github.com/MagdaOnaindia/tfm-selfhosting.git
cd tfm-selfhosting
```
#### 2. Configura tus variables de entorno (DNS, passwords)
```bash
cp docker/vps/.env.example docker/vps/.env
nano docker/vps/.env
```
#### 3. Levanta todo el stack del broker con Docker Compose
```bash
cd docker/vps
docker compose up -d
```


### 2. Registrarse y Descargar el Agente
1.  Visita `https://portal.tu-dominio.com` y crea una cuenta.
2.  Tras la confirmación, serás redirigido a la página de descarga.
3.  Copia el comando de instalación para tu sistema operativo (Linux/Windows).

### 3. Instalar el Agente en tu Torre
Ejecuta el comando copiado en la terminal de tu servidor local. El script se encargará de todo:
```bash
# Ejemplo para Linux
curl -sSL https://portal.tu-dominio.com/install.sh | sudo bash -s <TU_SETUP_TOKEN>
```
El instalador configurará Docker, Traefik, el Agente y lo iniciará como un servicio.

### 4. ¡Despliega tu primera App!
1.  Abre el dashboard local en `http://localhost:5500`.
2.  Ve a la sección "Deploy" y despliega una aplicación desde un template o tu propio repositorio de GitHub.
3.  ¡Accede a tu aplicación desde su URL pública!

## 📂 Estructura del Repositorio

Este es un monorepo que contiene todos los componentes del proyecto:

-   `src/`: Todo el código fuente en .NET.
    -   `TFM.Contracts/`: Definiciones gRPC `.proto` y modelos compartidos.
    -   `TFM.Broker/`: El servicio que se ejecuta en el VPS.
    -   `TFM.Portal/`: El portal web público para el registro de usuarios.
    -   `TFM.Agent/`: El servicio que se instala en la torre local.
-   `docker/`: Todos los archivos `Dockerfile` y `docker-compose.yml`.
-   `ansible/`: Playbooks para automatizar despliegues de aplicaciones complejas.
-   `scripts/`: Scripts de utilidad para generación de certificados, despliegue, etc.
-   `database/`: Esquema SQL y scripts de inicialización de la base de datos.
-   `docs/`: Documentación detallada del proyecto (arquitectura, seguridad, guías).

## 🤝 Contribuciones

Este es un proyecto académico, pero las ideas y contribuciones son bienvenidas. Por favor, consulta `CONTRIBUTING.md` para más detalles y abre un *issue* para discutir cualquier cambio que te gustaría proponer.

## 📄 Licencia

Este proyecto está bajo la Licencia MIT. Consulta el archivo `LICENSE` para más detalles.
