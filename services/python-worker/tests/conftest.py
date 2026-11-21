"""Shared test fixtures."""

from collections.abc import Iterator

import pytest
from fastapi.testclient import TestClient

from python_worker.app import create_app
from python_worker.config import Settings


@pytest.fixture
def settings() -> Settings:
    """Settings for a test run."""
    return Settings(environment="development", LOG_LEVEL="CRITICAL")


@pytest.fixture
def client(settings: Settings) -> Iterator[TestClient]:
    """A client bound to a freshly built app, with lifespan events run."""
    with TestClient(create_app(settings)) as test_client:
        yield test_client
