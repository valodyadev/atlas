using Atlas.Api.Endpoints;
using Atlas.Api.Extensions;
using Atlas.Modules.Identity;
using Atlas.ServiceDefaults;
using Atlas.ServiceDefaults.Modularity;
using Microsoft.AspNetCore.HttpOverrides;

var builder = WebApplication.CreateBuilder(args);

// ---------------------------------------------------------------------------
// Configuration
//
// Precedence (lowest to highest): appsettings.json -> appsettings.{Env}.json
// -> user secrets (Development only) -> environment variables -> command line.
// Nothing secret is ever committed; see docs/security/secrets.md.
// ---------------------------------------------------------------------------
builder.Configuration.AddEnvironmentVariables(prefix: "ATLAS_");

// ---------------------------------------------------------------------------
// Cross-cutting host defaults: telemetry, health, resilience, problem details.
// ---------------------------------------------------------------------------
builder.AddServiceDefaults();

// ---------------------------------------------------------------------------
// Transport concerns that apply to every module.
// ---------------------------------------------------------------------------
builder.AddApiAuthentication();
builder.AddApiRateLimiting();
builder.Services.AddApiVersioningDefaults();
builder.Services.AddApiDocumentation();

// Behind an ingress controller or load balancer, honour X-Forwarded-* so that
// request logs, redirects and the rate limiter see the real client.
builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
    options.KnownIPNetworks.Clear();
    options.KnownProxies.Clear();
});

// ---------------------------------------------------------------------------
// Modules.
//
// This list is the system's dependency graph. Adding a module is one line
// here; extracting one into its own service is deleting one line here and
// pointing the new host at the same module project.
// ---------------------------------------------------------------------------
builder.AddModules(
    new IdentityModule());

var app = builder.Build();

// ---------------------------------------------------------------------------
// Middleware pipeline. Order matters and is load-bearing.
// ---------------------------------------------------------------------------
app.UseForwardedHeaders();
app.UseExceptionHandler();      // -> problem+json, configured in AddServiceDefaults
app.UseStatusCodePages();
app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();

// ---------------------------------------------------------------------------
// Endpoints.
// ---------------------------------------------------------------------------
app.MapDefaultEndpoints();      // /health/live, /health/ready
app.MapApiDocumentation();      // /openapi/v1.json, /scalar

var v1 = app.NewVersionedApi("Atlas")
    .MapGroup("/api/v{version:apiVersion}")
    .HasApiVersion(1, 0);

v1.MapMetaEndpoints();
v1.MapModules();

await app.RunAsync();
