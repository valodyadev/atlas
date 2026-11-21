# syntax=docker/dockerfile:1.7
# ---------------------------------------------------------------------------
# Atlas Node worker.
#     docker build -t atlas-node-worker:dev services/node-worker
# ---------------------------------------------------------------------------

FROM node:26-bookworm-slim AS deps
WORKDIR /app
COPY package.json package-lock.json ./
# `npm ci` installs exactly the lockfile - never a resolved-at-build-time tree.
RUN npm ci --ignore-scripts

FROM deps AS build
COPY tsconfig.json ./
COPY src ./src
RUN npm run build

FROM node:26-bookworm-slim AS final
WORKDIR /app
ENV NODE_ENV=production
COPY package.json package-lock.json ./
RUN npm ci --omit=dev --ignore-scripts && npm cache clean --force
COPY --from=build /app/dist ./dist

# The node image ships a non-root `node` user; use it.
USER node
EXPOSE 8081

# No shell-based healthcheck: the orchestrator probes /health/live over HTTP.
CMD ["node", "dist/server.js"]
