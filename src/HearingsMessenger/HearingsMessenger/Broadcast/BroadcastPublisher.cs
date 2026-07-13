//===============================================================================
// TinyMessenger.Broadcast — one-way broadcast layer for enterprise workstations.
// See MODERNIZATION.md and licence.txt.
//===============================================================================

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace HearingsMessenger.Broadcast;

/// <summary>
/// Default <see cref="IBroadcastPublisher"/>: fans out each notification to every
/// registered <see cref="IBroadcastTransport"/> concurrently, optionally echoing
/// the notification on the local <see cref="ITinyMessengerHub"/> first.
///
/// One-way contract: transport/delivery failures are logged and swallowed — a
/// failing transport never affects the others and never surfaces to the caller.
/// Cancellation, by contrast, is honoured and propagated.
/// </summary>
public sealed class BroadcastPublisher : IBroadcastPublisher
{
    private readonly IReadOnlyList<IBroadcastTransport> _transports;
    private readonly ITinyMessengerHub? _localHub;
    private readonly BroadcastOptions _options;
    private readonly ILogger<BroadcastPublisher> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="BroadcastPublisher"/> class.
    /// </summary>
    /// <param name="transports">The transports to fan out to.</param>
    /// <param name="options">Broadcast options; defaults are used when null.</param>
    /// <param name="localHub">Optional in-process hub for local echo.</param>
    /// <param name="logger">Optional logger; a no-op logger is used when null.</param>
    public BroadcastPublisher(
        IEnumerable<IBroadcastTransport> transports,
        IOptions<BroadcastOptions>? options = null,
        ITinyMessengerHub? localHub = null,
        ILogger<BroadcastPublisher>? logger = null)
    {
        ArgumentNullException.ThrowIfNull(transports);

        _transports = transports.ToList();
        _options = options?.Value ?? new BroadcastOptions();
        _localHub = localHub;
        _logger = logger ?? NullLogger<BroadcastPublisher>.Instance;
    }

    /// <inheritdoc />
    public async Task PublishAsync(BroadcastNotification notification, MachineGroup group, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(notification);
        ArgumentNullException.ThrowIfNull(group);

        cancellationToken.ThrowIfCancellationRequested();

        EchoOnLocalHub(notification, group);

        if (_transports.Count == 0)
        {
            _logger.LogWarning("Broadcast {NotificationId} to group '{Group}' has no transports registered; nothing was sent.", notification.Id, group.Name);
            return;
        }

        var sends = _transports.Select(t => SendViaTransportAsync(t, notification, group, cancellationToken));
        await Task.WhenAll(sends).ConfigureAwait(false);
    }

    private void EchoOnLocalHub(BroadcastNotification notification, MachineGroup group)
    {
        if (_localHub is null || !_options.EchoOnLocalHub)
            return;

        try
        {
            _localHub.Publish(new BroadcastNotificationMessage(this, notification, group));
        }
        catch (Exception exception)
        {
            // Local echo is a courtesy, not a delivery guarantee.
            _logger.LogWarning(exception, "Local hub echo failed for broadcast {NotificationId}.", notification.Id);
        }
    }

    private async Task SendViaTransportAsync(IBroadcastTransport transport, BroadcastNotification notification, MachineGroup group, CancellationToken cancellationToken)
    {
        try
        {
            await transport.SendAsync(notification, group, cancellationToken).ConfigureAwait(false);

            _logger.LogInformation(
                "Broadcast {NotificationId} ('{Title}') sent via {Transport} to group '{Group}' ({MachineCount} machines).",
                notification.Id, notification.Title, transport.Name, group.Name, group.Machines.Count);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            // Fire-and-forget contract: isolate transport failures.
            _logger.LogError(
                exception,
                "Broadcast {NotificationId} failed in transport {Transport} for group '{Group}'.",
                notification.Id, transport.Name, group.Name);
        }
    }
}
