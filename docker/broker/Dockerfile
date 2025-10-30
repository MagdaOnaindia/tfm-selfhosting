FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
WORKDIR /app
EXPOSE 5000 50051
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
# Copiar archivos de proyecto
COPY ["TFM.Broker.csproj", "./"]
COPY ["../TFM.Contracts/TFM.Contracts.csproj", "../TFM.Contracts/"]
# Restore
RUN dotnet restore "TFM.Broker.csproj"
# Copiar código fuente
COPY . .
COPY ../TFM.Contracts ../TFM.Contracts/
# Build
RUN dotnet build "TFM.Broker.csproj" -c Release -o /app/build
FROM build AS publish
RUN dotnet publish "TFM.Broker.csproj" -c Release -o /app/publish /p:UseAppHost=false
FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
# Crear directorios para configuración
RUN mkdir -p /app/config /app/certs /app/logs
ENTRYPOINT ["dotnet", "TFM.Broker.dll"]
