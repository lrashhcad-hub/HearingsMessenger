//===============================================================================
// TinyMessenger.Broadcast — one-way broadcast layer for enterprise workstations.
// See MODERNIZATION.md and licence.txt.
//===============================================================================

namespace HearingsMessenger.Broadcast;

/// <summary>
/// Extension point for broadcast delivery. Implementations MUST be strictly
/// one-way, best-effort: per-machine delivery failures are logged, never thrown.
/// If you need acknowledgements or request/response semantics, build a separate
/// service — do not bend this contract.
/// </summary>
public interface IBroadcastTransport
{
    /// <summary>
    /// Human-readable transport name (used in logs).
    /// </summary>
    string Name { get; }

    /// <summary>
    /// Send the notification to every machine in the group. Must not throw for
    /// delivery failures; may throw <see cref="OperationCanceledException"/> if
    /// <paramref name="cancellationToken"/> is cancelled.
    /// </summary>
    Task SendAsync(BroadcastNotification notification, MachineGroup group, CancellationToken cancellationToken = default);
}

/// <summary>
/// Publishes one-way broadcast notifications to groups of machines by fanning
/// out to every registered <see cref="IBroadcastTransport"/>.
/// </summary>
public interface IBroadcastPublisher
{
    /// <summary>
    /// Publish the notification to the given machine group via all registered
    /// transports. Never throws for delivery failures; honours cancellation.
    /// </summary>
    Task PublishAsync(BroadcastNotification notification, MachineGroup group, CancellationToken cancellationToken = default);
}

/// <summary>
/// Options for the broadcast layer.
/// </summary>
public sealed class BroadcastOptions
{
    /// <summary>
    /// Default agent port used by <see cref="HttpAgentBroadcastTransport"/> when a
    /// <see cref="MachineTarget"/> has no per-machine override. Default: 7443.
    /// </summary>
    public int DefaultPort { get; set; } = 7443;

    /// <summary>
    /// Path of the agent's notification endpoint. Default: "/api/notifications".
    /// </summary>
    public string AgentPath { get; set; } = "/api/notifications";

    /// <summary>
    /// Per-machine send timeout. Default: 10 seconds.
    /// </summary>
    public TimeSpan SendTimeout { get; set; } = TimeSpan.FromSeconds(10);

    /// <summary>
    /// Maximum number of machines contacted concurrently per broadcast. Default: 16.
    /// </summary>
    public int MaxConcurrentSends { get; set; } = 16;

    /// <summary>
    /// When true (the default), <see cref="BroadcastPublisher"/> also publishes a
    /// <see cref="BroadcastNotificationMessage"/> on the local
    /// <see cref="ITinyMessengerHub"/> (if one is available) so in-process
    /// subscribers — auditing, UI, logging — see every outgoing broadcast.
    /// </summary>
    public bool EchoOnLocalHub { get; set; } = true;
}

/// <summary>
/// In-process hub message raised by <see cref="BroadcastPublisher"/> when a broadcast
/// is published (the "local echo"). Subscribe on the <see cref="ITinyMessengerHub"/>
/// to observe outgoing broadcasts for auditing or display.
/// </summary>
public sealed class BroadcastNotificationMessage : TinyMessageBase
{
    /// <summary>The notification that was broadcast.</summary>
    public BroadcastNotification Notification { get; }

    /// <summary>The machine group it was addressed to.</summary>
    public MachineGroup Group { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="BroadcastNotificationMessage"/> class.
    /// </summary>
    public BroadcastNotificationMessage(object sender, BroadcastNotification notification, MachineGroup group)
        : base(sender)
    {
        ArgumentNullException.ThrowIfNull(notification);
        ArgumentNullException.ThrowIfNull(group);

        Notification = notification;
        Group = group;
    }
}
