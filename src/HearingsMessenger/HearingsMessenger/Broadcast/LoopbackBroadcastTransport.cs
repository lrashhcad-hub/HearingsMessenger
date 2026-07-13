//===============================================================================
// TinyMessenger.Broadcast — one-way broadcast layer for enterprise workstations.
// See MODERNIZATION.md and licence.txt.
//===============================================================================

using System.Collections.Concurrent;

namespace HearingsMessenger.Broadcast;

/// <summary>
/// In-memory transport for development and testing: records every send instead
/// of touching the network, and can invoke an optional callback per send.
/// Thread-safe.
/// </summary>
public sealed class LoopbackBroadcastTransport : IBroadcastTransport
{
    private readonly ConcurrentQueue<LoopbackBroadcastRecord> _sent = new();
    private readonly Func<BroadcastNotification, MachineGroup, CancellationToken, Task>? _onSend;

    /// <inheritdoc />
    public string Name => "Loopback";

    /// <summary>
    /// Everything sent through this transport, in order.
    /// </summary>
    public IReadOnlyCollection<LoopbackBroadcastRecord> Sent => _sent;

    /// <summary>
    /// Initializes a new instance of the <see cref="LoopbackBroadcastTransport"/> class.
    /// </summary>
    /// <param name="onSend">Optional callback invoked for every send (e.g. to simulate failures in tests).</param>
    public LoopbackBroadcastTransport(Func<BroadcastNotification, MachineGroup, CancellationToken, Task>? onSend = null)
    {
        _onSend = onSend;
    }

    /// <inheritdoc />
    public async Task SendAsync(BroadcastNotification notification, MachineGroup group, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(notification);
        ArgumentNullException.ThrowIfNull(group);

        cancellationToken.ThrowIfCancellationRequested();

        _sent.Enqueue(new LoopbackBroadcastRecord(notification, group, DateTimeOffset.UtcNow));

        if (_onSend is not null)
        {
            await _onSend(notification, group, cancellationToken).ConfigureAwait(false);
        }
    }
}

/// <summary>
/// A single recorded send through a <see cref="LoopbackBroadcastTransport"/>.
/// </summary>
public sealed record LoopbackBroadcastRecord(
    BroadcastNotification Notification,
    MachineGroup Group,
    DateTimeOffset SentUtc);
