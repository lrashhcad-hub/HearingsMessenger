//===============================================================================
// TinyMessenger — see licence.txt.
//===============================================================================

namespace HearingsMessenger;

/// <summary>
/// Represents a message subscription.
/// </summary>
public interface ITinyMessageSubscription
{
    /// <summary>
    /// Token returned to the subscriber to reference this subscription.
    /// </summary>
    TinyMessageSubscriptionToken SubscriptionToken { get; }

    /// <summary>
    /// Whether delivery should be attempted.
    /// </summary>
    /// <param name="message">Message that may potentially be delivered.</param>
    /// <returns>True — ok to send; False — should not attempt to send.</returns>
    bool ShouldAttemptDelivery(ITinyMessage message);

    /// <summary>
    /// Deliver the message. Synchronous subscribers complete inline
    /// (the returned task is already completed).
    /// </summary>
    /// <param name="message">Message to deliver.</param>
    /// <param name="cancellationToken">Cancellation token (observed by async subscribers).</param>
    Task DeliverAsync(ITinyMessage message, CancellationToken cancellationToken);
}

/// <summary>
/// Message proxy definition.
///
/// A message proxy can be used to intercept/alter messages and/or
/// marshall delivery actions onto a particular thread.
/// </summary>
public interface ITinyMessageProxy
{
    /// <summary>
    /// Deliver the message to the subscription.
    /// </summary>
    Task DeliverAsync(ITinyMessage message, ITinyMessageSubscription subscription, CancellationToken cancellationToken);
}

/// <summary>
/// Default "pass through" proxy.
///
/// Does nothing other than deliver the message.
/// </summary>
public sealed class DefaultTinyMessageProxy : ITinyMessageProxy
{
    /// <summary>
    /// Singleton instance of the proxy.
    /// </summary>
    public static DefaultTinyMessageProxy Instance { get; } = new();

    private DefaultTinyMessageProxy()
    {
    }

    /// <inheritdoc />
    public Task DeliverAsync(ITinyMessage message, ITinyMessageSubscription subscription, CancellationToken cancellationToken)
        => subscription.DeliverAsync(message, cancellationToken);
}

/// <summary>
/// Thrown when an exception occurs while subscribing to a message type.
/// </summary>
public class TinyMessengerSubscriptionException : Exception
{
    private const string ErrorText = "Unable to add subscription for {0} : {1}";

    /// <summary>
    /// Initializes a new instance of the <see cref="TinyMessengerSubscriptionException"/> class.
    /// </summary>
    public TinyMessengerSubscriptionException(Type messageType, string reason)
        : base(string.Format(ErrorText, messageType, reason))
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="TinyMessengerSubscriptionException"/> class.
    /// </summary>
    public TinyMessengerSubscriptionException(Type messageType, string reason, Exception innerException)
        : base(string.Format(ErrorText, messageType, reason), innerException)
    {
    }
}

//===============================================================================
// Internal subscription implementations.
//
// Weak-subscription semantics are preserved deliberately from the original
// library: the hub holds a weak reference to the subscriber's DELEGATE, so
// callers opting into weak subscriptions must keep the delegate alive (e.g.
// store it in a field on the subscribing object). This is the event
// aggregator's memory-leak guard.
//===============================================================================

internal sealed class StrongTinyMessageSubscription<TMessage> : ITinyMessageSubscription
    where TMessage : class, ITinyMessage
{
    private readonly Action<TMessage> _deliveryAction;
    private readonly Func<TMessage, bool> _messageFilter;

    public TinyMessageSubscriptionToken SubscriptionToken { get; }

    public StrongTinyMessageSubscription(TinyMessageSubscriptionToken subscriptionToken, Action<TMessage> deliveryAction, Func<TMessage, bool> messageFilter)
    {
        ArgumentNullException.ThrowIfNull(subscriptionToken);
        ArgumentNullException.ThrowIfNull(deliveryAction);
        ArgumentNullException.ThrowIfNull(messageFilter);

        SubscriptionToken = subscriptionToken;
        _deliveryAction = deliveryAction;
        _messageFilter = messageFilter;
    }

    public bool ShouldAttemptDelivery(ITinyMessage message)
        => message is TMessage typed && _messageFilter(typed);

    public Task DeliverAsync(ITinyMessage message, CancellationToken cancellationToken)
    {
        if (message is not TMessage typed)
            throw new ArgumentException("Message is not the correct type", nameof(message));

        _deliveryAction(typed);
        return Task.CompletedTask;
    }
}

