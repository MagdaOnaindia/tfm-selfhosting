# --- Etapa de Build ---
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY TFM.Agent/TFM.Agent.csproj TFM.Agent/
COPY TFM.Contracts/TFM.Contracts.csproj TFM.Contracts/
RUN dotnet restore TFM.Agent/TFM.Agent.csproj
COPY . .
WORKDIR /src/TFM.Agent
RUN dotnet publish -c Release -o /app/publish

# --- Etapa Final ---
FROM mcr.microsoft.com/dotnet/runtime:8.0 AS final
WORKDIR /app

# Crear usuario, copiar archivos y asignar permisos en una sola capa
RUN groupadd -r agent && useradd -r --gid agent --shell /bin/false agent
COPY --from=build /app/publish .
RUN chown -R agent:agent /app

USER agent

ENTRYPOINT ["dotnet", "TFM.Agent.dll"]
