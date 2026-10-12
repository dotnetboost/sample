# Vertical slices with CQRS

**Built, and on `main`.** This document was the design; the code is the implementation, and where
the two disagreed the code won — §10 lists what changed on contact with the compiler. It was
written while the work sat on `feat/vertical-slices-cqrs` and still argues with itself about
whether to merge it; §11.3 says how that ended.

The solution builds clean on `net8.0` and `net10.0` with `TreatWarningsAsErrors`, and all 57 tests
pass, including the integration suite that boots the real `Program.cs` against a throwaway
PostgreSQL and drives every route over HTTP. No endpoint's URL, status code or JSON shape changed.

---

## 0. The short answers

**Does MediatR support .NET 8 and .NET 10?** Technically yes, and the licence is the part that
matters more.

| | Version | Target frameworks | Licence |
|---|---|---|---|
| Latest | **MediatR 14.2.0** (2026-07-02) | `netstandard2.0`, `net8.0`, `net462` — so it loads on `net8.0`, `net9.0` and `net10.0` | **Dual: RPL-1.5 or commercial**, with a licence key passed at registration |
| Last free | **MediatR 12.5.0** | `netstandard2.0`, `net6.0` — runs on `net10.0`, unmaintained | Apache-2.0 |

MediatR changed licence at **v13.0** (2025-07-02). So the choice is a commercial/RPL-1.5 dependency
on a current version, or pinning a 12.x that stopped getting fixes. There is no current
Apache/MIT MediatR.

**My recommendation for this repository: don't take the dependency. Write the dispatcher.**

Three reasons specific to this repo, not general taste:

1. **The licence story is the sample's pitch.** `Directory.Packages.props` leads with *"no
   `DotNetBoost.Core`, no `DotNetBoost.Abstractions`, no integration package"*, every DotNetBoost
   package is MIT, and `ZeroCouplingTests` exists to keep that true as a build failure rather than
   a README sentence. Acquiring an RPL-1.5-or-pay dependency to dispatch eleven handlers argues
   against the thing the repo is arguing for.
2. **The surface you'd use is tiny.** This application has 11 of its own routes. You need
   `SendAsync`, one handler interface, and a pipeline hook for validation. That is the ~70 lines in
   §4 — which also multi-targets `net8.0;net10.0` by construction, matching what
   `DotNetBoost.*.Core` already does.
3. **It stays swappable.** Keep MediatR's shape — `IRequest<TResponse>`, `IRequestHandler<,>`,
   `IPipelineBehavior<,>` — and moving to MediatR, or to [martinothamar/Mediator] (source-generated,
   no reflection), is a change of `using` directives and a registration call. Nothing in a slice
   knows which one it is. Verify any alternative's current licence yourself; this corner of the
   ecosystem has been moving.

**And a caveat on multi-targeting.** The *libraries* below can target `net8.0;net10.0`. The **host
cannot today**: `Microsoft.EntityFrameworkCore` / `Npgsql.EntityFrameworkCore.PostgreSQL` 10.0.3 and
Aspire 13.5.3 all require `net10.0`. §7 has the conditional-version pattern if you want the host on
both too; the value is mostly in the class libraries, where multi-targeting proves the slices
compile against DotNetBoost's own `net8.0` TFM.

---

## 1. One concern, stated once

This repo was **1,788 lines of C# in 13 files** when this was written, and the README's thesis is
that the integration between three independent libraries is *five files, none of them large*.
Clean architecture plus vertical slices turns one project into four and moves the composition
behind a dispatcher. The behaviour does not change. For a monolith heading to a hundred endpoints
that is the right trade; for a sample whose job is to make a composition claim visible in one
sitting, indirection is a cost charged against the only thing it sells.

Two things make it come out better than I expected, though, and they are why the rest of this
document is a design and not a warning:

- **Four of the five composition files are abstraction-only and move untouched.** `MediaText`,
  `MediaTextCleanup`, `MultiLingualSchemaContributor` and `BrandingProjector` depend on
  `DotNetBoost.*.Core` and nothing else — no EF, no ASP.NET. They land in `Application/Composition/`
  and `Application/Settings/` as-is. Only `AppDbContext` is infrastructure. A folder literally named
  `Composition/` makes the five-file claim *easier* to see than today's split between `Integration/`
  and `Settings/`.
- **One file per slice, not five.** Writing each feature as one file of nested types
  (`ListProducts.Query`, `.Handler`, `.Validator`, `.Endpoint`) keeps 11 slices at 11 files, against
  today's 2 endpoint files holding 652 lines. That is the version below. The one-type-per-file
  variant is the same design at ~45 files, and I would not pay it here.

If you want both: keep this repo as the minimal composition sample and publish the slice
architecture as a second repo or a `dotnet new` template. The design is identical either way.

---

## 2. Projects

