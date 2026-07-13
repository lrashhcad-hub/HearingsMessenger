//===============================================================================
// TinyMessenger — see licence.txt.
//===============================================================================

namespace HearingsMessenger;

/// <summary>
/// Represents an active subscription to a message. Disposing the token
/// unsubscribes from the hub (if the hub is still alive).
/// </summary>
public sealed class TinyMessageSubscriptionToken : IDisposable
{
    private readonly WeakReference<ITinyMessengerHub> _hub;
    private readonly Type _messageType;

    /// <summary>
    /// The message type this token's subscription was registered for.
    /// </summary>
    public Type MessageType => _messageType;

    /// <summary>
    /// Initializes a new instance of the <see cref="TinyMessageSubscriptionToken"/> class.
    /// </summary>
    /// <param name="hub">The hub the subscription belongs to.</param>
    /// <param name="messageType">The message type subscribed to; must implement <see cref="ITinyMessage"/>.</param>
    public TinyMessageSubscriptionToken(ITinyMessengerHub hub, Type messageType)
    {
        ArgumentNullException.ThrowIfNull(hub);
        ArgumentNullException.ThrowIfNull(messageType);

        if (!typeof(ITinyMessage).IsAssignableFrom(messageType))
            throw new ArgumentOutOfRangeException(nameof(messageType), messageType, "Message type must implement ITinyMessage.");

        _hub = new WeakReference<ITinyMessengerHub>(hub);
        _messageType = messageType;
    }

    /// <summary>
    /// Unsubscribes this token from the hub. Safe to call if the hub has been collected
    /// or the subscription was already removed.
    /// </summary>
    public void Dispose()
    {
        // The original implementation reflected over ITinyMessengerHub to close
        // Unsubscribe<TMessage> at runtime. Unsubscription is by token identity,
        // so the non-generic overload is equivalent — and direct.
        if (_hub.TryGetTarget(out var hub))
        {
            hub.Unsubscribe(this);
        }

        GC.SuppressFinalize(this);
    }
}
