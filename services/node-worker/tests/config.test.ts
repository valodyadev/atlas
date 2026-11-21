import { describe, expect, it } from 'vitest';

import { loadConfig } from '../src/config.js';

describe('loadConfig', () => {
  it('applies defaults for an empty environment', () => {
    const config = loadConfig({});

    expect(config).toMatchObject({
      environment: 'development',
      port: 8081,
      host: '0.0.0.0',
      logLevel: 'info',
    });
  });

  it('coerces the port from a string', () => {
    expect(loadConfig({ NODE_WORKER_PORT: '9001' }).port).toBe(9001);
  });

  it('fails fast and names the offending variable', () => {
    // The point of validating at boot: this is a startup crash with a clear
    // message, not a 500 for one user three hours later.
    expect(() => loadConfig({ ENVIRONMENT: 'prod' })).toThrow(/environment/);
    expect(() => loadConfig({ NODE_WORKER_PORT: 'eighty' })).toThrow(/port/);
    expect(() => loadConfig({ OTEL_EXPORTER_OTLP_ENDPOINT: 'not-a-url' })).toThrow(/otlpEndpoint/);
  });

  it('accepts a valid collector endpoint', () => {
    expect(loadConfig({ OTEL_EXPORTER_OTLP_ENDPOINT: 'http://localhost:4317' }).otlpEndpoint).toBe(
      'http://localhost:4317',
    );
  });
});
