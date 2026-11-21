import { z } from 'zod';

/**
 * Configuration schema.
 *
 * Validated at startup and nowhere else. A service that reads
 * `process.env.SOMETHING` deep inside a request handler fails in production,
 * under load, for one unlucky user. This fails immediately, on boot, with a
 * message naming the variable.
 */
const configSchema = z.object({
  environment: z.enum(['development', 'staging', 'production']).default('development'),
  port: z.coerce.number().int().min(1).max(65535).default(8081),
  host: z.string().default('0.0.0.0'),
  logLevel: z.enum(['trace', 'debug', 'info', 'warn', 'error', 'fatal']).default('info'),
  serviceName: z.string().default('atlas-node-worker'),
  otlpEndpoint: z.url().optional(),
});

export type Config = z.infer<typeof configSchema>;

export function loadConfig(env: NodeJS.ProcessEnv = process.env): Config {
  const result = configSchema.safeParse({
    environment: env['ENVIRONMENT'],
    port: env['NODE_WORKER_PORT'] ?? env['PORT'],
    host: env['HOST'],
    logLevel: env['LOG_LEVEL']?.toLowerCase(),
    serviceName: env['OTEL_SERVICE_NAME'],
    otlpEndpoint: env['OTEL_EXPORTER_OTLP_ENDPOINT'],
  });

  if (!result.success) {
    const issues = result.error.issues
      .map((issue) => `  ${issue.path.join('.') || '(root)'}: ${issue.message}`)
      .join('\n');
    throw new Error(`Invalid configuration:\n${issues}`);
  }

  return result.data;
}
