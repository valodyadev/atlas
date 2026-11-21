"""Configuration, validated once at startup."""

from functools import lru_cache
from typing import Literal

from pydantic import Field
from pydantic_settings import BaseSettings, SettingsConfigDict

Environment = Literal["development", "staging", "production"]


class Settings(BaseSettings):
    """Service configuration, read from the environment.

    Validated at import time by :func:`get_settings`, so a missing or
    malformed variable is a startup failure with a clear message rather than a
    500 for one unlucky request an hour later.
    """

    model_config = SettingsConfigDict(
        env_file=None,  # the platform injects environment variables; no .env in production
        extra="ignore",
        frozen=True,
    )

    environment: Environment = "development"
    port: int = Field(default=8082, ge=1, le=65535, alias="PYTHON_WORKER_PORT")
    host: str = "0.0.0.0"  # noqa: S104 - binding all interfaces is correct inside a container
    log_level: str = Field(default="INFO", alias="LOG_LEVEL")
    service_name: str = Field(default="atlas-python-worker", alias="OTEL_SERVICE_NAME")
    otlp_endpoint: str | None = Field(default=None, alias="OTEL_EXPORTER_OTLP_ENDPOINT")

    @property
    def allows_diagnostics(self) -> bool:
        """Whether verbose diagnostics may be exposed."""
        return self.environment != "production"


@lru_cache(maxsize=1)
def get_settings() -> Settings:
    """Return the process-wide settings, read once."""
    return Settings()
