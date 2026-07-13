//===============================================================================
// TinyMessenger — see licence.txt.
//===============================================================================

namespace HearingsMessenger;

/// <summary>
/// Messenger hub responsible for taking subscriptions/publications and delivering of messages.
/// </summary>
public interface ITinyMessengerHub
{
    /// <summary>
    /// Subscribe to a message type with the given delivery action.
    /// All references are held with strong references.
    ///
    /// All messages of this type will be delivered.
    /// </summary>
    /// <typeparam name="TMessage">Type of message.</typeparam>
    /// <param name="deliveryAction">Action to invoke when message is delivered.</param>
    /// <returns>Token used for unsubscribing.</returns>
    TinyMessageSubscriptionToken Subscribe<TMessage>(Action<TMessage> deliveryAction) where TMessage : class, ITinyMessage;

    /// <summary>
    /// Subscribe to a message type with the given delivery action.
    /// Messages will be delivered via the specified proxy.
    /// All references (apart from the proxy) are held with strong references.
    ///
    /// All messages of this type will be delivered.
    /// </summary>
    /// <typeparam name="TMessage">Type of message.</typeparam>
    /// <param name="deliveryAction">Action to invoke when message is delivered.</param>
    /// <param name="proxy">Proxy to use when delivering the messages.</param>
    /// <returns>Token used for unsubscribing.</returns>
    TinyMessageSubscriptionToken Subscribe<TMessage>(Action<TMessage> deliveryAction, ITinyMessageProxy proxy) where TMessage : class, ITinyMessage;

    /// <summary>
    /// Subscribe to a message type with the given delivery action.
    ///
    /// All messages of this type will be delivered.
    /// </summary>
    /// <typeparam name="TMessage">Type of message.</typeparam>
    /// <param name="deliveryAction">Action to invoke when message is delivered.</param>
    /// <param name="useStrongReferences">Use strong references to the delivery action; if false, the caller must keep the delegate alive.</param>
    /// <returns>Token used for unsubscribing.</returns>
    TinyMessageSubscriptionToken Subscribe<TMessage>(Action<TMessage> deliveryAction, bool useStrongReferences) where TMessage : class, ITinyMessage;

    /// <summary>
    /// Subscribe to a message type with the given delivery action.
    /// Messages will be delivered via the specified proxy.
    ///
    /// All messages of this type will be delivered.
    /// </summary>
    /// <typeparam name="TMessage">Type of message.</typeparam>
    /// <param name="deliveryAction">Action to invoke when message is delivered.</param>
    /// <param name="useStrongReferences">Use strong references to the delivery action; if false, the caller must keep the delegate alive.</param>
    /// <param name="proxy">Proxy to use when delivering the messages.</param>
    /// <returns>Token used for unsubscribing.</returns>
    TinyMessageSubscriptionToken Subscribe<TMessage>(Action<TMessage> deliveryAction, bool useStrongReferences, ITinyMessageProxy proxy) where TMessage : class, ITinyMessage;

    /// <summary>
    /// Subscribe to a message type with the given delivery action and filter.
    /// All references are held with strong references.
    ///
    /// Only messages that "pass" the filter will be delivered.
    /// </summary>
    /// <typeparam name="TMessage">Type of message.</typeparam>
    /// <param name="deliveryAction">Action to invoke when message is delivered.</param>
    /// <param name="messageFilter">Only messages for which this predicate returns true are delivered.</param>
    /// <returns>Token used for unsubscribing.</returns>
    TinyMessageSubscriptionToken Subscribe<TMessage>(Action<TMessage> deliveryAction, Func<TMessage, bool> messageFilter) where TMessage : class, ITinyMessage;

    /// <summary>
    /// Subscribe to a message type with the given delivery action and filter.
    /// Messages will be delivered via the specified proxy.
    /// All references (apart from the proxy) are held with strong references.
    ///
    /// Only messages that "pass" the filter will be delivered.
    /// </summary>
    /// <typeparam name="TMessage">Type of message.</typeparam>
    /// <param name="deliveryAction">Action to invoke when message is delivered.</param>
    /// <param name="messageFilter">Only messages for which this predicate returns true are delivered.</param>
    /// <param name="proxy">Proxy to use when delivering the messages.</param>
    /// <returns>Token used for unsubscribing.</returns>
    TinyMessageSubscriptionToken Subscribe<TMessage>(Action<TMessage> deliveryAction, Func<TMessage, bool> messageFilter, ITinyMessageProxy proxy) where TMessage : class, ITinyMessage;

    /// <summary>
    /// Subscribe to a message type with the given delivery action and filter.
    ///
    /// Only messages that "pass" the filter will be delivered.
    /// </summary>
    /// <typeparam name="TMessage">Type of message.</typeparam>
    /// <param name="deliveryAction">Action to invoke when message is delivered.</param>
    /// <param name="messageFilter">Only messages for which this predicate returns true are delivered.</param>
    /// <param name="useStrongReferences">Use strong references to the delivery action and filter; if false, the caller must keep the delegates alive.</param>
    /// <returns>Token used for unsubscribing.</returns>
    TinyMessageSubscriptionToken Subscribe<TMessage>(Action<TMessage> deliveryAction, Func<TMessage, bool> messageFilter, bool useStrongReferences) where TMessage : class, ITinyMessage;

