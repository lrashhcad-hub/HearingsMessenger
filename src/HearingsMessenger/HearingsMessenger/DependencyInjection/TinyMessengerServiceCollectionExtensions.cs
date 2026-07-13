//===============================================================================
// TinyMessenger — see licence.txt.
//===============================================================================

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using HearingsMessenger.Broadcast;

namespace HearingsMessenger;

/// <summary>
/// Dependency injection registration for TinyMessenger and its broadcast layer.
/// Only Microsoft.Extensions.*.Abstractions APIs are used — no container or
/// logging framework is forced on consumers.
/// </summary>
public static class TinyMessengerServiceCollectionExtensions
{
    /// <summary>
    /// Registers <see cref="ITinyMessengerHub"/> as a singleton
    /// (<see cref="TinyMessengerHub"/> with the default subscriber error handler,
    /// unless an <see cref="ISubscriberErrorHandler"/> has been registered).
    /// </summary>
    public static IServiceCollection AddTinyMessenger(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.TryAddSingleton<ITinyMessengerHub>(sp =>
        {
            var errorHandler = sp.GetService<ISubscriberErrorHandler>();
            return errorHandler is null ? new TinyMessengerHub() : new TinyMessengerHub(errorHandler);
        });

        return services;
    }

    /// <summary>
    /// Registers the one-way broadcast layer: <see cref="IBroadcastPublisher"/>
    /// (as <see cref="BroadcastPublisher"/>) and the recommended
    /// <see cref="HttpAgentBroadcastTransport"/>. Also registers the local hub
    /// (via <see cref="AddTinyMessenger"/>) so outgoing broadcasts can be echoed
    /// in-process.
    ///
    /// Register additional transports by adding more <see cref="IBroadcastTransport"/>
    /// implementations to the container; the publisher fans out to all of them.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configure">Optional configuration for <see cref="BroadcastOptions"/>.</param>
    public static IServiceCollection AddTinyMessengerBroadcast(this IServiceCollection services, Action<BroadcastOptions>? configure = null)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddTinyMessenger();
        services.AddOptions();

        if (configure is not null)
        {
            services.Configure(configure);
        }

        services.TryAddEnumerable(ServiceDescriptor.Singleton<IBroadcastTransport, HttpAgentBroadcastTransport>());

        services.TryAddSingleton<IBroadcastPublisher>(sp => new BroadcastPublisher(
            sp.GetServices<IBroadcastTransport>(),
            sp.GetService<Microsoft.Extensions.Options.IOptions<BroadcastOptions>>(),
            sp.GetService<ITinyMessengerHub>(),
            sp.GetService<Microsoft.Extensions.Logging.ILogger<BroadcastPublisher>>()));

        return services;
    }
}
