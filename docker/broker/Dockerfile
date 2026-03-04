# --- Etapa 1: Build ---
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Copia los .csproj y restaura dependencias
COPY src/TFM.Contracts/TFM.Contracts.csproj TFM.Contracts/
COPY src/TFM.Broker/TFM.Broker.csproj TFM.Broker/
RUN dotnet restore TFM.Broker/TFM.Broker.csproj

# Copia todo el código fuente
COPY src/ .

# Publica la aplicación
WORKDIR /src/TFM.Broker
RUN dotnet publish -c Release -o /app/publish

# --- Etapa 2: Final ---
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
WORKDIR /app

# Instalar curl para el healthcheck
RUN apt-get update && apt-get install -y curl && rm -rf /var/lib/apt/lists/*

# Crear usuario no-root
RUN groupadd -r broker && useradd -r --gid broker --shell /bin/false broker

# Copiar binarios y asignar permisos
COPY --from=build /app/publish .
RUN chown -R broker:broker /app

USER broker
EXPOSE 5000 50051
ENTRYPOINT ["dotnet", "TFM.Broker.dll"]