| Project | TFMs | References | Holds | Must not reference |
|---|---|---|---|---|
| `DotNetBoostSample.Domain` | `net8.0;net10.0` | `DotNetBoost.MultiLingual.Core` | `Category`, `Product` | anything else |
| `DotNetBoostSample.Application` | `net8.0;net10.0` | Domain, the three `*.Core` packages, `Microsoft.EntityFrameworkCore`, `FluentValidation`, framework-ref `Microsoft.AspNetCore.App` | the slices, the dispatcher, the composition files, the settings groups | Npgsql, the `*.EntityFrameworkCore` packages, the `*.API` packages |
| `DotNetBoostSample.Infrastructure` | `net8.0;net10.0` | Application, `*.EntityFrameworkCore` ×3, `Media.FileSystem`, Npgsql | `AppDbContext`, the EF model configuration, the seeder, `AddInfrastructure()` | ASP.NET endpoint mapping |
| `DotNetBoostSample.Api` | `net10.0` | Application, Infrastructure, the three `*.API` packages, Scalar, ServiceDefaults | `Program.cs`, `appsettings`, `Resources/*.json` | — |

Three judgement calls in that table worth naming, because each one is a place where "clean
architecture" and "vertical slices" pull in opposite directions:

**Domain references `DotNetBoost.MultiLingual.Core`.** `[MultiLingual]` and `[NotMultiLingual]` sit on
`Category` and `Product`, so the entities carry an attribute from a library. Purist reading: that is
a persistence concern on a domain type. Practical reading: it is one MIT, abstraction-only package,
exactly as a domain entity carrying EF's `[Required]` is normally accepted. Keep the attributes. If
the library has a fluent model-configuration path, prefer that from Infrastructure and leave Domain
with zero package references — worth checking, because it is strictly better if it exists.

**The endpoint lives in the slice**, which means `Application` takes a framework reference to
`Microsoft.AspNetCore.App`. The purist alternative keeps Application HTTP-free and puts endpoints in
`Api/Endpoints/`, at the price of every feature being two folders in two projects. A slice you can
delete with one `rm` is the entire payoff of this architecture; splitting it in half to protect
Application from `IResult` gives up the payoff to keep the dependency diagram tidy. Handlers still
return `Result<T>` and never `IResult`, so they stay testable without a host — which is the part of
the rule that was doing real work.

**`Application` references `Microsoft.EntityFrameworkCore`** and declares `IAppDbContext`;
Infrastructure owns the Npgsql provider. This keeps `db.Products.ToListAsync()` in handlers, which
matters here specifically: MultiLingual's interceptors are what make that come back already
translated, so routing reads through repositories would hide the sample's best trick. The honest
cost is that `IAppDbContext` leaks `DbSet<T>` and is not really a swappable seam. The alternative is
to drop it, let handlers take `AppDbContext`, and fold Infrastructure into Application — a modular
monolith rather than a layered one, three projects instead of four. Defensible; I'd keep the four
since you asked for clean architecture.

---

## 3. Layout

