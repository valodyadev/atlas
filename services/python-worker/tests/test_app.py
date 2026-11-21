"""Behaviour of the HTTP surface."""

from fastapi.testclient import TestClient

from python_worker.app import PROBLEM_JSON, create_app
from python_worker.config import Settings


def test_liveness_does_not_touch_dependencies(client: TestClient) -> None:
    response = client.get("/health/live")

    assert response.status_code == 200
    assert response.json() == {"status": "healthy"}


def test_readiness_is_separate_from_liveness(client: TestClient) -> None:
    response = client.get("/health/ready")

    assert response.status_code == 200
    assert response.json()["status"] == "healthy"


def test_meta_reports_the_environment(client: TestClient) -> None:
    response = client.get("/meta")

    assert response.status_code == 200
    body = response.json()
    assert body["service"] == "atlas-python-worker"
    assert body["environment"] == "development"


def test_unknown_route_returns_problem_json(client: TestClient) -> None:
    response = client.get("/does-not-exist")

    assert response.status_code == 404
    assert response.headers["content-type"].startswith(PROBLEM_JSON)
    body = response.json()
    assert body["status"] == 404
    assert body["instance"] == "GET /does-not-exist"


def test_validation_failure_returns_problem_json_with_field_errors() -> None:
    settings = Settings(environment="development", LOG_LEVEL="CRITICAL")
    app = create_app(settings)

    @app.get("/needs-a-number")
    async def needs_a_number(value: int) -> dict[str, int]:
        return {"value": value}

    with TestClient(app) as client:
        response = client.get("/needs-a-number", params={"value": "not-a-number"})

    assert response.status_code == 422
    assert response.headers["content-type"].startswith(PROBLEM_JSON)
    assert response.json()["errors"]


def test_docs_are_exposed_in_development(client: TestClient) -> None:
    assert client.get("/openapi.json").status_code == 200


def test_docs_are_hidden_in_production() -> None:
    # An interactive API explorer on a production service is an unnecessary
    # disclosure of the internal surface.
    settings = Settings(environment="production", LOG_LEVEL="CRITICAL")

    with TestClient(create_app(settings)) as client:
        assert client.get("/openapi.json").status_code == 404
        assert client.get("/docs").status_code == 404
        # The probes must keep working.
        assert client.get("/health/live").status_code == 200
