#!/usr/bin/env bash
# ---------------------------------------------------------------------------
# Exports the API's OpenAPI document to contracts/openapi/.
#
# The document is generated from the endpoints themselves, so it cannot drift
# from the implementation. Committing it means a breaking API change shows up
# as a reviewable diff in a pull request instead of as a broken mobile release
# three weeks later. CI runs this and fails if the result differs.
#
#     ./tools/scripts/export-openapi.sh
# ---------------------------------------------------------------------------
set -euo pipefail

REPO_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
OUTPUT_DIR="${REPO_ROOT}/contracts/openapi"
OUTPUT_FILE="${OUTPUT_DIR}/v1.json"
PORT="${OPENAPI_EXPORT_PORT:-8099}"
DOCUMENT_URL="http://127.0.0.1:${PORT}/openapi/v1.json"

mkdir -p "${OUTPUT_DIR}"

# The app validates that a connection string is present at startup but does
# not connect, so a placeholder is enough to generate the document. No
# database is needed to export a contract.
export ASPNETCORE_ENVIRONMENT=Development
export ASPNETCORE_URLS="http://127.0.0.1:${PORT}"
export ConnectionStrings__Default="Host=localhost;Port=5432;Database=atlas;Username=atlas;Password=openapi-export"
export Auth__Enabled=false
export OTEL_EXPORTER_OTLP_ENDPOINT=""

LOG_FILE="$(mktemp)"
API_PID=""

cleanup() {
  if [[ -n "${API_PID}" ]] && kill -0 "${API_PID}" 2>/dev/null; then
    kill "${API_PID}" 2>/dev/null || true
    wait "${API_PID}" 2>/dev/null || true
  fi
  rm -f "${LOG_FILE}"
}
trap cleanup EXIT

echo "Building the API..."
dotnet build "${REPO_ROOT}/backend/src/Atlas.Api" --configuration Release --nologo --verbosity quiet

echo "Starting the API on port ${PORT}..."
dotnet run --project "${REPO_ROOT}/backend/src/Atlas.Api" \
  --configuration Release --no-build --no-launch-profile \
  > "${LOG_FILE}" 2>&1 &
API_PID=$!

echo "Waiting for the API to become live..."
for _ in $(seq 1 60); do
  if curl --silent --fail --max-time 2 "http://127.0.0.1:${PORT}/health/live" > /dev/null 2>&1; then
    break
  fi
  if ! kill -0 "${API_PID}" 2>/dev/null; then
    echo "error: the API exited before becoming live. Output:" >&2
    cat "${LOG_FILE}" >&2
    exit 1
  fi
  sleep 1
done

if ! curl --silent --fail --max-time 5 "${DOCUMENT_URL}" -o "${OUTPUT_FILE}.raw"; then
  echo "error: could not fetch ${DOCUMENT_URL}. Output:" >&2
  cat "${LOG_FILE}" >&2
  exit 1
fi

# Normalise the formatting so the committed file produces a readable diff
# rather than one enormous line.
python3 -c "
import json, sys
with open('${OUTPUT_FILE}.raw', encoding='utf-8') as handle:
    document = json.load(handle)
with open('${OUTPUT_FILE}', 'w', encoding='utf-8') as handle:
    json.dump(document, handle, indent=2, sort_keys=True, ensure_ascii=False)
    handle.write('\n')
"
rm -f "${OUTPUT_FILE}.raw"

echo "Wrote ${OUTPUT_FILE#"${REPO_ROOT}/"}"