```
src/DotNetBoostSample.Domain/
  Catalog/Category.cs                      unchanged
  Catalog/Product.cs                       unchanged

src/DotNetBoostSample.Application/
  Abstractions/
    Cqrs/Dispatcher.cs                     IRequest, IQuery, ICommand, IRequestHandler,
                                           IPipelineBehavior, IDispatcher, Dispatcher —
                                           ~70 lines, and why it is not MediatR  ← §4
    Cqrs/ValidationBehavior.cs             FluentValidation on every request, once
    Cqrs/ValidationExceptionHandler.cs     and the 400 that follows — ASP.NET does not
                                           map a ValidationException by itself
    Persistence/IAppDbContext.cs           DbSet<Category>, DbSet<Product>, SaveChangesAsync
    Persistence/IDatabaseErrorTranslator.cs  what a refused constraint means
    Persistence/DbUpdateExceptionHandler.cs  and the 409 that follows from it
    Endpoints/IEndpoint.cs                 Map(IEndpointRouteBuilder)
    Endpoints/EndpointRegistration.cs      MapApplicationEndpoints() — scans and calls each
    Results/Result.cs                      Result<T>, Error, ErrorKind
    Results/ResultExtensions.cs            Result<T> → IResult, localized RFC 7807, once
    DependencyInjection.cs                 AddApplication() — handlers, behaviors, validators
  Catalog/
    Categories/ListCategories.cs           query + handler + endpoint
    Categories/GetCategory.cs
    Categories/CreateCategory.cs           command + validator + handler + endpoint
    Categories/UpdateCategory.cs
    Categories/DeleteCategory.cs
    Products/ListProducts.cs               ← the worked example, §5
    Products/GetProduct.cs
    Products/CreateProduct.cs
    Products/UpdateProduct.cs
    Products/UploadProductImage.cs
    Products/DeleteProduct.cs
    Shared/AttachedFiles.cs                moved — shared by catalogue and branding, plus the
                                           owner-key formatter the two listings read
    Shared/CatalogRoles.cs                 MaxPageSize, "primary", "gallery", "banner"
    Shared/CatalogResponses.cs             what a create, an update and an upload answer with
    Shared/PagedResult.cs                  Items / Total / Skip / Take, named at last
    Shared/TranslatedText.cs               one entity's text in every language, read and
                                           written — and the only place that knows whether a
                                           base language is configured
  Composition/                             ← the join, in one folder, named for what it is
    MediaText.cs                           Media × MultiLingual        moved, unchanged
    MediaTextCleanup.cs                    Media → MultiLingual        moved, unchanged
    MultiLingualSchemaContributor.cs       Settings × MultiLingual     moved, unchanged
  Settings/
    BrandingSettings.cs                    group + validator + response + projector — all three
                                           libraries, moved unchanged
    CatalogSettings.cs                     group + validator           moved, unchanged
  Localization/ApiMessages.cs              moved, unchanged

src/DotNetBoostSample.Infrastructure/
  Persistence/AppDbContext.cs              the three Apply* calls — implements IAppDbContext
  Persistence/Configurations/CategoryConfiguration.cs   IEntityTypeConfiguration<Category>
  Persistence/Configurations/ProductConfiguration.cs
  Persistence/DatabaseInitialiser.cs       Seed.cs
  Persistence/MultiLingualModelCacheKeyFactory.cs  new — the model depends on
                                           MultiLingualOptions.BaseLanguage, so EF Core's
                                           model cache key has to as well
  DependencyInjection.cs                   AddInfrastructure(config) — DbContext + the three
                                           libraries' EF/FileSystem wiring, i.e. most of today's
                                           Program.cs

src/DotNetBoostSample.Api/
  Program.cs                               ~35 lines — §6
  Resources/{en,ar,fr}.json                stay with the host: AddJsonLocalization() resolves
                                           them from the content root (verify)
  appsettings*.json
```

Eleven slices, each one file. The three library endpoint sets (`/api/settings`, `/api/media`,
`/api/languages`, `/api/translations`) are untouched — they are still four `Map*Endpoints()` calls
in `Program.cs`, and this architecture does not reach them.

---

## 4. The dispatcher

The whole of "MediatR" for this application. **This block is compiled and run** — `net8.0` and
`net10.0`, warnings as errors, with a validation behavior and a slice in front of it.

```csharp
namespace DotNetBoostSample.Application.Abstractions.Cqrs;

/// <summary>A request with a response. The two markers below are what a behavior tells apart.</summary>
public interface IRequest<out TResponse>;

/// <summary>A read. Changes nothing, so a behavior may cache it or send it to a replica.</summary>
public interface IQuery<out TResponse> : IRequest<TResponse>;

/// <summary>A write.</summary>
public interface ICommand<out TResponse> : IRequest<TResponse>;

public interface IRequestHandler<in TRequest, TResponse>
    where TRequest : IRequest<TResponse>
{
    Task<TResponse> HandleAsync(TRequest request, CancellationToken ct);
}

public interface IPipelineBehavior<in TRequest, TResponse>
    where TRequest : IRequest<TResponse>
{
    Task<TResponse> HandleAsync(TRequest request, Func<Task<TResponse>> next, CancellationToken ct);
}

public interface IDispatcher
{
    Task<TResponse> SendAsync<TResponse>(IRequest<TResponse> request, CancellationToken ct = default);
}

/// <summary>
/// Resolves the handler for a request's runtime type and wraps it in whatever behaviors are
/// registered for that pair.
/// <para>
/// The call site knows the request only as <c>IRequest&lt;TResponse&gt;</c>, so the closed handler
/// type has to be built at runtime. It is built once per request type and cached, which is the
/// same trick MediatR plays — and the same cost: a <c>MakeGenericType</c> that trimming and
/// native AOT cannot see through. If this application ever goes AOT, that is the line to replace,
/// and a source-generated dispatcher is the replacement.
/// </para>
/// </summary>
public sealed class Dispatcher(IServiceProvider services) : IDispatcher
{
    private static readonly ConcurrentDictionary<Type, object> Wrappers = new();

    public Task<TResponse> SendAsync<TResponse>(IRequest<TResponse> request, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var wrapper = (Wrapper<TResponse>)Wrappers.GetOrAdd(
            request.GetType(),
            static type => Activator.CreateInstance(
                typeof(Wrapper<,>).MakeGenericType(type, typeof(TResponse)))!);

        return wrapper.SendAsync(request, services, ct);
    }

    private abstract class Wrapper<TResponse>
    {
        public abstract Task<TResponse> SendAsync(
            IRequest<TResponse> request, IServiceProvider services, CancellationToken ct);
    }

    private sealed class Wrapper<TRequest, TResponse> : Wrapper<TResponse>
        where TRequest : IRequest<TResponse>
    {
        public override Task<TResponse> SendAsync(
            IRequest<TResponse> request, IServiceProvider services, CancellationToken ct)
        {
            var typed   = (TRequest)request;
            var handler = services.GetRequiredService<IRequestHandler<TRequest, TResponse>>();

            Func<Task<TResponse>> next = () => handler.HandleAsync(typed, ct);

            // Reversed, so the first-registered behavior is the outermost one.
            foreach (var behavior in services
                .GetServices<IPipelineBehavior<TRequest, TResponse>>()
                .Reverse())
            {
                var inner = next;
                next = () => behavior.HandleAsync(typed, inner, ct);
            }

            return next();
        }
    }
}
```

