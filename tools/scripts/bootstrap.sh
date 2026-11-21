#!/usr/bin/env bash
# Installs dependencies for whichever parts of the repo you can build.
#
# Deliberately tolerant: a backend engineer without Xcode and an iOS engineer
# without the .NET SDK should both be able to run this and get a working
# environment for their part of the repo.
set -uo pipefail

REPO_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
cd "${REPO_ROOT}"

if [ -d /opt/homebrew/opt/dotnet@10/bin ]; then
  PATH="/opt/homebrew/opt/dotnet@10/bin:${PATH}"
fi

step() { printf '\n\033[1m==> %s\033[0m\n' "$1"; }
skip() { printf '    skipped: %s\n' "$1"; }

step "Backend (.NET)"
if command -v dotnet >/dev/null 2>&1; then
  (cd backend && dotnet tool restore && dotnet restore)
else
  skip "dotnet not installed (brew install dotnet@10)"
fi

step "iOS shared packages"
if command -v swift >/dev/null 2>&1; then
  (cd ios/Packages/AtlasKit && swift build)
else
  skip "swift not installed"
fi

step "Node worker"
if command -v npm >/dev/null 2>&1; then
  (cd services/node-worker && npm ci)
else
  skip "npm not installed"
fi

step "Python worker"
if command -v uv >/dev/null 2>&1; then
  (cd services/python-worker && uv sync)
else
  skip "uv not installed (brew install uv)"
fi

step "Local environment file"
if [ -f .env ]; then
  printf '    .env already exists, leaving it alone\n'
else
  cp .env.example .env
  printf '    created .env from .env.example\n'
fi

step "Done"
printf '    Run "make doctor" to see what is still missing, then "make up".\n\n'
