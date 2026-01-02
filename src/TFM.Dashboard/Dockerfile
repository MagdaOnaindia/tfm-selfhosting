# --- Etapa 1: Build ---
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Copia los .csproj primero
COPY TFM.Dashboard/TFM.Dashboard.csproj TFM.Dashboard/
COPY TFM.Contracts/TFM.Contracts.csproj TFM.Contracts/
RUN dotnet restore TFM.Dashboard/TFM.Dashboard.csproj

# Copia todo el código fuente
COPY . .

# Publica la aplicación
WORKDIR /src/TFM.Dashboard
RUN dotnet publish -c Release -o /app/publish

# --- Etapa 2: Final ---
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
WORKDIR /app
# ... (El resto de tu Dockerfile del Dashboard con el usuario no-root está bien)
COPY --from=build /app/publish .
# ...
USER dashboard
ENTRYPOINT ["dotnet", "TFM.Dashboard.dll"]