    /// <summary>
    /// Subscribe to a message type with the given delivery action and filter.
    /// Messages will be delivered via the specified proxy.
    ///
    /// Only messages that "pass" the filter will be delivered.
    /// </summary>
    /// <typeparam name="TMessage">Type of message.</typeparam>
    /// <param name="deliveryAction">Action to invoke when message is delivered.</param>
    /// <param name="messageFilter">Only messages for which this predicate returns true are delivered.</param>
    /// <param name="useStrongReferences">Use strong references to the delivery action and filter; if false, the caller must keep the delegates alive.</param>
    /// <param name="proxy">Proxy to use when delivering the messages.</param>
    /// <returns>Token used for unsubscribing.</returns>
    TinyMessageSubscriptionToken Subscribe<TMessage>(Action<TMessage> deliveryAction, Func<TMessage, bool> messageFilter, bool useStrongReferences, ITinyMessageProxy proxy) where TMessage : class, ITinyMessage;

    /// <summary>
    /// Subscribe to a message type with an asynchronous handler.
    /// All references are held with strong references.
    /// </summary>
    /// <typeparam name="TMessage">Type of message.</typeparam>
    /// <param name="deliveryAction">Async handler invoked when message is delivered.</param>
    /// <returns>Token used for unsubscribing.</returns>
    TinyMessageSubscriptionToken SubscribeAsync<TMessage>(Func<TMessage, CancellationToken, Task> deliveryAction) where TMessage : class, ITinyMessage;

    /// <summary>
    /// Subscribe to a message type with an asynchronous handler and a filter.
    /// All references are held with strong references.
    /// </summary>
    /// <typeparam name="TMessage">Type of message.</typeparam>
    /// <param name="deliveryAction">Async handler invoked when message is delivered.</param>
    /// <param name="messageFilter">Only messages for which this predicate returns true are delivered.</param>
    /// <returns>Token used for unsubscribing.</returns>
    TinyMessageSubscriptionToken SubscribeAsync<TMessage>(Func<TMessage, CancellationToken, Task> deliveryAction, Func<TMessage, bool> messageFilter) where TMessage : class, ITinyMessage;

    /// <summary>
    /// Subscribe to a message type with an asynchronous handler.
    /// </summary>
    /// <typeparam name="TMessage">Type of message.</typeparam>
    /// <param name="deliveryAction">Async handler invoked when message is delivered.</param>
    /// <param name="useStrongReferences">Use strong references to the handler; if false, the caller must keep the delegate alive.</param>
    /// <returns>Token used for unsubscribing.</returns>
    TinyMessageSubscriptionToken SubscribeAsync<TMessage>(Func<TMessage, CancellationToken, Task> deliveryAction, bool useStrongReferences) where TMessage : class, ITinyMessage;

    /// <summary>
    /// Subscribe to a message type with an asynchronous handler and a filter.
    /// </summary>
    /// <typeparam name="TMessage">Type of message.</typeparam>
    /// <param name="deliveryAction">Async handler invoked when message is delivered.</param>
    /// <param name="messageFilter">Only messages for which this predicate returns true are delivered.</param>
    /// <param name="useStrongReferences">Use strong references to the handler and filter; if false, the caller must keep the delegates alive.</param>
    /// <returns>Token used for unsubscribing.</returns>
    TinyMessageSubscriptionToken SubscribeAsync<TMessage>(Func<TMessage, CancellationToken, Task> deliveryAction, Func<TMessage, bool> messageFilter, bool useStrongReferences) where TMessage : class, ITinyMessage;

    /// <summary>
    /// Unsubscribe from a particular message type.
    ///
    /// Does not throw an exception if the subscription is not found.
    /// </summary>
    /// <typeparam name="TMessage">Type of message.</typeparam>
    /// <param name="subscriptionToken">Subscription token received from Subscribe.</param>
    void Unsubscribe<TMessage>(TinyMessageSubscriptionToken subscriptionToken) where TMessage : class, ITinyMessage;

    /// <summary>
    /// Unsubscribe from a particular message type.
    ///
    /// Does not throw an exception if the subscription is not found.
    /// </summary>
    /// <param name="subscriptionToken">Subscription token received from Subscribe.</param>
    void Unsubscribe(TinyMessageSubscriptionToken subscriptionToken);

    /// <summary>
    /// Publish a message to any subscribers. Synchronous subscribers complete inline;
    /// asynchronous subscribers are awaited (blocking) — prefer <see cref="PublishAsync{TMessage}"/>
    /// when async subscribers may be registered.
    /// </summary>
    /// <typeparam name="TMessage">Type of message.</typeparam>
    /// <param name="message">Message to deliver.</param>
    void Publish<TMessage>(TMessage message) where TMessage : class, ITinyMessage;

    /// <summary>
    /// Publish a message to any subscribers asynchronously.
    /// </summary>
    /// <typeparam name="TMessage">Type of message.</typeparam>
    /// <param name="message">Message to deliver.</param>
    /// <param name="cancellationToken">Cancellation token, observed between deliveries and by async subscribers.</param>
    /// <returns>A task that completes when all subscribers have been delivered to.</returns>
    Task PublishAsync<TMessage>(TMessage message, CancellationToken cancellationToken = default) where TMessage : class, ITinyMessage;
}
