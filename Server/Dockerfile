# ── Stage 1: build ────────────────────────────────────────────────────────────
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Restore NuGet packages (layer-cached unless .csproj changes)
COPY ["src/GameServer/GameServer.csproj", "src/GameServer/"]
RUN dotnet restore "src/GameServer/GameServer.csproj"

# Build
COPY . .
WORKDIR "/src/src/GameServer"
RUN dotnet build "GameServer.csproj" -c Release -o /app/build --no-restore

# ── Stage 2: publish ───────────────────────────────────────────────────────────
FROM build AS publish
RUN dotnet publish "GameServer.csproj" -c Release -o /app/publish \
    /p:UseAppHost=false --no-restore

# ── Stage 3: runtime image ─────────────────────────────────────────────────────
FROM mcr.microsoft.com/dotnet/runtime:8.0 AS final
WORKDIR /app

# TCP port exposed by the game server
EXPOSE 7000

COPY --from=publish /app/publish .

ENTRYPOINT ["dotnet", "GameServer.dll"]
