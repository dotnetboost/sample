using Microsoft.Extensions.DependencyInjection;
using System.Collections.Concurrent;

namespace DotNetBoostSample.Application.Abstractions.Cqrs;

// ─────────────────────────────────────────────────────────────────────────────
//  The whole of "MediatR" for this application, in one file.
//
//  Why not MediatR itself. It would work: 14.2.0 targets netstandard2.0 and net8.0, so it loads
//  on net8.0 and net10.0 alike. But it changed licence at v13.0 — RPL-1.5 or commercial, with a
//  key passed at registration — and the last Apache-2.0 release, 12.5.0, is unmaintained. This
//  repository's pitch is that four libraries compose with no shared kernel and no integration
//  package, every one of them MIT, with a test that fails the build if that stops being true.
//  Taking an RPL-1.5-or-pay dependency to dispatch eleven handlers would argue against the thing
//  the sample is arguing for.
//
//  The shape below is MediatR's on purpose — IRequest<TResponse>, IRequestHandler<,>,
//  IPipelineBehavior<,> — so swapping to MediatR, or to a source-generated mediator, is a change
//  of using directives and one registration call. No slice knows which one it is.
//
//  ── On the names ────────────────────────────────────────────────────────────
//  This folder is Cqrs and not Messaging, which is the usual name for this file set in
//  clean-architecture templates. "Messaging" in .NET means a broker — MassTransit, Service Bus,
//  the RabbitMQ and Kafka integrations Aspire ships — and nothing here crosses a process
//  boundary. Worse than being ambiguous, it would be sitting on the name the real messaging layer
//  wants: the day this application publishes an event, its abstractions belong in
//  Abstractions/Messaging, and they should not have to share the folder with an in-process
//  dispatcher.
//
//  IRequest is kept anyway, and it is the one name here with a genuine problem: inside an ASP.NET
//  application it sits a few lines from actual HTTP requests. It stays because it is MediatR's
//  name, and keeping MediatR's vocabulary is exactly what makes the swap above a change of using
//  directives rather than a rewrite of eleven slices. A folder name costs nothing to change; a
//  public contract costs the escape hatch.
// ─────────────────────────────────────────────────────────────────────────────

/// <summary>A request with a response. The two markers below are what a behavior tells apart.</summary>
public interface IRequest<out TResponse>;

/// <summary>
/// A read. Changes nothing, so a behavior may legitimately cache it or send it to a replica.
/// </summary>
public interface IQuery<out TResponse> : IRequest<TResponse>;

/// <summary>
/// A write. Separate from <see cref="IQuery{TResponse}"/> so a behavior can constrain itself to
/// one or the other — <c>where TRequest : ICommand&lt;TResponse&gt;</c> is how a transaction
/// behavior wraps every write and no read.
/// </summary>
public interface ICommand<out TResponse> : IRequest<TResponse>;

/// <summary>Answers one request type. One per slice, and it is the only place its logic lives.</summary>
public interface IRequestHandler<in TRequest, TResponse>
    where TRequest : IRequest<TResponse>
{
    Task<TResponse> HandleAsync(TRequest request, CancellationToken ct);
}

/// <summary>
/// Wraps every handler whose request/response pair it closes over. Registered as an open generic,
/// so one registration covers every slice.
/// </summary>
public interface IPipelineBehavior<in TRequest, TResponse>
    where TRequest : IRequest<TResponse>
{
    Task<TResponse> HandleAsync(TRequest request, Func<Task<TResponse>> next, CancellationToken ct);
}

/// <summary>What an endpoint depends on. The handler is resolved from the request's own type.</summary>
public interface IDispatcher
{
    Task<TResponse> SendAsync<TResponse>(IRequest<TResponse> request, CancellationToken ct = default);
}

/// <summary>
/// Resolves the handler for a request's runtime type and wraps it in whatever behaviors are
/// registered for that pair.
///
/// <para><b>Why there is a wrapper type at all.</b> The call site knows the request only as
/// <c>IRequest&lt;TResponse&gt;</c> — that is the point of a dispatcher — so the closed
/// <c>IRequestHandler&lt;TRequest, TResponse&gt;</c> cannot be named at compile time. It is built
/// once per request type and cached in <see cref="Wrappers"/>, which is the same trick MediatR
/// plays and carries the same cost: a <c>MakeGenericType</c> that trimming and native AOT cannot
/// see through. If this application ever goes AOT, this is the one line to replace, and a
/// source-generated dispatcher is the replacement.</para>
///
/// <para>Caching by request type alone is safe because a request implements
/// <c>IRequest&lt;T&gt;</c> for exactly one <c>T</c>. A type closing it twice would be a compile
/// error at the call site, not a wrong cache hit.</para>
/// </summary>
public sealed class Dispatcher(IServiceProvider services) : IDispatcher
{
    private static readonly ConcurrentDictionary<Type, object> Wrappers = new();

    /// <inheritdoc/>
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

            // Reversed while building inwards, so the FIRST-registered behavior ends up outermost
            // — the order anyone reading AddApplication() would assume.
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
