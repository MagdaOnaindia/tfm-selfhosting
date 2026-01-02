# --- Etapa 1: Build ---
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Copia los .csproj primero para cachear la restauración de dependencias
COPY TFM.Agent/TFM.Agent.csproj TFM.Agent/
COPY TFM.Contracts/TFM.Contracts.csproj TFM.Contracts/
RUN dotnet restore TFM.Agent/TFM.Agent.csproj

# Copia todo el código fuente
COPY . .

# Publica la aplicación
WORKDIR /src/TFM.Agent
RUN dotnet publish -c Release -o /app/publish

# --- Etapa 2: Final ---
FROM mcr.microsoft.com/dotnet/runtime:8.0 AS final
WORKDIR /app
# ... (El resto de tu Dockerfile del Agent con el usuario no-root está bien)
COPY --from=build /app/publish .
# ...
USER selfhosting
ENTRYPOINT ["dotnet", "TFM.Agent.dll"]
