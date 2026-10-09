using Microsoft.AspNetCore.Routing;

namespace DotNetBoostSample.Application.Abstractions.Endpoints;

/// <summary>
/// One slice's route, declared inside the slice.
///
/// <para>This is the interface that keeps a feature in one file. <c>ListProducts.cs</c> holds the
/// query, the response, the bounds, the handler <em>and</em> the URL it answers on — so the whole
/// feature is one thing to read and one thing to delete. The alternative is a parallel
/// <c>Endpoints/</c> tree in the host, which is a second folder per feature and a second place to
/// look.</para>
/// </summary>
public interface IEndpoint
{
    /// <summary>Maps this slice's route. Parse, dispatch, map the result — and no logic.</summary>
    void Map(IEndpointRouteBuilder app);
}
