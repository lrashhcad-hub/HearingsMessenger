//===============================================================================
// TinyMessenger — see licence.txt.
//===============================================================================

namespace HearingsMessenger;

/// <summary>
/// Messenger hub responsible for taking subscriptions/publications and delivering of messages.
/// </summary>
public sealed class TinyMessengerHub : ITinyMessengerHub
{
    private sealed record SubscriptionItem(ITinyMessageProxy Proxy, ITinyMessageSubscription Subscription);

    private readonly ISubscriberErrorHandler _subscriberErrorHandler;
    private readonly object _subscriptionsPadlock = new();
    private readonly List<SubscriptionItem> _subscriptions = [];

    /// <summary>
    /// Initializes a new instance of the <see cref="TinyMessengerHub"/> class
    /// using the default (exception-swallowing) subscriber error handler.
    /// </summary>
    public TinyMessengerHub()
        : this(new DefaultSubscriberErrorHandler())
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="TinyMessengerHub"/> class
    /// with a custom subscriber error handler.
    /// </summary>
    /// <param name="subscriberErrorHandler">Handler invoked when a subscriber throws during delivery.</param>
    public TinyMessengerHub(ISubscriberErrorHandler subscriberErrorHandler)
    {
        ArgumentNullException.ThrowIfNull(subscriberErrorHandler);

        _subscriberErrorHandler = subscriberErrorHandler;
    }

    #region Subscribe (synchronous handlers)

    /// <inheritdoc />
    public TinyMessageSubscriptionToken Subscribe<TMessage>(Action<TMessage> deliveryAction) where TMessage : class, ITinyMessage
        => AddSubscriptionInternal(deliveryAction, static _ => true, true, DefaultTinyMessageProxy.Instance);

    /// <inheritdoc />
    public TinyMessageSubscriptionToken Subscribe<TMessage>(Action<TMessage> deliveryAction, ITinyMessageProxy proxy) where TMessage : class, ITinyMessage
        => AddSubscriptionInternal(deliveryAction, static _ => true, true, proxy);

    /// <inheritdoc />
    public TinyMessageSubscriptionToken Subscribe<TMessage>(Action<TMessage> deliveryAction, bool useStrongReferences) where TMessage : class, ITinyMessage
        => AddSubscriptionInternal(deliveryAction, static _ => true, useStrongReferences, DefaultTinyMessageProxy.Instance);

    /// <inheritdoc />
    public TinyMessageSubscriptionToken Subscribe<TMessage>(Action<TMessage> deliveryAction, bool useStrongReferences, ITinyMessageProxy proxy) where TMessage : class, ITinyMessage
        => AddSubscriptionInternal(deliveryAction, static _ => true, useStrongReferences, proxy);

    /// <inheritdoc />
    public TinyMessageSubscriptionToken Subscribe<TMessage>(Action<TMessage> deliveryAction, Func<TMessage, bool> messageFilter) where TMessage : class, ITinyMessage
        => AddSubscriptionInternal(deliveryAction, messageFilter, true, DefaultTinyMessageProxy.Instance);

    /// <inheritdoc />
    public TinyMessageSubscriptionToken Subscribe<TMessage>(Action<TMessage> deliveryAction, Func<TMessage, bool> messageFilter, ITinyMessageProxy proxy) where TMessage : class, ITinyMessage
        => AddSubscriptionInternal(deliveryAction, messageFilter, true, proxy);

    /// <inheritdoc />
    public TinyMessageSubscriptionToken Subscribe<TMessage>(Action<TMessage> deliveryAction, Func<TMessage, bool> messageFilter, bool useStrongReferences) where TMessage : class, ITinyMessage
        => AddSubscriptionInternal(deliveryAction, messageFilter, useStrongReferences, DefaultTinyMessageProxy.Instance);

    /// <inheritdoc />
    public TinyMessageSubscriptionToken Subscribe<TMessage>(Action<TMessage> deliveryAction, Func<TMessage, bool> messageFilter, bool useStrongReferences, ITinyMessageProxy proxy) where TMessage : class, ITinyMessage
        => AddSubscriptionInternal(deliveryAction, messageFilter, useStrongReferences, proxy);

    #endregion

    #region SubscribeAsync (asynchronous handlers)

    /// <inheritdoc />
    public TinyMessageSubscriptionToken SubscribeAsync<TMessage>(Func<TMessage, CancellationToken, Task> deliveryAction) where TMessage : class, ITinyMessage
        => AddAsyncSubscriptionInternal(deliveryAction, static _ => true, true);

    /// <inheritdoc />
    public TinyMessageSubscriptionToken SubscribeAsync<TMessage>(Func<TMessage, CancellationToken, Task> deliveryAction, Func<TMessage, bool> messageFilter) where TMessage : class, ITinyMessage
        => AddAsyncSubscriptionInternal(deliveryAction, messageFilter, true);

