using DotNetBoostSample.Api.Data;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Testcontainers.PostgreSql;
using Xunit;

namespace DotNetBoostSample.Tests.Integration;

/// <summary>
/// The real application, on a real PostgreSQL, for one test class run.
///
/// <para><b>Why this exists.</b> Every other test in this project reads attributes and type
/// metadata: fast, infrastructure-free, and unable to see any of what the README actually claims.
/// That a plain <c>ToListAsync()</c> comes back translated, that alt text written through one
/// library reads back beside a file owned by another, that a settings value changes what an
/// endpoint answers without a restart — all of it is behaviour of three libraries wired into one
/// <c>DbContext</c>, and none of it is provable without the database they share. The bug fixed in
/// 9d3f782 — a translated name written into the settings cache — was invisible to every test here
/// and would have been caught by the first one below.</para>
///
/// <para><b>It boots the real Program.cs.</b> Not a trimmed-down host: the same three
/// registrations, the same interceptors, the same middleware order. A test host assembled
/// differently would prove something about itself.</para>
///
/// <para><b>Two things are overridden</b>, both infrastructure rather than composition: the
/// connection string, so the run gets a throwaway database, and <c>Media:Root</c>, so uploaded
/// bytes land in a temporary directory instead of the repository's media folder.</para>
///
/// <para>Postgres comes from Testcontainers. The README already asks for a container runtime to
/// run the sample at all, so this adds no prerequisite — and where there is no Docker the tests
/// skip rather than fail, so <c>dotnet test</c> stays useful on a machine without it.</para>
/// </summary>
public sealed class SampleApp : IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder("postgres:17-alpine")
        .WithDatabase("catalog")
        .WithCleanUp(true)
        .Build();

    private readonly string _mediaRoot =
        Path.Combine(Path.GetTempPath(), "dnb-sample-tests", Guid.NewGuid().ToString("N"));

    private WebApplicationFactory<Program>? _factory;

    /// <summary>Why the run was skipped, or null when the application is up.</summary>
    public string? Unavailable { get; private set; }

    /// <summary>
    /// A client for the running application. Call <see cref="SkipIfUnavailable"/> first — this
    /// throws rather than returning a client that cannot work.
    /// </summary>
    public HttpClient Client => _factory?.CreateClient()
        ?? throw new InvalidOperationException(Unavailable ?? "The application is not running.");

    public async Task InitializeAsync()
    {
        // Nothing to start, and nothing will run: [RequiresDockerFact] has already skipped every
        // test in the collection. Constructing the container anyway would throw here instead.
        if (!ContainerRuntime.Available)
        {
            Unavailable = "No container runtime found.";
            return;
        }

        try
        {
            await _postgres.StartAsync();
        }
        catch (Exception ex)
        {
            // A runtime was found but would not start one — recorded rather than thrown, so the
            // failure surfaces on the test that needed it, with this reason attached.
            Unavailable = $"PostgreSQL would not start: {ex.GetType().Name}: {ex.Message}";
            return;
        }

        Directory.CreateDirectory(_mediaRoot);

        _factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseSetting("ConnectionStrings:catalog", _postgres.GetConnectionString());
            builder.UseSetting("Media:Root", _mediaRoot);
        });

        // Forces the host to build and Program.cs's seeding to run before the first test, so a
        // failure in startup is reported here rather than inside whichever test ran first.
        _ = _factory.CreateClient();
    }

    public async Task DisposeAsync()
    {
        if (_factory is not null) await _factory.DisposeAsync();

        if (ContainerRuntime.Available) await _postgres.DisposeAsync();

        // Best effort: the bytes are in the system temp directory either way.
        try { if (Directory.Exists(_mediaRoot)) Directory.Delete(_mediaRoot, recursive: true); }
        catch (IOException) { /* a file still open on Windows; the OS will clear temp */ }
    }

    /// <summary>
    /// The application's own <see cref="AppDbContext"/>, for the few assertions that are about what
    /// is in the tables rather than what the API answers — the translation rows a delete should
    /// have taken with it, for one.
    /// </summary>
    public async Task WithDbAsync(Func<AppDbContext, Task> work)
    {
        using var scope = _factory!.Services.CreateScope();
        await work(scope.ServiceProvider.GetRequiredService<AppDbContext>());
    }

    /// <summary>Resets the catalogue settings group, so one test's edit cannot reach another.</summary>
    public Task ResetCatalogSettingsAsync() =>
        Client.PostAsJsonChecked("/api/settings/catalog", new
        {
            pageSize = 12,
            currencySymbol = "$"
        });
}

/// <summary>
/// One application for every test in the collection — starting a container and seeding per test
/// class would cost more than the tests do.
/// </summary>
[CollectionDefinition(Name)]
public sealed class SampleAppCollection : ICollectionFixture<SampleApp>
{
    public const string Name = "sample-app";
}
