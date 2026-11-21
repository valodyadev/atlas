"""Configuration validation."""

import pytest
from pydantic import ValidationError

from python_worker.config import Settings


def test_defaults_are_sane() -> None:
    settings = Settings()

    assert settings.environment == "development"
    assert settings.port == 8082
    assert settings.service_name == "atlas-python-worker"
    assert settings.otlp_endpoint is None


def test_port_is_read_from_the_environment(monkeypatch: pytest.MonkeyPatch) -> None:
    monkeypatch.setenv("PYTHON_WORKER_PORT", "9002")

    assert Settings().port == 9002


def test_invalid_environment_fails_fast() -> None:
    with pytest.raises(ValidationError):
        Settings(environment="prod")  # type: ignore[arg-type]


def test_out_of_range_port_fails_fast(monkeypatch: pytest.MonkeyPatch) -> None:
    monkeypatch.setenv("PYTHON_WORKER_PORT", "70000")

    with pytest.raises(ValidationError):
        Settings()


def test_diagnostics_are_disabled_only_in_production() -> None:
    assert Settings(environment="development").allows_diagnostics
    assert Settings(environment="staging").allows_diagnostics
    assert not Settings(environment="production").allows_diagnostics


def test_settings_are_immutable() -> None:
    # Frozen settings mean no code path can reconfigure the service at runtime.
    settings = Settings()

    with pytest.raises(ValidationError):
        settings.port = 1234
