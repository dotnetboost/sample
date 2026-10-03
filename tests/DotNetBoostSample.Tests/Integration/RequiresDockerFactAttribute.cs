using Xunit;

namespace DotNetBoostSample.Tests.Integration;

/// <summary>
/// A <see cref="FactAttribute"/> that skips itself when the machine has no container runtime.
///
/// <para>The integration tests need a real PostgreSQL, which Testcontainers starts in Docker. The
/// README already asks for a container runtime to run the sample at all, so this adds no
/// prerequisite — but <c>dotnet test</c> should still be useful to someone who has only cloned the
/// repository, and a wall of red that means "Docker is not running" teaches them nothing about the
/// code.</para>
///
/// <para>Skipped rather than silently passing: a test that reports success without having run is
/// worse than one that reports nothing, and the runner prints the reason beside each.</para>
///
/// <para>xUnit 2 decides skipping at DISCOVERY, through this attribute's <see cref="FactAttribute.Skip"/>,
/// so the check has to be cheap and cannot await anything. It looks for a socket rather than
/// shelling out to <c>docker info</c>, which would cost a process per test.</para>
/// </summary>
public sealed class RequiresDockerFactAttribute : FactAttribute
{
    public RequiresDockerFactAttribute()
    {
        if (!ContainerRuntime.Available)
        {
            Skip = "No container runtime found, so this integration test was skipped. "
                 + "Start Docker Desktop, OrbStack, Colima or Podman and run `dotnet test` again.";
        }
    }
}

/// <summary>Whether something that can start a container appears to be running.</summary>
internal static class ContainerRuntime
{
    /// <summary>Probed once per test run — the answer cannot change mid-run in any useful way.</summary>
    public static readonly bool Available = Probe();

    private static bool Probe()
    {
        // An explicit endpoint wins: whoever set it knows better than this guess does.
        if (!string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("DOCKER_HOST"))
            || !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("TESTCONTAINERS_DOCKER_SOCKET_OVERRIDE")))
        {
            return true;
        }

        if (OperatingSystem.IsWindows())
        {
            // The engine's named pipe. Enumerating the pipe directory is the cheap way to ask.
            try { return Directory.GetFiles(@"\\.\pipe\").Any(p => p.Contains("docker_engine", StringComparison.OrdinalIgnoreCase)); }
            catch (IOException) { return false; }
        }

        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);

        // Docker Desktop, then the ones that put their socket under the user's home: Docker
        // Desktop's rootless path, OrbStack, Colima, Rancher Desktop, and Podman's user socket.
        string[] sockets =
        [
            "/var/run/docker.sock",
            Path.Combine(home, ".docker", "run", "docker.sock"),
            Path.Combine(home, ".orbstack", "run", "docker.sock"),
            Path.Combine(home, ".colima", "default", "docker.sock"),
            Path.Combine(home, ".rd", "docker.sock"),
            $"/run/user/{Environment.GetEnvironmentVariable("UID") ?? "1000"}/podman/podman.sock"
        ];

        // A unix socket is a file as far as the file system is concerned.
        return sockets.Any(File.Exists);
    }
}