Registration, which replaces MediatR's `AddMediatR`:

```csharp
public static IServiceCollection AddApplication(this IServiceCollection services)
{
    var assembly = typeof(AddApplicationMarker).Assembly;

    services.AddScoped<IDispatcher, Dispatcher>();

    // Every IRequestHandler<,> in this assembly, by the interface it closes.
    foreach (var type in assembly.GetTypes().Where(t => t is { IsAbstract: false, IsInterface: false }))
    {
        foreach (var handler in type.GetInterfaces().Where(i =>
            i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IRequestHandler<,>)))
        {
            services.AddScoped(handler, type);
        }
    }

    // Open generic: one registration covers every request/response pair.
    services.AddScoped(typeof(IPipelineBehavior<,>), typeof(ValidationBehavior<,>));

    // The slices' validators. The same assembly the settings library already scans for
    // AbstractValidator<T> in Program.cs — these are the request validators, which it does not.
    services.AddValidatorsFromAssembly(assembly, includeInternalTypes: true);

    return services;
}
```

And the one behavior that earns its place on day one:

```csharp
public sealed class ValidationBehavior<TRequest, TResponse>(
    IEnumerable<IValidator<TRequest>> validators) : IPipelineBehavior<TRequest, TResponse>
    where TRequest : IRequest<TResponse>
{
    public async Task<TResponse> HandleAsync(
        TRequest request, Func<Task<TResponse>> next, CancellationToken ct)
    {
        foreach (var validator in validators)
        {
            var result = await validator.ValidateAsync(request, ct);

            // ASP.NET's own problem-details handler turns this into a 400 with the failures,
            // which is why there is no Result<T> plumbing for it here.
            if (!result.IsValid) throw new ValidationException(result.Errors);
        }

        return await next();
    }
}
```

**What else a pipeline is for, once it exists.** Nothing below is needed today, and each is one
class when it is: a `LoggingBehavior` tagging every log line with
`IMultiLingualManager.CurrentLanguage`; a `TransactionBehavior<TRequest, TResponse> where TRequest :
ICommand<TResponse>` wrapping writes in one `SaveChanges` — the reason `ICommand` and `IQuery` are
separate markers; a caching behavior on `IQuery` that would have to key by language, which is
exactly the mistake `Program.cs` documents having removed from the settings cache.

---

## 5. One worked slice

`Application/Catalog/Products/ListProducts.cs` — today's longest handler, moved. The body is the
same code; what changes is where it lives and what it returns.

