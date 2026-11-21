import { loadConfig } from './config.js';
import { startTelemetry } from './telemetry.js';

// Telemetry must be initialised before the app (and therefore before any
// instrumented library) is imported. Hence the dynamic import below.
const config = loadConfig();
const telemetry = startTelemetry(config);

const { buildApp } = await import('./app.js');
const app = await buildApp(config);

/**
 * Graceful shutdown.
 *
 * Kubernetes sends SIGTERM and then waits. Draining in-flight requests and
 * flushing telemetry here is the difference between a rolling deploy that is
 * invisible to users and one that drops a handful of requests every time.
 */
async function shutdown(signal: string): Promise<void> {
  app.log.info({ signal }, 'shutting down');
  try {
    await app.close();
    await telemetry.shutdown();
    process.exit(0);
  } catch (error) {
    app.log.error({ err: error }, 'shutdown failed');
    process.exit(1);
  }
}

for (const signal of ['SIGTERM', 'SIGINT'] as const) {
  process.on(signal, () => {
    void shutdown(signal);
  });
}

try {
  await app.listen({ port: config.port, host: config.host });
} catch (error) {
  app.log.error({ err: error }, 'failed to start');
  process.exit(1);
}
