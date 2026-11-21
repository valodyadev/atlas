namespace Atlas.Api.IntegrationTests;

/// <summary>
/// Detects whether a container runtime is reachable.
/// </summary>
/// <remarks>
/// Probed by looking for a daemon socket rather than by trying to start a
/// container, so a developer without Docker gets a skipped test in
/// milliseconds instead of a 30-second connection timeout.
/// </remarks>
internal static class ContainerRuntime
{
    private static readonly Lazy<bool> Available = new(Probe);

    /// <summary>True when Testcontainers should be able to start a container.</summary>
    internal static bool IsAvailable => Available.Value;

    /// <summary>Why the tests were skipped, for the test output.</summary>
    internal const string SkipReason =
        "No container runtime detected (Docker Desktop, Colima, OrbStack or Podman). "
        + "These tests run in CI; see docs/runbooks/local-development.md to run them locally.";

    private static bool Probe()
    {
        // Explicit configuration wins: CI and remote-daemon setups set this.
        if (!string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("DOCKER_HOST")))
        {
            return true;
        }

        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);

        string[] candidateSockets =
        [
            "/var/run/docker.sock",                                  // Docker Desktop, Linux
            Path.Combine(home, ".docker", "run", "docker.sock"),     // Docker Desktop, macOS
            Path.Combine(home, ".colima", "default", "docker.sock"), // Colima
            Path.Combine(home, ".orbstack", "run", "docker.sock"),   // OrbStack
            Path.Combine(home, ".local", "share", "containers", "podman", "machine", "podman.sock"),
            "/run/podman/podman.sock",
        ];

        return candidateSockets.Any(File.Exists)
            || candidateSockets.Any(path => Directory.Exists(Path.GetDirectoryName(path) ?? string.Empty)
                                            && File.Exists(path));
    }
}
