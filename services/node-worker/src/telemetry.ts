import { getNodeAutoInstrumentations } from '@opentelemetry/auto-instrumentations-node';
import { NodeSDK } from '@opentelemetry/sdk-node';

import type { Config } from './config.js';

/**
 * Starts OpenTelemetry.
 *
 * Must run before any instrumented module is imported, which is why
 * `server.ts` calls this first and imports the app lazily afterwards: the
 * auto-instrumentations patch `http`, `pg` and friends at require time, so
 * anything imported earlier is invisible in traces.
 *
 * No exporter is configured when there is no collector endpoint, so local runs
 * stay quiet instead of logging a failed export every few seconds.
 */
export function startTelemetry(config: Config): { shutdown: () => Promise<void> } {
  if (!config.otlpEndpoint) {
    return { shutdown: async () => {} };
  }

  const sdk = new NodeSDK({
    serviceName: config.serviceName,
    instrumentations: [
      getNodeAutoInstrumentations({
        // Health probes would otherwise dominate trace volume.
        '@opentelemetry/instrumentation-http': {
          ignoreIncomingRequestHook: (request) => (request.url ?? '').startsWith('/health'),
        },
        // Noisy, rarely actionable.
        '@opentelemetry/instrumentation-fs': { enabled: false },
      }),
    ],
  });

  sdk.start();

  return {
    shutdown: async () => {
      await sdk.shutdown();
    },
  };
}
