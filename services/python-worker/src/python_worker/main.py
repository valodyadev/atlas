"""Entry point.

uv run python -m python_worker.main
"""

import uvicorn

from python_worker.app import create_app
from python_worker.config import get_settings


def main() -> None:
    """Run the service."""
    settings = get_settings()
    uvicorn.run(
        create_app(settings),
        host=settings.host,
        port=settings.port,
        # Uvicorn's own access log is replaced by structured logging in the app.
        access_log=False,
        log_config=None,
    )


if __name__ == "__main__":
    main()
