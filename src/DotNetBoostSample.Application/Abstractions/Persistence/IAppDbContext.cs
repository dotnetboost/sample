using DotNetBoostSample.Domain.Catalog;
using Microsoft.EntityFrameworkCore;

namespace DotNetBoostSample.Application.Abstractions.Persistence;

/// <summary>
/// The application's own two tables, as a handler sees them.
///
/// <para><b>Why handlers get a <c>DbSet</c> and not a repository.</b> Because of what the
/// libraries do to it. <c>db.Products.ToListAsync()</c> comes back with <c>Name</c> and
/// <c>Description</c> already filled in the request's language — MultiLingual's EF Core
/// interceptors did that, and they act on the query, not on a type. Routing reads through a
/// repository interface would hide the single most interesting thing about this sample behind a
/// method called <c>GetAllAsync</c>.</para>
///
/// <para><b>And the honest cost of it.</b> This is not a real seam. It hands out
/// <c>IQueryable</c>, so a handler can write any query EF can translate, and swapping the
/// implementation for something that is not EF Core is not on the table. What it buys is narrower
/// and still worth having: Application never names the Npgsql provider, never reaches
/// <c>Database.EnsureCreated()</c>, and cannot quietly grow a dependency on one of the three
/// libraries' EF packages — the dependency rule that was doing real work. If that trade ever
/// stops being worth it, delete this interface, let handlers take <c>AppDbContext</c>, and fold
/// Infrastructure into Application: a modular monolith rather than a layered one, three projects
/// instead of four.</para>
/// </summary>
public interface IAppDbContext
{
    DbSet<Category> Categories { get; }

    DbSet<Product> Products { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