```csharp
namespace DotNetBoostSample.Application.Catalog.Products;

/// <summary>
/// One page of products, translated into the request's language, with each image's per-language
/// alt text. The whole feature: what is asked, what comes back, what bounds the ask, who answers
/// it, and the route. Delete the file, delete the feature.
/// </summary>
public static class ListProducts
{
    public sealed record Query(int? CategoryId, int? Page, int? PageSize)
        : IQuery<PagedResult<ProductListItem>>;

    /// <summary>
    /// Named, where today's listing builds an anonymous object — so OpenAPI can describe it, for
    /// the reason AttachedFiles.cs gives about its own two records.
    /// </summary>
    public sealed record ProductListItem(
        int Id,
        string Name,
        string Description,
        string Sku,
        decimal Price,
        int CategoryId,
        AttachedFile? Primary,
        IReadOnlyList<AttachedFile> Images);

    // No Validator. This slice is where the design was wrong and the test suite said so: bounding
    // Page and PageSize here turns a clamp into a 400, and a page size in this API is a caller's
    // SUGGESTION — `CatalogSettingsTests.A_caller_can_override_the_page_size_but_not_past_the_cap`
    // asserts that asking for 99,999 rows is answered with 200. The clamp stayed in the handler.
    // See §10.

    public sealed class Handler(
        IAppDbContext db,
        IMediaManager media,
        ISettingManager settings,
        MediaText mediaText) : IRequestHandler<Query, PagedResult<ProductListItem>>
    {
        public async Task<PagedResult<ProductListItem>> HandleAsync(Query request, CancellationToken ct)
        {
            // Read per request, not captured at startup — the whole promise of the settings
            // library, unchanged by any of this.
            var catalog = await settings.For<CatalogSettings>().GetAsync(cancellationToken: ct);

            var take = request.PageSize ?? catalog.PageSize;
            var skip = ((request.Page ?? 1) - 1) * take;

            var query = db.Products.AsQueryable();

            if (request.CategoryId is not null)
                query = query.Where(p => p.CategoryId == request.CategoryId);

            var total = await query.CountAsync(ct);

            // Still translated by MultiLingual's interceptors, because this is still a DbSet on
            // the real context. Four queries for the page, whatever it holds.
            var rows = await query.OrderBy(p => p.Id).Skip(skip).Take(take).ToListAsync(ct);

            var attachedByProduct = await media.ForMany(rows).ListWithRolesAsync(ct);

            var text = await mediaText.GetManyAsync(
                [.. attachedByProduct.Values.SelectMany(files => files.Select(f => f.File.Id))], ct);

            var items = rows.Select(product => ToListItem(product, attachedByProduct, text)).ToList();

            return new PagedResult<ProductListItem>(items, total, skip, take);
        }

        // ... the same grouping and role selection as today's endpoint, unchanged ...
    }

    /// <summary>The route. Parses, dispatches, maps — and holds no logic, which is the test of it.</summary>
    public sealed class Endpoint : IEndpoint
    {
        public void Map(IEndpointRouteBuilder app) =>
            app.MapGet("/api/products", async (
                    int? categoryId,
                    int? page,
                    int? pageSize,
                    IDispatcher dispatcher,
                    CancellationToken ct) =>
                await dispatcher.SendAsync(new Query(categoryId, page, pageSize), ct))
               .WithTags("Products")
               .WithSummary("One page of products, translated, with per-language image alt text");
    }
}
```

And the write side, where the shape pays off most, because `Result<T>` takes the localizer out of
the handler entirely:

```csharp
public static class DeleteCategory
{
    // The response type is Result<string>, not string: a request declares the type its handler
    // returns, and this one can refuse.
    public sealed record Command(int Id) : ICommand<Result<string>>;

    public sealed class Handler(IAppDbContext db, IMediaManager media)
        : IRequestHandler<Command, Result<string>>
    {
        public async Task<Result<string>> HandleAsync(Command request, CancellationToken ct)
        {
            var category = await db.Categories.FirstOrDefaultAsync(c => c.Id == request.Id, ct);

            // A message KEY and its argument, not a sentence and not an IResult. The handler says
            // what happened; the endpoint decides the status code and the localizer picks the
            // language — in one place, instead of in the seven handlers that call
            // NotFound(messages, …) today.
            if (category is null)
                return Error.NotFound("Catalog.CategoryNotFound", request.Id);

            if (await db.Products.AnyAsync(p => p.CategoryId == request.Id, ct))
                return Error.Conflict("Catalog.CategoryHasProducts");

            await media.For(category).DetachAllAsync(ct);

            var slug = category.Slug;

            db.Categories.Remove(category);
            await db.SaveChangesAsync(ct);

            return slug;   // implicit conversion to Result<string>
        }
    }

    public sealed class Endpoint : IEndpoint
    {
        public void Map(IEndpointRouteBuilder app) =>
            app.MapDelete("/api/categories/{id:int}", async (
                    int id,
                    IDispatcher dispatcher,
                    IStringLocalizer<ApiMessages> messages,
                    CancellationToken ct) =>
                (await dispatcher.SendAsync(new Command(id), ct))
                    .ToHttpResult(messages, slug => Results.Ok(
                        new { Message = messages["Catalog.CategoryDeleted", slug].Value })))
               .WithTags("Categories")
               .WithSummary("Deletes a category that has no products");
    }
}
```

The mapping, written once, replacing `CatalogEndpoints.NotFound` and `.Wrote`:

```csharp
public static IResult ToHttpResult<T>(
    this Result<T> result,
    IStringLocalizer<ApiMessages> messages,
    Func<T, IResult>? onSuccess = null) =>
    result.IsSuccess
        ? onSuccess?.Invoke(result.Value!) ?? Results.Ok(result.Value)
        : Results.Problem(
            detail: messages[result.Error!.MessageKey, result.Error.Arguments],
            statusCode: result.Error.Kind switch
            {
                ErrorKind.NotFound => StatusCodes.Status404NotFound,
                ErrorKind.Conflict => StatusCodes.Status409Conflict,
                _                  => StatusCodes.Status400BadRequest
            });
```

RFC 7807 and the localized sentence survive exactly as documented in `CatalogEndpoints`, and the
rule now lives in one method instead of two helpers plus eleven call sites.

---

## 6. `Program.cs` after

The long comment block at the top of today's `Program.cs` stays — it is the best documentation in
the repo and it describes the composition, which has not moved. What leaves is the wiring.

