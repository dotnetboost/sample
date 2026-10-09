using Microsoft.AspNetCore.Routing;
using System.Reflection;

namespace DotNetBoostSample.Application.Abstractions.Endpoints;

/// <summary>
/// Maps every <see cref="IEndpoint"/> in this assembly, so adding a slice adds its route with no
/// edit in <c>Program.cs</c>.
/// </summary>
public static class EndpointRegistration
{
    /// <summary>
    /// Finds and maps them.
    ///
    /// <para><b>What this gives up.</b> Route registration order now depends on a reflection scan
    /// rather than on the order of calls in a file. It does not matter for these eleven literal
    /// routes — none of them can shadow another — and it would matter the day two slices claim
    /// overlapping patterns, because the winner would be decided by whatever order
    /// <c>GetTypes()</c> happens to return. If that day comes, delete this and give each feature
    /// folder an explicit <c>Map(app)</c> that calls its slices in order: same slices, stated
    /// order, one more file per folder.</para>
    ///
    /// <para>Instantiated with <c>Activator</c> rather than resolved from the container: an
    /// endpoint declaration holds no state and takes no dependencies — everything a route needs is
    /// a parameter on the lambda, injected per request. One of these existing as a singleton for
    /// the lifetime of the process would be the mistake.</para>
    /// </summary>
    public static IEndpointRouteBuilder MapApplicationEndpoints(this IEndpointRouteBuilder app)
    {
        var endpoints = typeof(EndpointRegistration).Assembly
            .GetTypes()
            .Where(type => type is { IsAbstract: false, IsInterface: false, IsGenericTypeDefinition: false }
                        && typeof(IEndpoint).IsAssignableFrom(type));

        foreach (var type in endpoints)
            ((IEndpoint)Activator.CreateInstance(type)!).Map(app);

        return app;
    }

    /// <summary>The assembly the scans above and in <c>AddApplication()</c> both read.</summary>
    internal static Assembly ApplicationAssembly => typeof(EndpointRegistration).Assembly;
}
