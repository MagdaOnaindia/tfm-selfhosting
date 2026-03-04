# --- Etapa 1: Build ---
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Copia los .csproj primero
COPY TFM.Dashboard/TFM.Dashboard.csproj TFM.Dashboard/
COPY TFM.Contracts/TFM.Contracts.csproj TFM.Contracts/
RUN dotnet restore TFM.Dashboard/TFM.Dashboard.csproj

# --- INICIO DEL CAMBIO ---
# Copia todo el contenido del contexto (la carpeta 'src') al directorio de trabajo /src de la imagen
COPY . .
# --- FIN DEL CAMBIO ---

# Publica la aplicación
WORKDIR /src/TFM.Dashboard
RUN dotnet publish -c Release -o /app/publish /p:UseAppHost=false

# --- Etapa 2: Final ---
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
WORKDIR /app

# Instalar Docker CLI (necesario para docker compose via CliWrap)
RUN apt-get update \
    && apt-get install -y --no-install-recommends ca-certificates curl gnupg \
    && install -m 0755 -d /etc/apt/keyrings \
    && curl -fsSL https://download.docker.com/linux/debian/gpg | gpg --dearmor -o /etc/apt/keyrings/docker.gpg \
    && chmod a+r /etc/apt/keyrings/docker.gpg \
    && echo "deb [arch=$(dpkg --print-architecture) signed-by=/etc/apt/keyrings/docker.gpg] https://download.docker.com/linux/debian $(. /etc/os-release && echo "$VERSION_CODENAME") stable" > /etc/apt/sources.list.d/docker.list \
    && apt-get update \
    && apt-get install -y --no-install-recommends docker-ce-cli docker-compose-plugin \
    && rm -rf /var/lib/apt/lists/*

RUN groupadd -r dashboard && useradd -r --gid dashboard --shell /bin/false dashboard
COPY --from=build /app/publish .
RUN chown -R dashboard:dashboard /app
USER dashboard
EXPOSE 8080
ENTRYPOINT ["dotnet", "TFM.Dashboard.dll"]