```csharp
var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();

// The DbContext and all three libraries' registrations — today's four ── blocks, moved whole.
builder.Services.AddInfrastructure(builder.Configuration, builder.Environment);

// The dispatcher, the handlers, the behaviors, the request validators.
builder.Services.AddApplication();

builder.Services.AddProblemDetails();
builder.Services.AddOpenApi();
builder.Services.AddCors(options => options.AddDefaultPolicy(policy =>
    policy.AllowAnyOrigin().AllowAnyHeader().AllowAnyMethod()));

var app = builder.Build();

app.MapDefaultEndpoints();
app.UseExceptionHandler();
app.UseCors();

await app.Services.InitialiseDatabaseAsync();

app.UseMultiLingual();

app.MapOpenApi();
app.MapScalarApiReference();

// Each library maps its own. Unchanged, and untouched by any of this.
app.MapSettingsEndpoints();
app.MapMediaEndpoints();
app.MapLanguageEndpoints();
app.MapTranslationEndpoints();

// Every IEndpoint in the Application assembly.
app.MapApplicationEndpoints();

app.Run();

public partial class Program;
```

One thing to watch: `MapApplicationEndpoints()` makes route registration order depend on a
reflection scan. It does not matter for these eleven literal routes, and it would matter the day
two slices claim overlapping patterns. If you would rather not have the question, drop `IEndpoint`
and give each feature folder a `CategoryEndpoints.Map(app)` that calls its slices in order — same
slices, explicit order, one more file per folder.

---

## 7. Multi-targeting, concretely

`Directory.Build.props` stops naming a framework **at all**, and each project states its own.

That is not the tidiest-looking option, and it is the only one that works: `Directory.Build.props`
is imported before a csproj's body, so a condition there cannot see whether the project is about to
set `TargetFrameworks`, and a `TargetFramework` inherited from the props file silently wins over a
`TargetFrameworks` in the csproj — you get a single-TFM build and no warning. So the props file
keeps `Nullable`, `ImplicitUsings`, `TreatWarningsAsErrors` and `IsPackable`, the three class
libraries declare `net8.0;net10.0`, and the host, the AppHost, ServiceDefaults and the test project
each declare `net10.0`.

For `Application` and `Infrastructure` to build on `net8.0`, the EF packages need a version per
framework. `Directory.Packages.props` conditions are evaluated per TFM pass, so:

```xml
<ItemGroup Condition="'$(TargetFramework)' == 'net8.0'">
  <PackageVersion Include="Microsoft.EntityFrameworkCore" Version="8.0.30" />
  <PackageVersion Include="Npgsql.EntityFrameworkCore.PostgreSQL" Version="8.0.11" />
</ItemGroup>

<ItemGroup Condition="'$(TargetFramework)' != 'net8.0'">
  <PackageVersion Include="Microsoft.EntityFrameworkCore" Version="10.0.11" />
  <PackageVersion Include="Npgsql.EntityFrameworkCore.PostgreSQL" Version="10.0.3" />
</ItemGroup>
```

The EF versions are the ones the DotNetBoost EF packages themselves ask for on each TFM — 8.0.30
and 10.0.11, which is also what their `.nuspec` files declare. Naming anything lower is a package
**downgrade** against a transitive requirement, which is NU1605 and an error: the trap to know
about whenever you add a direct reference to something a library already depends on. The Npgsql
provider's 8.x line tops out at 8.0.11 and wants EF 8.0.11, which the 8.0.30 above *upgrades*, so
the two sit together without complaint.

Costs, so the decision is made with them in view: every build compiles twice, `TreatWarningsAsErrors`
now has two sets of analyzer warnings to satisfy, and anything in the `net10.0` BCL that `net8.0`
lacks needs an `#if NET10_0_OR_GREATER`. The dispatcher in §4 needs none — that is one of the
reasons it is worth writing rather than importing.

---

## 8. Where every file goes

