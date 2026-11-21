namespace Atlas.Api.IntegrationTests;

/// <summary>
/// Detects whether a container runtime is reachable, and smooths over the one
/// difference between the runtimes that would otherwise break these tests.
/// </summary>
/// <remarks>
/// Probed by looking for a daemon socket rather than by trying to start a
/// container, so a developer without a runtime gets a skipped test in
/// milliseconds instead of a 30-second connection timeout.
/// </remarks>
internal static class ContainerRuntime
{
    private const string DaemonSocketPath = "/var/run/docker.sock";
    private const string SocketOverrideVariable = "TESTCONTAINERS_DOCKER_SOCKET_OVERRIDE";

    private static readonly Lazy<string?> DetectedSocket = new(FindSocket);

    /// <summary>True when Testcontainers should be able to start a container.</summary>
    internal static bool IsAvailable =>
        !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("DOCKER_HOST"))
        || DetectedSocket.Value is not null;

    /// <summary>Why the tests were skipped, for the test output.</summary>
    internal const string SkipReason =
        "No container runtime detected (Docker Desktop, Colima, OrbStack or Podman). "
        + "These tests run in CI; see docs/runbooks/local-development.md to run them locally.";

    /// <summary>
    /// Points Testcontainers' resource reaper at the socket the daemon can
    /// actually see. Must be called before the first container is started.
    /// </summary>
    /// <remarks>
    /// Testcontainers bind-mounts the Docker socket into its Ryuk reaper
    /// container so that stray containers get cleaned up. On a VM-backed
    /// runtime - Colima, OrbStack, Podman, Docker Desktop - the client talks
    /// to a socket under the user's home directory, but the daemon runs
    /// inside the VM and cannot stat a macOS host path. Container creation
    /// then fails with
    ///
    ///     invalid mount config for type "bind": stat
    ///     /Users/you/.colima/default/docker.sock: operation not supported
    ///
    /// and because it happens in the collection fixture, every test in the
    /// suite fails with that one message.
    ///
    /// Inside the VM the daemon's socket is at the standard path, so that is
    /// what has to be mounted. Setting it here keeps `make test-backend`
    /// working whichever runtime the developer installed, instead of making
    /// each of them discover this for themselves. A developer who has already
    /// set the variable is left alone, as is Linux CI, where the detected
    /// socket is already the standard path.
    /// </remarks>
    internal static void ConfigureSocketMount()
    {
        if (!string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable(SocketOverrideVariable)))
        {
            return;
        }

        var socket = DetectedSocket.Value;
        if (socket is null || string.Equals(socket, DaemonSocketPath, StringComparison.Ordinal))
        {
            return;
        }

        Environment.SetEnvironmentVariable(SocketOverrideVariable, DaemonSocketPath);
    }

    /// <summary>The daemon socket found on this machine, or null.</summary>
    private static string? FindSocket()
    {
        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);

        string[] candidates =
        [
            DaemonSocketPath,                                        // Docker Engine on Linux, and inside any VM
            Path.Combine(home, ".docker", "run", "docker.sock"),     // Docker Desktop, macOS
            Path.Combine(home, ".colima", "default", "docker.sock"), // Colima
            Path.Combine(home, ".orbstack", "run", "docker.sock"),   // OrbStack
            Path.Combine(home, ".local", "share", "containers", "podman", "machine", "podman.sock"),
            "/run/podman/podman.sock",
        ];

        return Array.Find(candidates, File.Exists);
    }
}
