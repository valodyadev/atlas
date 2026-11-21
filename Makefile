# ===========================================================================
# Atlas - one entry point for every routine task.
#
# Run `make` to list targets. Everything here works on a clean checkout and
# is the same thing CI runs, so "works on my machine" and "passes CI" cannot
# drift apart.
# ===========================================================================

SHELL := /usr/bin/env bash
.SHELLFLAGS := -eu -o pipefail -c
.DEFAULT_GOAL := help

# The Homebrew .NET formula is keg-only, so its bin directory is not on PATH
# by default. Add it when present, rather than making every developer edit
# their shell profile before `make test` works.
DOTNET_KEG := $(shell [ -d /opt/homebrew/opt/dotnet@10/bin ] && echo /opt/homebrew/opt/dotnet@10/bin || true)
ifneq ($(DOTNET_KEG),)
export PATH := $(DOTNET_KEG):$(PATH)
endif

export DOTNET_NOLOGO := true
export DOTNET_CLI_TELEMETRY_OPTOUT := true

# --env-file is required, not optional: passing -f makes compose treat
# infra/docker/ as the project directory, so it would look for .env there and
# silently miss the one at the repository root.
COMPOSE := docker compose --env-file .env -f infra/docker/compose.yaml
BACKEND := backend
IOS_PACKAGE := ios/Packages/AtlasKit

# ---------------------------------------------------------------------------
# Help
# ---------------------------------------------------------------------------
.PHONY: help
help: ## Show this help
	@echo "Atlas - available targets:"
	@echo
	@grep -hE '^[a-zA-Z0-9_-]+:.*?## .*$$' $(MAKEFILE_LIST) \
		| awk 'BEGIN {FS = ":.*?## "}; {printf "  \033[36m%-22s\033[0m %s\n", $$1, $$2}'
	@echo

# ---------------------------------------------------------------------------
# Setup
# ---------------------------------------------------------------------------
.PHONY: doctor
doctor: ## Report which toolchains are present and which are missing
	@./tools/scripts/check-env.sh

.PHONY: bootstrap
bootstrap: ## Install dependencies for every part of the repo
	@./tools/scripts/bootstrap.sh

.PHONY: install-hooks
install-hooks: ## Install the git pre-commit hook
	@git rev-parse --git-dir >/dev/null 2>&1 || { echo "Not a git repository yet. Run: make git-init"; exit 1; }
	@git config core.hooksPath tools/git-hooks
	@echo "Hooks enabled (core.hooksPath=tools/git-hooks)."

.PHONY: git-init
git-init: ## Initialise the git repository (does not commit)
	@if git rev-parse --git-dir >/dev/null 2>&1; then \
		echo "Already a git repository."; \
	else \
		git init -b main && echo "Initialised. Review 'git status', then make your first commit."; \
	fi

# ---------------------------------------------------------------------------
# Local stack
# ---------------------------------------------------------------------------
.PHONY: up
up: ## Start the full local stack (Postgres, collector, API, workers)
	$(COMPOSE) up -d --build
	@echo
	@echo "  API      http://localhost:8080/scalar"
	@echo "  Traces   http://localhost:16686"
	@echo "  Node     http://localhost:8081/health/live"
	@echo "  Python   http://localhost:8082/health/live"

.PHONY: deps
deps: ## Start only the backing services (run the app from your IDE)
	$(COMPOSE) up -d postgres otel-collector jaeger

.PHONY: down
down: ## Stop the stack, keeping data
	$(COMPOSE) down

.PHONY: nuke
nuke: ## Stop the stack and DELETE all local data
	$(COMPOSE) down --volumes --remove-orphans

.PHONY: logs
logs: ## Tail logs from the stack
	$(COMPOSE) logs -f --tail=100

# ---------------------------------------------------------------------------
# Build
# ---------------------------------------------------------------------------
.PHONY: build
build: build-backend build-ios build-services ## Build everything

.PHONY: build-backend
build-backend: ## Build the .NET solution (Release)
	cd $(BACKEND) && dotnet build --configuration Release

.PHONY: build-ios
build-ios: ## Build the iOS shared packages (no Xcode required)
	cd $(IOS_PACKAGE) && swift build -Xswiftc -warnings-as-errors

