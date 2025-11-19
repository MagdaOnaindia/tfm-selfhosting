FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
WORKDIR /app
EXPOSE 5000 50051

# Crear usuario no-root
RUN groupadd -r broker && useradd -r -g broker broker

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

COPY ["TFM.Broker/TFM.Broker.csproj", "TFM.Broker/"]
COPY ["TFM.Contracts/TFM.Contracts.csproj", "TFM.Contracts/"]

RUN dotnet restore "TFM.Broker/TFM.Broker.csproj"

COPY . .
WORKDIR "/src/TFM.Broker"
RUN dotnet build "TFM.Broker.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "TFM.Broker.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .

# Permisos seguros
RUN chown -R broker:broker /app && \
    chmod -R 550 /app && \
    mkdir -p /app/logs /app/config /app/certs && \
    chown broker:broker /app/logs /app/config /app/certs && \
    chmod 770 /app/logs && \
    chmod 550 /app/config /app/certs

USER broker

HEALTHCHECK --interval=30s --timeout=5s --start-period=10s --retries=3 \
    CMD curl -f http://localhost:5000/health || exit 1

ENTRYPOINT ["dotnet", "TFM.Broker.dll"]