internal sealed class WeakTinyMessageSubscription<TMessage> : ITinyMessageSubscription
    where TMessage : class, ITinyMessage
{
    private readonly WeakReference<Action<TMessage>> _deliveryAction;
    private readonly WeakReference<Func<TMessage, bool>> _messageFilter;

    public TinyMessageSubscriptionToken SubscriptionToken { get; }

    public WeakTinyMessageSubscription(TinyMessageSubscriptionToken subscriptionToken, Action<TMessage> deliveryAction, Func<TMessage, bool> messageFilter)
    {
        ArgumentNullException.ThrowIfNull(subscriptionToken);
        ArgumentNullException.ThrowIfNull(deliveryAction);
        ArgumentNullException.ThrowIfNull(messageFilter);

        SubscriptionToken = subscriptionToken;
        _deliveryAction = new WeakReference<Action<TMessage>>(deliveryAction);
        _messageFilter = new WeakReference<Func<TMessage, bool>>(messageFilter);
    }

    public bool ShouldAttemptDelivery(ITinyMessage message)
    {
        if (message is not TMessage typed)
            return false;

        if (!_deliveryAction.TryGetTarget(out _))
            return false;

        if (!_messageFilter.TryGetTarget(out var filter))
            return false;

        return filter(typed);
    }

    public Task DeliverAsync(ITinyMessage message, CancellationToken cancellationToken)
    {
        if (message is not TMessage typed)
            throw new ArgumentException("Message is not the correct type", nameof(message));

        if (_deliveryAction.TryGetTarget(out var deliveryAction))
        {
            deliveryAction(typed);
        }

        return Task.CompletedTask;
    }
}

internal sealed class StrongAsyncTinyMessageSubscription<TMessage> : ITinyMessageSubscription
    where TMessage : class, ITinyMessage
{
    private readonly Func<TMessage, CancellationToken, Task> _deliveryAction;
    private readonly Func<TMessage, bool> _messageFilter;

    public TinyMessageSubscriptionToken SubscriptionToken { get; }

    public StrongAsyncTinyMessageSubscription(TinyMessageSubscriptionToken subscriptionToken, Func<TMessage, CancellationToken, Task> deliveryAction, Func<TMessage, bool> messageFilter)
    {
        ArgumentNullException.ThrowIfNull(subscriptionToken);
        ArgumentNullException.ThrowIfNull(deliveryAction);
        ArgumentNullException.ThrowIfNull(messageFilter);

        SubscriptionToken = subscriptionToken;
        _deliveryAction = deliveryAction;
        _messageFilter = messageFilter;
    }

    public bool ShouldAttemptDelivery(ITinyMessage message)
        => message is TMessage typed && _messageFilter(typed);

    public Task DeliverAsync(ITinyMessage message, CancellationToken cancellationToken)
    {
        if (message is not TMessage typed)
            throw new ArgumentException("Message is not the correct type", nameof(message));

        return _deliveryAction(typed, cancellationToken);
    }
}

internal sealed class WeakAsyncTinyMessageSubscription<TMessage> : ITinyMessageSubscription
    where TMessage : class, ITinyMessage
{
    private readonly WeakReference<Func<TMessage, CancellationToken, Task>> _deliveryAction;
    private readonly WeakReference<Func<TMessage, bool>> _messageFilter;

    public TinyMessageSubscriptionToken SubscriptionToken { get; }

    public WeakAsyncTinyMessageSubscription(TinyMessageSubscriptionToken subscriptionToken, Func<TMessage, CancellationToken, Task> deliveryAction, Func<TMessage, bool> messageFilter)
    {
        ArgumentNullException.ThrowIfNull(subscriptionToken);
        ArgumentNullException.ThrowIfNull(deliveryAction);
        ArgumentNullException.ThrowIfNull(messageFilter);

        SubscriptionToken = subscriptionToken;
        _deliveryAction = new WeakReference<Func<TMessage, CancellationToken, Task>>(deliveryAction);
        _messageFilter = new WeakReference<Func<TMessage, bool>>(messageFilter);
    }

    public bool ShouldAttemptDelivery(ITinyMessage message)
    {
        if (message is not TMessage typed)
            return false;

        if (!_deliveryAction.TryGetTarget(out _))
            return false;

        if (!_messageFilter.TryGetTarget(out var filter))
            return false;

        return filter(typed);
    }

    public Task DeliverAsync(ITinyMessage message, CancellationToken cancellationToken)
    {
        if (message is not TMessage typed)
            throw new ArgumentException("Message is not the correct type", nameof(message));

        if (_deliveryAction.TryGetTarget(out var deliveryAction))
        {
            return deliveryAction(typed, cancellationToken);
        }

        return Task.CompletedTask;
    }
}
