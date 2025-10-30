FROM mcr.microsoft.com/dotnet/runtime:8.0 AS base
WORKDIR /app
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
ENTRYPOINT ["dotnet", "TFM.Agent.dll"]
