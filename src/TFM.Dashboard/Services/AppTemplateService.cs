using TFM.Dashboard.Interfaces;
using TFM.Dashboard.Models;

namespace TFM.Dashboard.Services;

/// <summary>
/// Proporciona templates predefinidos para aplicaciones populares.
/// 
/// SOLID:
/// - S: Solo gestión de templates
/// </summary>
public class AppTemplateService : IAppTemplateService
{
    private readonly List<AppTemplate> _templates;

    public AppTemplateService()
    {
        _templates = InitializeTemplates();
    }

    public List<AppTemplate> GetTemplates() => _templates;

    public AppTemplate? GetTemplate(string id) =>
        _templates.FirstOrDefault(t => t.Id == id);

    public List<AppTemplate> GetTemplatesByCategory(string category) =>
        _templates.Where(t => t.Category == category).ToList();

    public List<string> GetCategories() =>
        _templates.Select(t => t.Category).Distinct().OrderBy(c => c).ToList();

    public Task<string> GenerateDockerComposeAsync(
        AppTemplate template,
        Dictionary<string, string> variables)
    {
        if (string.IsNullOrEmpty(template.ComposeTemplate))
        {
            throw new InvalidOperationException("Template does not have a compose template");
        }

        var compose = template.ComposeTemplate;

        // Reemplazar todas las variables
        foreach (var (key, value) in variables)
        {
            compose = compose.Replace($"{{{{{key}}}}}", value);
        }

        return Task.FromResult(compose);
    }

    public bool ValidateVariables(
        AppTemplate template,
        Dictionary<string, string> variables)
    {
        foreach (var envVar in template.RequiredEnvVars.Where(e => e.Required))
        {
            if (!variables.ContainsKey(envVar.Name) ||
                string.IsNullOrWhiteSpace(variables[envVar.Name]))
            {
                return false;
            }
        }

        return true;
    }

