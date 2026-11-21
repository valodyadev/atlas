import { describe, expect, it } from 'vitest';

import { buildApp } from '../src/app.js';
import { loadConfig } from '../src/config.js';

const config = loadConfig({ ENVIRONMENT: 'development', LOG_LEVEL: 'fatal' });

describe('health endpoints', () => {
  it('reports liveness without touching dependencies', async () => {
    const app = await buildApp(config);
    try {
      const response = await app.inject({ method: 'GET', url: '/health/live' });

      expect(response.statusCode).toBe(200);
      expect(response.json()).toEqual({ status: 'healthy' });
    } finally {
      await app.close();
    }
  });

  it('reports readiness separately from liveness', async () => {
    const app = await buildApp(config);
    try {
      const response = await app.inject({ method: 'GET', url: '/health/ready' });

      expect(response.statusCode).toBe(200);
      expect(response.json()).toMatchObject({ status: 'healthy' });
    } finally {
      await app.close();
    }
  });

  it('reports build metadata', async () => {
    const app = await buildApp(config);
    try {
      const response = await app.inject({ method: 'GET', url: '/meta' });

      expect(response.statusCode).toBe(200);
      expect(response.json()).toMatchObject({
        service: 'atlas-node-worker',
        environment: 'development',
      });
    } finally {
      await app.close();
    }
  });
});

describe('error handling', () => {
  it('returns RFC 9457 problem+json for an unknown route', async () => {
    const app = await buildApp(config);
    try {
      const response = await app.inject({ method: 'GET', url: '/does-not-exist' });

      expect(response.statusCode).toBe(404);
      expect(response.headers['content-type']).toContain('application/problem+json');

      const problem = response.json<Record<string, unknown>>();
      expect(problem['status']).toBe(404);
      expect(problem['traceId']).toBeTruthy();
    } finally {
      await app.close();
    }
  });

  it('reuses the W3C trace id so logs correlate with the rest of the platform', async () => {
    const app = await buildApp(config);
    try {
      const traceId = '0af7651916cd43dd8448eb211c80319c';
      const response = await app.inject({
        method: 'GET',
        url: '/does-not-exist',
        headers: { traceparent: `00-${traceId}-b7ad6b7169203331-01` },
      });

      expect(response.json<Record<string, unknown>>()['traceId']).toBe(traceId);
    } finally {
      await app.close();
    }
  });
});
