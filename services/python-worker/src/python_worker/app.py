"""FastAPI application factory."""

from collections.abc import AsyncIterator
from contextlib import asynccontextmanager
from typing import Any

import structlog
from fastapi import FastAPI, Request
from fastapi.exceptions import RequestValidationError
from fastapi.responses import JSONResponse
from opentelemetry.instrumentation.fastapi import FastAPIInstrumentor
from starlette.exceptions import HTTPException as StarletteHTTPException

from python_worker import __version__
from python_worker.config import Settings, get_settings
from python_worker.telemetry import configure_logging, configure_tracing

PROBLEM_JSON = "application/problem+json"

logger = structlog.get_logger(__name__)


def create_app(settings: Settings | None = None) -> FastAPI:
    """Build the application.

    A factory rather than a module-level singleton, so tests construct an app
    with their own settings instead of mutating global state.
    """
    resolved = settings or get_settings()

    configure_logging(resolved)
    configure_tracing(resolved)

    @asynccontextmanager
    async def lifespan(_app: FastAPI) -> AsyncIterator[None]:
        logger.info("starting", service=resolved.service_name, environment=resolved.environment)
        yield
        # Drain and flush here. Kubernetes sends SIGTERM and waits; the
        # difference between a clean rolling deploy and a handful of dropped
        # requests is what happens in this block.
        logger.info("stopped", service=resolved.service_name)

    app = FastAPI(
        title="Atlas Python Worker",
        version=__version__,
        lifespan=lifespan,
        # The interactive docs are a development affordance, not a production
        # endpoint.
        docs_url="/docs" if resolved.allows_diagnostics else None,
        redoc_url=None,
        openapi_url="/openapi.json" if resolved.allows_diagnostics else None,
    )

    _register_error_handlers(app)
    _register_routes(app, resolved)

    if resolved.otlp_endpoint:
        # Health probes would otherwise dominate trace volume and tell us
        # nothing.
        FastAPIInstrumentor.instrument_app(app, excluded_urls="health/.*")

    return app


def _problem(
    *, status: int, title: str, detail: str, request: Request, **extra: Any
) -> JSONResponse:
    """Build an RFC 9457 problem response.

    The same shape the .NET backend returns, so a client needs one error
    handler for the whole platform rather than one per service.
    """
    body: dict[str, Any] = {
        "title": title,
        "status": status,
        "detail": detail,
        "instance": f"{request.method} {request.url.path}",
        **extra,
    }
    return JSONResponse(status_code=status, content=body, media_type=PROBLEM_JSON)


def _register_error_handlers(app: FastAPI) -> None:
    @app.exception_handler(StarletteHTTPException)
    async def http_exception_handler(request: Request, exc: StarletteHTTPException) -> JSONResponse:
        return _problem(
            status=exc.status_code,
            title="Http Error",
            detail=str(exc.detail),
            request=request,
        )

    @app.exception_handler(RequestValidationError)
    async def validation_exception_handler(
        request: Request, exc: RequestValidationError
    ) -> JSONResponse:
        return _problem(
            status=422,
            title="Validation",
            detail="The request body or parameters are invalid.",
            request=request,
            errors=exc.errors(),
        )

    @app.exception_handler(Exception)
    async def unhandled_exception_handler(request: Request, exc: Exception) -> JSONResponse:
        logger.exception("unhandled error", path=request.url.path)
        # Never return the internal message: it leaks stack details and
        # sometimes credentials.
        return _problem(
            status=500,
            title="Internal Server Error",
            detail="An unexpected error occurred.",
            request=request,
        )


def _register_routes(app: FastAPI, settings: Settings) -> None:
    @app.get("/health/live", tags=["Diagnostics"])
    async def liveness() -> dict[str, str]:
        """Liveness: is the process healthy? Never touches a dependency."""
        return {"status": "healthy"}

    @app.get("/health/ready", tags=["Diagnostics"])
    async def readiness() -> dict[str, Any]:
        """Readiness: can this instance serve traffic right now?

        Add dependency checks here. Keep them out of the liveness probe, or a
        brief database blip restarts the whole fleet.
        """
        return {"status": "healthy", "checks": {}}

    @app.get("/meta", tags=["Diagnostics"])
    async def meta() -> dict[str, str]:
        """Build and environment metadata."""
        return {
            "service": settings.service_name,
            "environment": settings.environment,
            "version": __version__,
        }