    private List<AppTemplate> InitializeTemplates()
    {
        return new List<AppTemplate>
        {
            // ════════════════════════════════════════════════════════
            // NEXTCLOUD
            // ════════════════════════════════════════════════════════
            new AppTemplate
            {
                Id = "nextcloud",
                Name = "Nextcloud",
                Description = "Suite de productividad auto-hospedada con almacenamiento en la nube, calendario, contactos y colaboración en documentos",
                Category = "Productividad",
                IconUrl = "https://upload.wikimedia.org/wikipedia/commons/6/60/Nextcloud_Logo.svg",
                DefaultPort = 80,
                Tags = new List<string> { "cloud", "storage", "productivity", "calendar" },
                RequiredEnvVars = new List<EnvironmentVariable>
                {
                    new() { Name = "MYSQL_ROOT_PASSWORD", Description = "Contraseña root de MySQL", IsSecret = true },
                    new() { Name = "MYSQL_PASSWORD", Description = "Contraseña de base de datos", IsSecret = true },
                    new() { Name = "MYSQL_DATABASE", Description = "Nombre de la base de datos", DefaultValue = "nextcloud" },
                    new() { Name = "MYSQL_USER", Description = "Usuario de base de datos", DefaultValue = "nextcloud" },
                    new() { Name = "NEXTCLOUD_ADMIN_USER", Description = "Usuario administrador", DefaultValue = "admin" },
                    new() { Name = "NEXTCLOUD_ADMIN_PASSWORD", Description = "Contraseña administrador", IsSecret = true }
                },
                ComposeTemplate = @"
version: '3.8'

services:
  nextcloud-db:
    image: mariadb:10.11
    container_name: {{SERVICE_NAME}}-db
    restart: unless-stopped
    command: --transaction-isolation=READ-COMMITTED --log-bin=binlog --binlog-format=ROW
    environment:
      - MYSQL_ROOT_PASSWORD={{MYSQL_ROOT_PASSWORD}}
      - MYSQL_DATABASE={{MYSQL_DATABASE}}
      - MYSQL_USER={{MYSQL_USER}}
      - MYSQL_PASSWORD={{MYSQL_PASSWORD}}
    volumes:
      - {{DATA_PATH}}/db:/var/lib/mysql
    networks:
      - traefik-net

  nextcloud-redis:
    image: redis:alpine
    container_name: {{SERVICE_NAME}}-redis
    restart: unless-stopped
    networks:
      - traefik-net

  nextcloud:
    image: nextcloud:latest
    container_name: {{SERVICE_NAME}}
    restart: unless-stopped
    depends_on:
      - nextcloud-db
      - nextcloud-redis
    environment:
      - MYSQL_HOST={{SERVICE_NAME}}-db
      - MYSQL_DATABASE={{MYSQL_DATABASE}}
      - MYSQL_USER={{MYSQL_USER}}
      - MYSQL_PASSWORD={{MYSQL_PASSWORD}}
      - REDIS_HOST={{SERVICE_NAME}}-redis
      - NEXTCLOUD_ADMIN_USER={{NEXTCLOUD_ADMIN_USER}}
      - NEXTCLOUD_ADMIN_PASSWORD={{NEXTCLOUD_ADMIN_PASSWORD}}
      - NEXTCLOUD_TRUSTED_DOMAINS={{DOMAIN}}
      - OVERWRITEPROTOCOL=https
      - OVERWRITEHOST={{DOMAIN}}
      - PHP_MEMORY_LIMIT=512M
      - PHP_UPLOAD_LIMIT=10G
    volumes:
      - {{DATA_PATH}}/data:/var/www/html
    networks:
      - traefik-net
    labels:
      - traefik.enable=true
      - traefik.http.routers.{{SERVICE_NAME}}.rule=Host(`{{DOMAIN}}`)
      - traefik.http.routers.{{SERVICE_NAME}}.entrypoints=web
      - traefik.http.services.{{SERVICE_NAME}}.loadbalancer.server.port=80
      - traefik.http.middlewares.{{SERVICE_NAME}}-caldav.redirectregex.permanent=true
      - traefik.http.middlewares.{{SERVICE_NAME}}-caldav.redirectregex.regex=^https://(.*)/.well-known/(card|cal)dav
      - traefik.http.middlewares.{{SERVICE_NAME}}-caldav.redirectregex.replacement=https://$${1}/remote.php/dav/
      - traefik.http.routers.{{SERVICE_NAME}}.middlewares={{SERVICE_NAME}}-caldav

networks:
  traefik-net:
    external: true

volumes:
  nextcloud-data:
  nextcloud-db:
",
                Documentation = "https://github.com/nextcloud/docker"
            },

            // ════════════════════════════════════════════════════════
            // GITEA
            // ════════════════════════════════════════════════════════
            new AppTemplate
            {
                Id = "gitea",
                Name = "Gitea",
                Description = "Servidor Git auto-hospedado ligero escrito en Go",
                Category = "Desarrollo",
                IconUrl = "https://gitea.io/images/gitea.png",
                DefaultPort = 3000,
                Tags = new List<string> { "git", "development", "vcs" },
                RequiredEnvVars = new List<EnvironmentVariable>
                {
                    new() { Name = "USER_UID", Description = "UID del usuario", DefaultValue = "1000" },
                    new() { Name = "USER_GID", Description = "GID del usuario", DefaultValue = "1000" },
                    new() { Name = "GITEA__database__PASSWD", Description = "Contraseña de base de datos", IsSecret = true }
                },
                ComposeTemplate = @"
version: '3.8'

services:
  gitea-db:
    image: postgres:14-alpine
    container_name: {{SERVICE_NAME}}-db
    restart: unless-stopped
    environment:
      - POSTGRES_USER=gitea
      - POSTGRES_PASSWORD={{GITEA__database__PASSWD}}
      - POSTGRES_DB=gitea
    volumes:
      - {{DATA_PATH}}/postgres:/var/lib/postgresql/data
    networks:
      - traefik-net

  gitea:
    image: gitea/gitea:latest
    container_name: {{SERVICE_NAME}}
    restart: unless-stopped
    depends_on:
      - gitea-db
    environment:
      - USER_UID={{USER_UID}}
      - USER_GID={{USER_GID}}
      - GITEA__database__DB_TYPE=postgres
      - GITEA__database__HOST={{SERVICE_NAME}}-db:5432
      - GITEA__database__NAME=gitea
      - GITEA__database__USER=gitea
      - GITEA__database__PASSWD={{GITEA__database__PASSWD}}
      - GITEA__server__DOMAIN={{DOMAIN}}
      - GITEA__server__SSH_DOMAIN={{DOMAIN}}
      - GITEA__server__ROOT_URL=https://{{DOMAIN}}/
    volumes:
      - {{DATA_PATH}}/data:/data
      - /etc/timezone:/etc/timezone:ro
      - /etc/localtime:/etc/localtime:ro
    ports:
      - '2222:22'
    networks:
      - traefik-net
    labels:
      - traefik.enable=true
      - traefik.http.routers.{{SERVICE_NAME}}.rule=Host(`{{DOMAIN}}`)
      - traefik.http.routers.{{SERVICE_NAME}}.entrypoints=web
      - traefik.http.services.{{SERVICE_NAME}}.loadbalancer.server.port=3000

networks:
  traefik-net:
    external: true
",
                Documentation = "https://docs.gitea.io/"
            },

            // ════════════════════════════════════════════════════════
            // PORTAINER
            // ════════════════════════════════════════════════════════
            new AppTemplate
            {
                Id = "portainer",
                Name = "Portainer",
                Description = "Gestión de contenedores Docker con interfaz web intuitiva",
                Category = "Administración",
                IconUrl = "https://www.portainer.io/hubfs/portainer-logo-black.svg",
                DefaultPort = 9000,
                Tags = new List<string> { "docker", "management", "admin" },
                RequiredEnvVars = new List<EnvironmentVariable>(),
                ComposeTemplate = @"
version: '3.8'

services:
  portainer:
    image: portainer/portainer-ce:latest
    container_name: {{SERVICE_NAME}}
    restart: unless-stopped
    volumes:
      - /var/run/docker.sock:/var/run/docker.sock
      - {{DATA_PATH}}:/data
    networks:
      - traefik-net
    labels:
      - traefik.enable=true
      - traefik.http.routers.{{SERVICE_NAME}}.rule=Host(`{{DOMAIN}}`)
      - traefik.http.routers.{{SERVICE_NAME}}.entrypoints=web
      - traefik.http.services.{{SERVICE_NAME}}.loadbalancer.server.port=9000

networks:
  traefik-net:
    external: true
",
                Documentation = "https://docs.portainer.io/"
            },

            // ════════════════════════════════════════════════════════
            // WORDPRESS
            // ════════════════════════════════════════════════════════
            new AppTemplate
            {
                Id = "wordpress",
                Name = "WordPress",
                Description = "Sistema de gestión de contenidos (CMS) más popular del mundo",
                Category = "Web",
                IconUrl = "https://s.w.org/style/images/about/WordPress-logotype-simplified.png",
                DefaultPort = 80,
                Tags = new List<string> { "cms", "blog", "website" },
                RequiredEnvVars = new List<EnvironmentVariable>
                {
                    new() { Name = "MYSQL_ROOT_PASSWORD", Description = "Contraseña root de MySQL", IsSecret = true },
                    new() { Name = "MYSQL_PASSWORD", Description = "Contraseña de WordPress DB", IsSecret = true }
                },
                ComposeTemplate = @"
version: '3.8'

services:
  wordpress-db:
    image: mysql:8.0
    container_name: {{SERVICE_NAME}}-db
    restart: unless-stopped
    environment:
      - MYSQL_ROOT_PASSWORD={{MYSQL_ROOT_PASSWORD}}
      - MYSQL_DATABASE=wordpress
      - MYSQL_USER=wordpress
      - MYSQL_PASSWORD={{MYSQL_PASSWORD}}
    volumes:
      - {{DATA_PATH}}/db:/var/lib/mysql
    networks:
      - traefik-net

  wordpress:
    image: wordpress:latest
    container_name: {{SERVICE_NAME}}
    restart: unless-stopped
    depends_on:
      - wordpress-db
    environment:
      - WORDPRESS_DB_HOST={{SERVICE_NAME}}-db
      - WORDPRESS_DB_USER=wordpress
      - WORDPRESS_DB_PASSWORD={{MYSQL_PASSWORD}}
      - WORDPRESS_DB_NAME=wordpress
    volumes:
      - {{DATA_PATH}}/wordpress:/var/www/html
    networks:
      - traefik-net
    labels:
      - traefik.enable=true
      - traefik.http.routers.{{SERVICE_NAME}}.rule=Host(`{{DOMAIN}}`)
      - traefik.http.routers.{{SERVICE_NAME}}.entrypoints=web
      - traefik.http.services.{{SERVICE_NAME}}.loadbalancer.server.port=80

networks:
  traefik-net:
    external: true
",
                Documentation = "https://hub.docker.com/_/wordpress"
            },

            // ════════════════════════════════════════════════════════
            // WHOAMI (Testing)
            // ════════════════════════════════════════════════════════
            new AppTemplate
            {
                Id = "whoami",
                Name = "Whoami",
                Description = "Servicio simple para testing de routing y headers HTTP",
                Category = "Testing",
                DefaultPort = 80,
                Tags = new List<string> { "test", "debug", "http" },
                RequiredEnvVars = new List<EnvironmentVariable>(),
                ComposeTemplate = @"
version: '3.8'

services:
  whoami:
    image: traefik/whoami:latest
    container_name: {{SERVICE_NAME}}
    restart: unless-stopped
    networks:
      - traefik-net
    labels:
      - traefik.enable=true
      - traefik.http.routers.{{SERVICE_NAME}}.rule=Host(`{{DOMAIN}}`)
      - traefik.http.routers.{{SERVICE_NAME}}.entrypoints=web

networks:
  traefik-net:
    external: true
",
                Documentation = "https://github.com/traefik/whoami"
            }
        };
    }
}