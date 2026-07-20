#!/usr/bin/env bash
# Bootstraps a local MusiQL environment: database, sample catalog, and frontend
# dependencies. Safe to re-run. After it finishes, start the two dev servers
# with the commands it prints.
set -euo pipefail

root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
cd "$root"

skip_tests=0
[[ "${1:-}" == "--skip-tests" ]] && skip_tests=1

require() {
    command -v "$1" >/dev/null 2>&1 || { echo "Missing '$1'. $2"; exit 1; }
}

echo "Checking prerequisites..."
require dotnet "Install the .NET 10 SDK: https://dotnet.microsoft.com/download"
require node   "Install Node 20+: https://nodejs.org"
require docker "Install Docker: https://www.docker.com/products/docker-desktop"

if [[ ! -f .env ]]; then
    cp .env.example .env
    echo "Created .env from .env.example"
fi

echo "Starting PostgreSQL..."
docker compose up -d db

echo "Waiting for the database to be ready..."
for _ in $(seq 1 30); do
    if docker exec musiql-postgres pg_isready -U musiql -d musiql >/dev/null 2>&1; then
        break
    fi
    sleep 1
done

echo "Loading the sample catalog..."
dotnet run --project src/MusiQL.Etl -- load --sample

if [[ "$skip_tests" -eq 0 ]]; then
    echo "Running backend tests..."
    dotnet test --nologo
fi

echo "Installing frontend dependencies..."
(cd frontend && npm install)

cat <<'DONE'

Setup complete. Start MusiQL with two terminals:
  1) dotnet run --project src/MusiQL.Api
  2) cd frontend && npm run dev
Then open http://127.0.0.1:5173
DONE