    /// <inheritdoc />
    public TinyMessageSubscriptionToken SubscribeAsync<TMessage>(Func<TMessage, CancellationToken, Task> deliveryAction, bool useStrongReferences) where TMessage : class, ITinyMessage
        => AddAsyncSubscriptionInternal(deliveryAction, static _ => true, useStrongReferences);

    /// <inheritdoc />
    public TinyMessageSubscriptionToken SubscribeAsync<TMessage>(Func<TMessage, CancellationToken, Task> deliveryAction, Func<TMessage, bool> messageFilter, bool useStrongReferences) where TMessage : class, ITinyMessage
        => AddAsyncSubscriptionInternal(deliveryAction, messageFilter, useStrongReferences);

    #endregion

    #region Unsubscribe / Publish

    /// <inheritdoc />
    public void Unsubscribe<TMessage>(TinyMessageSubscriptionToken subscriptionToken) where TMessage : class, ITinyMessage
        => RemoveSubscriptionInternal(subscriptionToken);

    /// <inheritdoc />
    public void Unsubscribe(TinyMessageSubscriptionToken subscriptionToken)
        => RemoveSubscriptionInternal(subscriptionToken);

    /// <inheritdoc />
    public void Publish<TMessage>(TMessage message) where TMessage : class, ITinyMessage
    {
        ArgumentNullException.ThrowIfNull(message);

        foreach (var item in GetDeliverableSubscriptions(message))
        {
            try
            {
                // Synchronous subscribers complete inline, so this does not block
                // in the common (sync-only) case. Async subscribers are awaited.
                item.Proxy.DeliverAsync(message, item.Subscription, CancellationToken.None)
                    .GetAwaiter()
                    .GetResult();
            }
            catch (Exception exception)
            {
                // By default ignore any errors and carry on.
                _subscriberErrorHandler.Handle(message, exception);
            }
        }
    }

    /// <inheritdoc />
    public async Task PublishAsync<TMessage>(TMessage message, CancellationToken cancellationToken = default) where TMessage : class, ITinyMessage
    {
        ArgumentNullException.ThrowIfNull(message);

        foreach (var item in GetDeliverableSubscriptions(message))
        {
            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                await item.Proxy.DeliverAsync(message, item.Subscription, cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception exception)
            {
                // By default ignore any errors and carry on.
                _subscriberErrorHandler.Handle(message, exception);
            }
        }
    }

    #endregion

    #region Internal Methods

    private TinyMessageSubscriptionToken AddSubscriptionInternal<TMessage>(Action<TMessage> deliveryAction, Func<TMessage, bool> messageFilter, bool strongReference, ITinyMessageProxy proxy)
        where TMessage : class, ITinyMessage
    {
        ArgumentNullException.ThrowIfNull(deliveryAction);
        ArgumentNullException.ThrowIfNull(messageFilter);
        ArgumentNullException.ThrowIfNull(proxy);

        lock (_subscriptionsPadlock)
        {
            var subscriptionToken = new TinyMessageSubscriptionToken(this, typeof(TMessage));

            ITinyMessageSubscription subscription = strongReference
                ? new StrongTinyMessageSubscription<TMessage>(subscriptionToken, deliveryAction, messageFilter)
                : new WeakTinyMessageSubscription<TMessage>(subscriptionToken, deliveryAction, messageFilter);

            _subscriptions.Add(new SubscriptionItem(proxy, subscription));

            return subscriptionToken;
        }
    }

    private TinyMessageSubscriptionToken AddAsyncSubscriptionInternal<TMessage>(Func<TMessage, CancellationToken, Task> deliveryAction, Func<TMessage, bool> messageFilter, bool strongReference)
        where TMessage : class, ITinyMessage
    {
        ArgumentNullException.ThrowIfNull(deliveryAction);
        ArgumentNullException.ThrowIfNull(messageFilter);

        lock (_subscriptionsPadlock)
        {
            var subscriptionToken = new TinyMessageSubscriptionToken(this, typeof(TMessage));

            ITinyMessageSubscription subscription = strongReference
                ? new StrongAsyncTinyMessageSubscription<TMessage>(subscriptionToken, deliveryAction, messageFilter)
                : new WeakAsyncTinyMessageSubscription<TMessage>(subscriptionToken, deliveryAction, messageFilter);

            _subscriptions.Add(new SubscriptionItem(DefaultTinyMessageProxy.Instance, subscription));

            return subscriptionToken;
        }
    }

    private void RemoveSubscriptionInternal(TinyMessageSubscriptionToken subscriptionToken)
    {
        ArgumentNullException.ThrowIfNull(subscriptionToken);

        lock (_subscriptionsPadlock)
        {
            _subscriptions.RemoveAll(sub => ReferenceEquals(sub.Subscription.SubscriptionToken, subscriptionToken));
        }
    }

    private List<SubscriptionItem> GetDeliverableSubscriptions(ITinyMessage message)
    {
        lock (_subscriptionsPadlock)
        {
            return _subscriptions.Where(sub => sub.Subscription.ShouldAttemptDelivery(message)).ToList();
        }
    }

    #endregion
}
