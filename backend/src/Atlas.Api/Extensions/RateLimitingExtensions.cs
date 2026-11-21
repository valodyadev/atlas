using System.Globalization;
using System.Threading.RateLimiting;
using Atlas.Api.Options;

namespace Atlas.Api.Extensions;

/// <summary>
/// A global request-rate limit, partitioned per authenticated subject and
/// falling back to remote IP for anonymous callers.
/// </summary>
/// <remarks>
/// This is a last line of defence inside the process, not a replacement for an
/// edge WAF/CDN limit. It exists so that a single misbehaving mobile client
/// cannot exhaust database connections for everyone else.
/// </remarks>
internal static class RateLimitingExtensions
{
    internal static IHostApplicationBuilder AddApiRateLimiting(this IHostApplicationBuilder builder)
    {
        var options = builder.Configuration
            .GetSection(RateLimitOptions.SectionName)
            .Get<RateLimitOptions>() ?? new RateLimitOptions();

        builder.Services.AddRateLimiter(limiter =>
        {
            limiter.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

            limiter.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(context =>
            {
                var partitionKey =
                    context.User.FindFirst("sub")?.Value
                    ?? context.Connection.RemoteIpAddress?.ToString()
                    ?? "unknown";

                return RateLimitPartition.GetFixedWindowLimiter(partitionKey, _ => new FixedWindowRateLimiterOptions
                {
                    PermitLimit = options.PermitLimit,
                    Window = TimeSpan.FromSeconds(options.WindowSeconds),
                    QueueLimit = options.QueueLimit,
                    QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                });
            });

            // Tell well-behaved clients when to come back instead of making
            // them guess.
            limiter.OnRejected = (context, _) =>
            {
                context.HttpContext.Response.Headers.RetryAfter =
                    options.WindowSeconds.ToString(CultureInfo.InvariantCulture);
                return ValueTask.CompletedTask;
            };
        });

        return builder;
    }
}
