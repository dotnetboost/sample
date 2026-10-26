using DotNetBoostSample.Application.Abstractions.Endpoints;
using DotNetBoostSample.Application.Abstractions.Cqrs;
using DotNetBoostSample.Application.Catalog.Shared;
using DotNetBoostSample.Application.Composition;
using FluentValidation;
using Microsoft.Extensions.DependencyInjection;

namespace DotNetBoostSample.Application;

/// <summary>
/// Everything this project needs in the container: the dispatcher, the slices' handlers, the
/// behaviors, the request validators, and the one composition service that is injected rather
/// than registered by a library.
/// </summary>
public static class DependencyInjection
{
    /// <summary>
    /// Registers the application layer. One call in <c>Program.cs</c>, and adding a slice needs no
    /// edit here.
    /// </summary>
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        var assembly = EndpointRegistration.ApplicationAssembly;

        services.AddScoped<IDispatcher, Dispatcher>();

        // ── The handlers ─────────────────────────────────────────────────────
        // Registered against the closed interface each one implements, which is also the service
        // the dispatcher asks for. A slice with no handler fails at its first request rather than
        // at startup — the price of scanning instead of listing, and the reason the integration
        // tests drive every route.
        foreach (var type in Concrete(assembly))
        {
            foreach (var handler in Closing(type, typeof(IRequestHandler<,>)))
                services.AddScoped(handler, type);
        }

        // ── The pipeline ─────────────────────────────────────────────────────
        // Open generic: one registration covers every request/response pair. Microsoft's container
        // checks the implementation's generic constraints when it closes this, so a behavior
        // constrained to ICommand<> is simply not resolved for a query.
        services.AddScoped(typeof(IPipelineBehavior<,>), typeof(ValidationBehavior<,>));

        // ── The request validators ───────────────────────────────────────────
        // Only the ones validating a REQUEST. The settings groups' validators —
        // BrandingSettingsValidator, CatalogSettingsValidator — are AbstractValidator<T> too, and
        // DotNetBoost.Settings.FluentValidation registers those itself from the same assembly, as
        // well as publishing their rules through GET /api/settings/{group}/schema. Registering
        // them a second time here would put two of each in the container and say nothing about
        // which one the schema came from.
        foreach (var type in Concrete(assembly))
        {
            foreach (var validator in Closing(type, typeof(IValidator<>))
                .Where(v => IsRequest(v.GetGenericArguments()[0])))
            {
                services.AddScoped(validator, type);
            }
        }

        // ── The composition ──────────────────────────────────────────────────
        // Media × MultiLingual, injected into the handlers that read a file's alt text and into
        // BrandingProjector. MediaTextCleanup is NOT registered here: it is handed to the media
        // library with .OnFilesDeleted<MediaTextCleanup>(), which is what makes the cleanup run
        // for every delete including the library's own endpoint's.
        services.AddScoped<MediaText>();

        // And its counterpart for the application's own entities: one entity's text in every
        // language, read and written. Scoped for the same reason — it holds the request's engine —
        // and registered rather than static because the six slices that use it would otherwise
        // each take an IMultiLingualManager to hand straight on.
        services.AddScoped<TranslatedText>();

        return services;
    }

    private static IEnumerable<Type> Concrete(System.Reflection.Assembly assembly) =>
        assembly.GetTypes().Where(type => type is
        {
            IsAbstract: false, IsInterface: false, IsGenericTypeDefinition: false
        });

    private static IEnumerable<Type> Closing(Type type, Type openGeneric) =>
        type.GetInterfaces().Where(i => i.IsGenericType && i.GetGenericTypeDefinition() == openGeneric);

    private static bool IsRequest(Type type) =>
        type.GetInterfaces().Any(i => i.IsGenericType
                                   && i.GetGenericTypeDefinition() == typeof(IRequest<>));
}
