#!/bin/bash
set -e

PROJECT="src/GameServer/GameServer.csproj"
SCRIPT_DIR="$(cd "$(dirname "$0")" && pwd)"

cd "$SCRIPT_DIR"

echo "Building..."
dotnet build "$PROJECT" -c Debug --nologo

echo "Starting server on port 7000..."
cd "$SCRIPT_DIR/src/GameServer"
dotnet run --no-build
