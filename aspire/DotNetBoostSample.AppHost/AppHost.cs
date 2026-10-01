// ─────────────────────────────────────────────────────────────────────────────
//  DotNetBoost sample — Aspire AppHost
//
//  Starts the whole stack with one command:
//
//    aspire run                  (or: dotnet run --project aspire/DotNetBoostSample.AppHost)
//
//    • postgres  — one database for the app's two tables and all six library tables
//    • api       — src/DotNetBoostSample.Api, storing media bytes on local disk under ./media
//    • dashboard — clients/dashboard (Nuxt 4), pointed at the API
//
//  Media bytes go to the local file system, which needs no resource here at all — that is
//  rather the point of the FileSystem backend.
// ─────────────────────────────────────────────────────────────────────────────

var builder = DistributedApplication.CreateBuilder(args);

// ── PostgreSQL ───────────────────────────────────────────────────────────────
// Named volume, so what you create in one session is still there in the next.
var postgres = builder.AddPostgres("postgres")
    .WithDataVolume("dotnetboost-sample-pgdata")
    .WithPgAdmin();

var database = postgres.AddDatabase("catalog");

// ── The API ──────────────────────────────────────────────────────────────────
var api = builder.AddProject<Projects.DotNetBoostSample_Api>("api")
    .WithReference(database)
    .WaitFor(database)
    .WithHttpHealthCheck("/health")
    .WithExternalHttpEndpoints();

// ── The dashboard ────────────────────────────────────────────────────────────
// npm install runs automatically before the dev server starts.
builder.AddJavaScriptApp("dashboard", "../../clients/dashboard", "dev")
    .WithNpm()
    // Nuxt binds whatever PORT says; without this it picks its own and Aspire has no
    // endpoint to publish, so the dashboard never gets a clickable URL.
    .WithHttpEndpoint(env: "PORT")
    .WithReference(api)
    .WaitFor(api)
    // Read by server/api/[...path].ts, which proxies the browser's calls so the dashboard
    // reaches every endpoint through one origin.
    .WithEnvironment("NUXT_API_URL", api.GetEndpoint("http"))
    .WithEnvironment("NUXT_PUBLIC_API_REFERENCE_URL",
        ReferenceExpression.Create($"{api.GetEndpoint("http")}/scalar"))
    // Must match WithMaxFileSize(...) in src/DotNetBoostSample.Api/Program.cs: the upload form
    // refuses a larger file before sending it, rather than after a 413 comes back.
    .WithEnvironment("NUXT_PUBLIC_MAX_UPLOAD_BYTES", "26214400")
    .WithExternalHttpEndpoints();

builder.Build().Run();
