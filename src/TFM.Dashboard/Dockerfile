FROM mcr.microsoft.com/dotnet/runtime:8.0 AS base
WORKDIR /app

#Crear usuario no-root
RUN groupadd -r selfhosting && useradd -r -g selfhosting selfhosting

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

COPY ["TFM.Dashboard.csproj", "./"]
COPY ["../TFM.Contracts/TFM.Contracts.csproj", "../TFM.Contracts/"]

RUN dotnet restore "TFM.Dashboard.csproj"

COPY . .
COPY ../TFM.Contracts ../TFM.Contracts/

# Publica la aplicación en modo Release
WORKDIR "/src/TFM.Dashboard"
RUN dotnet publish "TFM.Dashboard.csproj" -c Release -o /app/publish /p:UseAppHost=false

# --- Etapa 2: Final ---
# Usamos la imagen de runtime de ASP.NET, que es mucho más ligera
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
WORKDIR /app

# (Opcional, pero buena práctica) Crear un usuario no-root para ejecutar la aplicación
RUN groupadd -r dashboard && useradd -r --gid dashboard --shell /bin/false dashboard

# Copiar los binarios publicados desde la etapa de build
COPY --from=build /app/publish .

# Cambiar al usuario no-root
USER dashboard

EXPOSE 8080 8081

# El Entrypoint que ejecuta la aplicación
ENTRYPOINT ["dotnet", "TFM.Dashboard.dll"]