.PHONY: build-services
build-services: ## Build the satellite services
	cd services/node-worker && npm run build

.PHONY: ios-project
ios-project: ## Generate ios/Atlas.xcodeproj from project.yml
	cd ios && xcodegen generate
	@echo "Open ios/Atlas.xcodeproj"

# ---------------------------------------------------------------------------
# Test
# ---------------------------------------------------------------------------
.PHONY: test
test: test-backend test-ios test-services ## Run every test suite

.PHONY: test-backend
test-backend: ## Run the .NET tests (integration tests skip without Docker)
	cd $(BACKEND) && dotnet test

.PHONY: test-ios
test-ios: ## Run the iOS package tests
	cd $(IOS_PACKAGE) && swift test -Xswiftc -warnings-as-errors

.PHONY: test-services
test-services: ## Run the satellite service tests
	cd services/node-worker && npm test
	cd services/python-worker && uv run pytest

# ---------------------------------------------------------------------------
# Quality
# ---------------------------------------------------------------------------
.PHONY: lint
lint: ## Run every linter and formatting check (does not modify files)
	cd $(BACKEND) && dotnet format --verify-no-changes
	cd ios && swiftformat --lint .
	@command -v swiftlint >/dev/null 2>&1 && (cd ios && swiftlint lint --strict) \
		|| echo "  skipping swiftlint (not on PATH - brew fails on macOS 27; use the portable_swiftlint.zip release)"
	cd services/node-worker && npm run lint && npm run typecheck && npm run format:check
	cd services/python-worker && uv run ruff check . && uv run ruff format --check . && uv run mypy

.PHONY: format
format: ## Auto-format everything
	cd $(BACKEND) && dotnet format
	cd ios && swiftformat .
	cd services/node-worker && npm run format
	cd services/python-worker && uv run ruff format . && uv run ruff check --fix .

# ---------------------------------------------------------------------------
# Database
# ---------------------------------------------------------------------------
.PHONY: migrate
migrate: ## Apply pending migrations to the local database
	cd $(BACKEND) && dotnet run --project src/Atlas.DbMigrator

# make migration NAME=AddOrdersTable MODULE=Identity
.PHONY: migration
migration: ## Create a migration: make migration NAME=Thing MODULE=Identity
	@test -n "$(NAME)" || { echo "Usage: make migration NAME=AddThing MODULE=Identity"; exit 1; }
	@test -n "$(MODULE)" || { echo "Usage: make migration NAME=AddThing MODULE=Identity"; exit 1; }
	cd $(BACKEND) && dotnet tool restore && dotnet ef migrations add $(NAME) \
		--project src/Modules/Atlas.Modules.$(MODULE) \
		--startup-project src/Modules/Atlas.Modules.$(MODULE) \
		--context $(MODULE)DbContext \
		--output-dir Infrastructure/Migrations

.PHONY: migration-sql
migration-sql: ## Print the idempotent SQL for a module: make migration-sql MODULE=Identity
	@test -n "$(MODULE)" || { echo "Usage: make migration-sql MODULE=Identity"; exit 1; }
	cd $(BACKEND) && dotnet tool restore && dotnet ef migrations script --idempotent \
		--project src/Modules/Atlas.Modules.$(MODULE) \
		--startup-project src/Modules/Atlas.Modules.$(MODULE) \
		--context $(MODULE)DbContext

# ---------------------------------------------------------------------------
# Contracts
# ---------------------------------------------------------------------------
.PHONY: openapi
openapi: ## Regenerate contracts/openapi/v1.json from the running API
	@./tools/scripts/export-openapi.sh

# ---------------------------------------------------------------------------
# Housekeeping
# ---------------------------------------------------------------------------
.PHONY: clean
clean: ## Remove build output (keeps dependencies)
	rm -rf $(BACKEND)/artifacts
	rm -rf $(IOS_PACKAGE)/.build
	rm -rf ios/Atlas.xcodeproj
	rm -rf services/node-worker/dist
	find . -type d -name __pycache__ -prune -exec rm -rf {} + 2>/dev/null || true
	@echo "Clean."
