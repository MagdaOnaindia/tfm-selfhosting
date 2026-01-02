# --- Etapa de Build ---
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY TFM.Dashboard/TFM.Dashboard.csproj TFM.Dashboard/
COPY TFM.Contracts/TFM.Contracts.csproj TFM.Contracts/
RUN dotnet restore TFM.Dashboard/TFM.Dashboard.csproj
COPY . .
WORKDIR /src/TFM.Dashboard
RUN dotnet publish -c Release -o /app/publish

# --- Etapa Final ---
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
WORKDIR /app

# Crear usuario, copiar archivos y asignar permisos en una sola capa
RUN groupadd -r dashboard && useradd -r --gid dashboard --shell /bin/false dashboard
COPY --from=build /app/publish .
RUN chown -R dashboard:dashboard /app

USER dashboard

EXPOSE 8080
ENTRYPOINT ["dotnet", "TFM.Dashboard.dll"]
