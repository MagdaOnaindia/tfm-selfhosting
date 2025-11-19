FROM mcr.microsoft.com/dotnet/runtime:8.0 AS base
WORKDIR /app

#Crear usuario no-root
RUN groupadd -r selfhosting && useradd -r -g selfhosting selfhosting

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

COPY ["TFM.Agent.csproj", "./"]
COPY ["../TFM.Contracts/TFM.Contracts.csproj", "../TFM.Contracts/"]

RUN dotnet restore "TFM.Agent.csproj"

COPY . .
COPY ../TFM.Contracts ../TFM.Contracts/

RUN dotnet build "TFM.Agent.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "TFM.Agent.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .

#Cambiar ownership y permisos
RUN chown -R selfhosting:selfhosting /app && \
    chmod -R 550 /app && \
    mkdir -p /app/logs && \
    chown selfhosting:selfhosting /app/logs && \
    chmod 770 /app/logs

#Cambiar a usuario no-root
USER selfhosting

#Health check
HEALTHCHECK --interval=30s --timeout=5s --start-period=10s --retries=3 \
  CMD pgrep -f "TFM.Agent.dll" || exit 1

ENTRYPOINT ["dotnet", "TFM.Agent.dll"]