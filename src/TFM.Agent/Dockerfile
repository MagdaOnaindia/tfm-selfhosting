# --- Etapa 1: Build ---
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Copia los .csproj primero
COPY TFM.Agent/TFM.Agent.csproj TFM.Agent/
COPY TFM.Contracts/TFM.Contracts.csproj TFM.Contracts/
RUN dotnet restore TFM.Agent/TFM.Agent.csproj

# --- INICIO DEL CAMBIO ---
# Copia todo el contenido del contexto (la carpeta 'src') al directorio de trabajo /src de la imagen
COPY . .
# --- FIN DEL CAMBIO ---

# Publica la aplicación
WORKDIR /src/TFM.Agent
RUN dotnet publish -c Release -o /app/publish /p:UseAppHost=false

# --- Etapa 2: Final ---
# ... (el resto del Dockerfile está bien) ...
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
WORKDIR /app
RUN groupadd -r agent && useradd -r --gid agent --shell /bin/false agent
COPY --from=build /app/publish .
RUN chown -R agent:agent /app
USER agent
ENTRYPOINT ["dotnet", "TFM.Agent.dll"]
