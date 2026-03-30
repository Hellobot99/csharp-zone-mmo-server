#!/bin/bash
set -e

PROJECT="src/TestClient/TestClient.csproj"
SCRIPT_DIR="$(cd "$(dirname "$0")" && pwd)"

cd "$SCRIPT_DIR"

echo "Building..."
dotnet build "$PROJECT" -c Debug --nologo

echo "Starting TestClient..."
cd "$SCRIPT_DIR/src/TestClient"
dotnet run --no-build -- "$@"
