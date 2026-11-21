# syntax=docker/dockerfile:1.7
# ---------------------------------------------------------------------------
# Atlas Python worker.
#     docker build -t atlas-python-worker:dev services/python-worker
# ---------------------------------------------------------------------------

FROM python:3.13-slim-bookworm AS build
COPY --from=ghcr.io/astral-sh/uv:0.13.9 /uv /usr/local/bin/uv
WORKDIR /app

ENV UV_COMPILE_BYTECODE=1 \
    UV_LINK_MODE=copy \
    UV_PYTHON_DOWNLOADS=never

# Dependencies first, from the lockfile only, so a source edit does not
# invalidate the dependency layer.
COPY pyproject.toml uv.lock ./
RUN uv sync --locked --no-install-project --no-dev

COPY src ./src
RUN uv sync --locked --no-dev

FROM python:3.13-slim-bookworm AS final
WORKDIR /app

ENV PATH="/app/.venv/bin:$PATH" \
    PYTHONUNBUFFERED=1 \
    PYTHONDONTWRITEBYTECODE=1

# Run as a non-root user; nothing in here needs to write to the filesystem.
RUN groupadd --system --gid 1001 atlas \
    && useradd --system --uid 1001 --gid atlas --no-create-home atlas

COPY --from=build --chown=atlas:atlas /app/.venv /app/.venv
COPY --from=build --chown=atlas:atlas /app/src /app/src

USER atlas
EXPOSE 8082

CMD ["python", "-m", "python_worker.main"]
