import helmet from '@fastify/helmet';
import sensible from '@fastify/sensible';
import Fastify, { type FastifyError, type FastifyInstance } from 'fastify';

import type { Config } from './config.js';

/**
 * Builds the Fastify instance.
 *
 * Separated from `server.ts` so tests can call `app.inject()` against the real
 * routing, serialisation and error handling without binding a port.
 */
export async function buildApp(config: Config): Promise<FastifyInstance> {
  const app = Fastify({
    // Structured JSON to stdout: the platform collects stdout, and a log line
    // that is not machine-readable cannot be alerted on.
    logger: {
      level: config.logLevel,
      base: { service: config.serviceName, environment: config.environment },
      redact: {
        paths: ['req.headers.authorization', 'req.headers.cookie'],
        censor: '[redacted]',
      },
    },
    // Trust the ingress controller's forwarding headers so logs and rate
    // limits see the real client address.
    trustProxy: true,
    // Correlate with the rest of the platform: reuse the W3C trace header as
    // the request id rather than minting an unrelated one.
    genReqId: (request) => {
      const traceparent = request.headers['traceparent'];
      if (typeof traceparent === 'string') {
        const [, traceId] = traceparent.split('-');
        if (traceId) return traceId;
      }
      return crypto.randomUUID();
    },
    disableRequestLogging: false,
  });

  await app.register(helmet);
  await app.register(sensible);

  // RFC 9457 problem+json, matching what the .NET backend returns, so a client
  // needs exactly one error handler for the whole platform.
  app.setErrorHandler((error: FastifyError, request, reply) => {
    const status = error.statusCode ?? 500;

    if (status >= 500) {
      request.log.error({ err: error }, 'unhandled error');
    }

    void reply
      .status(status)
      .type('application/problem+json')
      .send({
        title: error.name,
        status,
        // Never leak an internal message to a caller on a 500.
        detail: status >= 500 ? 'An unexpected error occurred.' : error.message,
        instance: `${request.method} ${request.url}`,
        traceId: request.id,
      });
  });

  // Fastify answers unknown routes through its not-found handler, which never
  // reaches setErrorHandler. Without this, a typo'd URL returns plain
  // application/json and breaks the one-error-shape guarantee.
  app.setNotFoundHandler((request, reply) => {
    void reply
      .status(404)
      .type('application/problem+json')
      .send({
        title: 'Not Found',
        status: 404,
        detail: `No route for ${request.method} ${request.url}.`,
        instance: `${request.method} ${request.url}`,
        traceId: request.id,
      });
  });

  // --- Liveness: process only, never a dependency -------------------------
  app.get('/health/live', () => ({ status: 'healthy' }));

  // --- Readiness: can this instance serve traffic right now? --------------
  // Add dependency checks here (database, queue, upstream API). Keep them out
  // of /health/live, or a brief outage restarts the whole fleet.
  app.get('/health/ready', () => ({ status: 'healthy', checks: {} }));

  app.get('/meta', () => ({
    service: config.serviceName,
    environment: config.environment,
    version: process.env['SERVICE_VERSION'] ?? '0.1.0',
  }));

  return app;
}
