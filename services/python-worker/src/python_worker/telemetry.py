"""OpenTelemetry wiring."""

import logging
import sys

import structlog
from opentelemetry import trace
from opentelemetry.exporter.otlp.proto.grpc.trace_exporter import OTLPSpanExporter
from opentelemetry.sdk.resources import Resource
from opentelemetry.sdk.trace import TracerProvider
from opentelemetry.sdk.trace.export import BatchSpanProcessor

from python_worker.config import Settings


def configure_logging(settings: Settings) -> None:
    """Emit structured JSON logs to stdout.

    stdout, because that is what the platform collects; JSON, because a log
    line that cannot be parsed cannot be alerted on. Trace and span ids are
    merged in so a log line can be pivoted to the trace that produced it.
    """
    logging.basicConfig(
        format="%(message)s",
        stream=sys.stdout,
        level=getattr(logging, settings.log_level.upper(), logging.INFO),
    )

    structlog.configure(
        processors=[
            structlog.contextvars.merge_contextvars,
            structlog.processors.add_log_level,
            structlog.processors.TimeStamper(fmt="iso", utc=True),
            structlog.processors.dict_tracebacks,
            _add_trace_context,
            structlog.processors.JSONRenderer(),
        ],
        wrapper_class=structlog.make_filtering_bound_logger(
            getattr(logging, settings.log_level.upper(), logging.INFO)
        ),
        logger_factory=structlog.PrintLoggerFactory(),
        cache_logger_on_first_use=True,
    )


def _add_trace_context(
    _logger: object, _name: str, event_dict: structlog.types.EventDict
) -> structlog.types.EventDict:
    """Attach the active trace and span ids to every log line."""
    span = trace.get_current_span()
    context = span.get_span_context()
    if context.is_valid:
        event_dict["trace_id"] = format(context.trace_id, "032x")
        event_dict["span_id"] = format(context.span_id, "016x")
    return event_dict


def configure_tracing(settings: Settings) -> None:
    """Install the tracer provider, if a collector is configured.

    With no endpoint the exporter is skipped entirely, so local runs do not
    log an export failure every few seconds.
    """
    if not settings.otlp_endpoint:
        return

    provider = TracerProvider(
        resource=Resource.create(
            {
                "service.name": settings.service_name,
                "deployment.environment": settings.environment,
            }
        )
    )
    provider.add_span_processor(
        BatchSpanProcessor(OTLPSpanExporter(endpoint=settings.otlp_endpoint))
    )
    trace.set_tracer_provider(provider)
