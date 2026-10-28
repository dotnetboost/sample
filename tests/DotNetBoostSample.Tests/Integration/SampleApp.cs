using DotNetBoostSample.Infrastructure.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
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
/// endpoint answers without a restart — all of it is behaviour of four libraries wired into one
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
public class SampleApp : IAsyncLifetime
{
    /// <summary>
    /// <c>MultiLingualOptions.BaseLanguage</c> for this run, or null for the mode the sample ships
    /// in. Overridden by <c>BaseLanguageSampleApp</c>, which boots the same application the other
    /// way — see <c>BaseLanguageTests</c> for why that is worth a second container.
    /// </summary>
    protected virtual string? BaseLanguage => null;

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
    /// The id of the seeded <c>furniture</c> category — what a product created by a test is filed
    /// under, since <c>categoryId</c> is required and has to name one that exists.
    ///
    /// <para>Asked for rather than written down as <c>1</c>, which is what nine of these tests
    /// did. That literal is not wrong today — every fixture gets a fresh container and the seed
    /// creates its categories first, so the first id the sequence hands out is 1 and belongs to
    /// furniture. It is an undeclared dependency on both of those facts, and it fails badly in
    /// either direction: if no category has id 1 any more, a test about Arabic text answers 404 on its product
    /// create and names the seed nowhere; and if id 1 is simply a DIFFERENT category — one added
    /// above furniture in <c>DatabaseInitialiser</c> — nothing fails at all, the products are
    /// just filed somewhere nobody meant, which is the worse of the two.</para>
    ///
    /// <para>The slug is what the seed actually promises, so it is what this asks by.</para>
    ///
    /// <para>Read once per fixture rather than per test, from the same API the tests use: after
    /// seeding, before the first test, and over a listing that is deliberately not paged.</para>
    /// </summary>
    public int SeededCategoryId { get; private set; }

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

            // The third override, and the only one that is not infrastructure: it decides where a
            // [MultiLingual] property is STORED, so a run with it set exercises a different schema
            // on the same code. Left alone, the host reads appsettings.json and finds it empty.
            if (BaseLanguage is not null)
                builder.UseSetting("MultiLingual:BaseLanguage", BaseLanguage);
        });

        // Forces the host to build and Program.cs's seeding to run before the first test, so a
        // failure in startup is reported here rather than inside whichever test ran first.
        var client = _factory.CreateClient();

        // And the one seeded id the tests need, read now that there is a catalogue to read it
        // from. By slug, because that is the name the seed actually promises — see
        // SeededCategoryId.
        SeededCategoryId = (await client.GetJsonAsync("/api/categories"))
            .EnumerateArray().First(c => c.Str("slug") == "furniture").Int("id");
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

    /// <summary>
    /// One value from the database, for the assertions that are about which COLUMN content is in
    /// rather than what the API answers — the one thing base-language mode changes that no
    /// response can show.
    /// </summary>
    public async Task<string?> ScalarAsync(string sql)
    {
        string? value = null;

        await WithDbAsync(async db =>
        {
            await using var command = db.Database.GetDbConnection().CreateCommand();
            command.CommandText = sql;

            await db.Database.OpenConnectionAsync();
            value = (await command.ExecuteScalarAsync())?.ToString();
        });

        return value;
    }

    /// <summary>
    /// A connection string for another database in this run's PostgreSQL — for the one test that
    /// needs a database of its own to change the schema of, without touching the one every other
    /// test in the collection shares.
    /// </summary>
    public string ConnectionStringFor(string database) =>
        new Npgsql.NpgsqlConnectionStringBuilder(_postgres.GetConnectionString()) { Database = database }
            .ConnectionString;

    /// <summary>
    /// A second copy of the real application, on <paramref name="connectionString"/> and this run's
    /// media root. Booted the same way as <see cref="InitializeAsync"/> boots the shared one, so the
    /// initialiser it runs is the one <c>aspire run</c> runs. The caller disposes it.
    /// </summary>
    public WebApplicationFactory<Program> Boot(string connectionString) =>
        new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseSetting("ConnectionStrings:catalog", connectionString);
            builder.UseSetting("Media:Root", _mediaRoot);

            if (BaseLanguage is not null)
                builder.UseSetting("MultiLingual:BaseLanguage", BaseLanguage);
        });

    /// <summary>
    /// The running instance of a hosted service, for a test that drives one step of it rather than
    /// waiting for its timer — <c>AuditRetentionService.PruneIfDueAsync</c>, which is what the
    /// timer calls.
    /// </summary>
    public T HostedService<T>() where T : IHostedService =>
        _factory!.Services.GetServices<IHostedService>().OfType<T>().Single();

    /// <summary>
    /// Puts the auditing settings group back to its defaults — pruning off — so a test that turned
    /// it on cannot leave the shared database pruning under the tests after it.
    /// </summary>
    public Task ResetAuditingSettingsAsync() =>
        Client.PostAsJsonChecked("/api/settings/auditing", new
        {
            pruneEnabled = false,
            retentionDays = 365,
            pruneIntervalHours = 24
        });

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