| Today | Proposed | Change |
|---|---|---|
| `Program.cs` (221) | `Api/Program.cs` + `Infrastructure/DependencyInjection.cs` | split; the comment block stays with `Program.cs` |
| `Endpoints/CatalogEndpoints.cs` (533) | 11 slice files under `Application/Catalog/` | split by feature; handler bodies move as they are |
| `Endpoints/AttachedFiles.cs` (119) | `Application/Catalog/Shared/AttachedFiles.cs` | moved |
| `Entities/Category.cs`, `Entities/Product.cs` | `Domain/Catalog/` | moved |
| `Data/AppDbContext.cs` (76) | `Infrastructure/Persistence/AppDbContext.cs` | implements `IAppDbContext`; the two `modelBuilder.Entity<…>` blocks move to `IEntityTypeConfiguration<T>` files; the three `Apply*` calls stay exactly as they are |
| `Data/Seed.cs` (111) | `Infrastructure/Persistence/DatabaseInitialiser.cs` | moved |
| `Integration/MediaText.cs` (111) | `Application/Composition/MediaText.cs` | moved, **unchanged** |
| `Integration/MediaTextCleanup.cs` (32) | `Application/Composition/MediaTextCleanup.cs` | moved, **unchanged** |
| `Settings/MultiLingualSchemaContributor.cs` (46) | `Application/Composition/` | moved, **unchanged** |
| `Settings/BrandingSettings.cs` (372) | `Application/Settings/BrandingSettings.cs` | moved, **unchanged** — group, validator, response and projector stay one file |
| `Settings/CatalogSettings.cs` (44) | `Application/Settings/CatalogSettings.cs` | moved; its `MaxPageSize` reference points at `CatalogRoles` instead of `CatalogEndpoints` |
| `Localization/ApiMessages.cs` | `Application/Localization/ApiMessages.cs` | moved |
| `Resources/{en,ar,fr}.json` | stays in `Api/` | `AddJsonLocalization()` resolves them from the host's content root; confirmed by the integration tests, which assert localized messages in all three languages |
| — | `Application/Abstractions/**` | new: ~200 lines of dispatcher, result and endpoint plumbing |

Five of the thirteen files move with no edit at all, and four of those five are the composition.

**Tests.** `ZeroCouplingTests`, `KeyFormatAgreementTests` and `BrandingOwnerTests` all read
attributes and assembly references, so they need their assembly handles repointed from
`DotNetBoostSample.Api` to `Application`/`Domain` and nothing more. The integration tests boot
`Program` through `WebApplicationFactory<Program>` and are indifferent to all of this — they drive
HTTP, and no route changes. What becomes newly possible is the thing CQRS is actually for: a handler
test that constructs `ListProducts.Handler` with fakes and never starts a host, so the assertion
about four queries per page could be a unit test rather than a comment.

---

## 9. What it costs

| | |
|---|---|
| **Projects and files** | 1 → 4 projects, and 13 → 42 C# files in `src/`. The eleven slices account for eleven of the twenty-nine new ones; the rest is the dispatcher, the result type, the endpoint scan, four shared records, two EF configurations, and the refusal handling §10a added after this table was first written — the validation and `DbUpdate` handlers, the error-translator interface and its Postgres implementation. Nothing it does is new. |
| **Indirection** | A route no longer shows you what it does. `MapGet → IDispatcher → Wrapper<,> → behaviors → Handler` is four hops for a query whose body is unchanged, and the stack trace in a debugger gets a reflection frame in the middle. |
| **Reflection** | Handler scanning and `MakeGenericType` at first dispatch. Fine for a server; a blocker under native AOT, where the answer is a source-generated dispatcher. |
| **Reach** | Covers 11 of the application's routes. The three libraries' own endpoints are not slices and will never be — so the dashboard talks to two different architectures, which is honest but worth knowing. |
| **Multi-targeting** | Two compiles, two analyzer passes, a CI matrix, and conditional EF versions. Buys you proof that the slices build against DotNetBoost's `net8.0` TFM. |
| **The thesis** | The README's claim is "five files, none of them large." It is still true after this, and `Composition/` makes it more visible — but a reader now meets a dispatcher, a result type and four projects on the way to it. |

---

## 10. What changed on contact with the compiler

Five things, and the first two are the ones worth reading.

**The paging contract — the design was wrong.** §5 proposed a `ListProducts.Validator` bounding
`Page` and `PageSize`, on the reasoning that a request which cannot be honoured is better refused
than silently rewritten. That is a fine principle and it is not this API's contract: a page size
here is a caller's *suggestion*, deliberately matching what DotNetBoost.Media says of its own
`Take`, and `CatalogSettingsTests.A_caller_can_override_the_page_size_but_not_past_the_cap` asserts
that `?pageSize=99999` is answered with 200 rows rather than a 400. The validator made that test
fail, which is exactly what it is for. The clamp stayed in the handler, the slice has no validator,
and there is a comment where one would have gone saying why — a restructure must not smuggle a
contract change in under cover of moving files.

**`ExposeOverHttp` belongs to the host, and the compiler is what pointed it out.** The plan put the
whole MultiLingual registration in `AddInfrastructure`. It does not compile there:
`ExposeOverHttp` is declared in `DotNetBoost.MultiLingual.**API**`, the package the *host*
installs, and pulling that package into Infrastructure to keep the chain in one place would have
put routing in the persistence layer. So `AddInfrastructure` takes
`Action<MultiLingualBuilder>? exposeTranslations` and `Program.cs` passes its answer in. The split
it forced is the correct one, and better than what was designed: **which** translations are
reachable over HTTP is an API decision, **where** they are stored is infrastructure. Worth noting
that the package layout made the layering mistake unbuildable rather than merely regrettable.

