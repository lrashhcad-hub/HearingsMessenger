//===============================================================================
// TinyMessenger
//
// A simple messenger/event aggregator.
//
// https://github.com/grumpydev/TinyMessenger
//===============================================================================
// Copyright © Steven Robbins.  All rights reserved.
// THIS CODE AND INFORMATION IS PROVIDED "AS IS" WITHOUT WARRANTY
// OF ANY KIND, EITHER EXPRESSED OR IMPLIED, INCLUDING BUT NOT
// LIMITED TO THE IMPLIED WARRANTIES OF MERCHANTABILITY AND
// FITNESS FOR A PARTICULAR PURPOSE.
//===============================================================================

namespace HearingsMessenger;

/// <summary>
/// A TinyMessage to be published/delivered by TinyMessenger.
/// </summary>
public interface ITinyMessage
{
    /// <summary>
    /// The sender of the message, or null if not supported by the message implementation
    /// (or if the sender has been garbage collected).
    /// </summary>
    object? Sender { get; }
}

/// <summary>
/// Base class for messages that provides weak reference storage of the sender.
/// </summary>
public abstract class TinyMessageBase : ITinyMessage
{
    /// <summary>
    /// Store a WeakReference to the sender just in case anyone is daft enough to
    /// keep the message around and prevent the sender from being collected.
    /// </summary>
    private readonly WeakReference<object> _sender;

    /// <inheritdoc />
    public object? Sender => _sender.TryGetTarget(out var sender) ? sender : null;

    /// <summary>
    /// Initializes a new instance of the <see cref="TinyMessageBase"/> class.
    /// </summary>
    /// <param name="sender">Message sender (usually "this").</param>
    protected TinyMessageBase(object sender)
    {
        ArgumentNullException.ThrowIfNull(sender);

        _sender = new WeakReference<object>(sender);
    }
}

/// <summary>
/// Generic message with user specified content.
/// </summary>
/// <typeparam name="TContent">Content type to store.</typeparam>
public class GenericTinyMessage<TContent> : TinyMessageBase
{
    /// <summary>
    /// Contents of the message.
    /// </summary>
    public TContent Content { get; protected set; }

    /// <summary>
    /// Create a new instance of the <see cref="GenericTinyMessage{TContent}"/> class.
    /// </summary>
    /// <param name="sender">Message sender (usually "this").</param>
    /// <param name="content">Contents of the message.</param>
    public GenericTinyMessage(object sender, TContent content)
        : base(sender)
    {
        Content = content;
    }
}

/// <summary>
/// Basic "cancellable" generic message.
/// </summary>
/// <typeparam name="TContent">Content type to store.</typeparam>
public class CancellableGenericTinyMessage<TContent> : TinyMessageBase
{
    /// <summary>
    /// Cancel action.
    /// </summary>
    public Action Cancel { get; protected set; }

    /// <summary>
    /// Contents of the message.
    /// </summary>
    public TContent Content { get; protected set; }

    /// <summary>
    /// Create a new instance of the <see cref="CancellableGenericTinyMessage{TContent}"/> class.
    /// </summary>
    /// <param name="sender">Message sender (usually "this").</param>
    /// <param name="content">Contents of the message.</param>
    /// <param name="cancelAction">Action to call for cancellation.</param>
    public CancellableGenericTinyMessage(object sender, TContent content, Action cancelAction)
        : base(sender)
    {
        ArgumentNullException.ThrowIfNull(cancelAction);

        Content = content;
        Cancel = cancelAction;
    }
}