**`AddInfrastructure` takes strings, not `IConfiguration`.** Reading configuration is the host's
job, so `Program.cs` resolves the connection string and the media root and passes both. That keeps
Infrastructure's package list to EF Core, the three libraries' stores and the Npgsql provider — no
`Microsoft.Extensions.Configuration`, no `Hosting` — and it means the "run the AppHost" message for
a missing connection string is raised where it can say something useful.

**A request declares the type its handler returns.** §5 wrote `ICommand<string>` with a handler
returning `Result<string>`. Those are different types and it does not compile: a refusable command
is `ICommand<Result<string>>`. The slices that cannot refuse — the two creates, the two listings —
return their value directly rather than wrapping everything to look uniform.

**One naming collision worth knowing about.** `Abstractions/Results/ResultExtensions.cs` sits in a
namespace ending in `Results`, so the bare name `Results.Ok(...)` binds to *that namespace* rather
than to ASP.NET's static helper, with an error that reads as a missing assembly reference. Fixed
with `using Http = Microsoft.AspNetCore.Http.Results;` and a comment, which is cheaper than
renaming the namespace away from the word the layer is actually about.

---

## 10a. And what running it found

Three 500s, none of which the 48 tests could see, because no test sent a request that provoked
one. They are worth listing together, because the fix is different each time and the reason is the
same each time: a constraint was doing its job and nothing had decided what its refusal should look
like over HTTP.

| Request | Was | Is | Fixed by |
|---|---|---|---|
| A body of the wrong shape — empty SKU, negative price | 500 | 400, fields named | `ValidationExceptionHandler`. ASP.NET does not map FluentValidation's `ValidationException`, and a comment here claimed it did. |
| `categoryId` naming no category | 500 | 404, **the id in the sentence** | A check in the handler. Only the handler knows which id was wrong; an exception handler sees the violation but not the request. |
| A slug or SKU already taken | 500 | 409, the field named | `PostgresErrorTranslator` + `DbUpdateExceptionHandler`. Uniqueness cannot be checked honestly — the query is stale by the time the insert runs — so the index decides and its refusal is translated. |

The second and third look like one problem and are not, and the pair is the clearest argument here
for why the layer split exists. A missing category can be *asked about*, so the question goes in
the handler, where the answer can name the id. A duplicate slug cannot — so the constraint answers,
and something has to read PostgreSQL's `SqlState` and constraint name to know what it said. That something is one class in the infrastructure layer, matching index names the EF
configurations declare as constants a few files away. The application layer still never names
Npgsql, and swapping provider replaces that one file.

`CatalogRefusalTests` covers all three — eight tests over requests the suite had never sent.

---

## 11. If it were mine

1. **Skip MediatR.** The dispatcher in §4 is seventy lines, MIT by being yours, `net8.0;net10.0` by
   construction, and shaped so MediatR or [martinothamar/Mediator] drops in later if you ever want
   the source generator.
2. **Four projects, endpoint in the slice, one file per feature.** That is the version above.
3. **Decide what `main` is for — and it has been decided.** This sample's job is to make a
   composition claim legible in one sitting, and `main` did that in 1,788 lines. A slice
   architecture is a better demonstration of *how to build an application with these libraries* and
   a worse demonstration of *how little the libraries ask of you*. Shipping the two apart — a
   second repo, or a `dotnet new` template — would have left `main` unchanged, and was the other
   way to go.

   It went this way: the slices are `main`. That does not retract the trade-off above, which is
   left standing rather than rewritten into agreement — it is why the choice was worth making
   deliberately instead of by default. What guards the claim does not depend on the decision
   either way: the README still counts the five files honestly, and `ZeroCouplingTests` still
   fails the build if one library ever reaches for another.
4. **The one piece worth back-porting here regardless** is `Result<T>` + `Error` + `ToHttpResult`.
   It removes `IStringLocalizer<ApiMessages>` from eleven handler signatures and puts the status-code
   decision in one method, and it needs no dispatcher, no new project and no new package.

[martinothamar/Mediator]: https://github.com/martinothamar/Mediator

---

### Sources

- [MediatR on NuGet](https://www.nuget.org/packages/MediatR/) — 14.2.0, target frameworks
- [MediatR 12.5.0](https://www.nuget.org/packages/MediatR/12.5.0) — last Apache-2.0 release
- [MediatR 13.0.0](https://www.nuget.org/packages/MediatR/13.0.0)
- [AutoMapper and MediatR Commercial Editions Launch Today](https://www.jimmybogard.com/automapper-and-mediatr-commercial-editions-launch-today/) — the RPL-1.5 / commercial dual licence
- [mediatr.io](https://mediatr.io/) — licence keys
- [DotNetBoost.Settings.Core 1.0.0-preview.2](https://www.nuget.org/packages/DotNetBoost.Settings.Core/1.0.0-preview.2) — MIT, `net8.0;net10.0`
