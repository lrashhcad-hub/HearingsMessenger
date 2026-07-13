//===============================================================================
// TinyMessenger — see licence.txt.
//===============================================================================

namespace HearingsMessenger;

/// <summary>
/// Handles exceptions thrown by subscriber delivery actions during publish.
/// </summary>
public interface ISubscriberErrorHandler
{
    /// <summary>
    /// Handle an exception thrown by a subscriber while a message was being delivered.
    /// </summary>
    /// <param name="message">The message that was being delivered.</param>
    /// <param name="exception">The exception the subscriber threw.</param>
    void Handle(ITinyMessage message, Exception exception);
}

/// <summary>
/// Default subscriber error handler: subscriber exceptions are swallowed so one
/// faulty subscriber cannot break delivery to the others (original TinyMessenger behavior).
/// </summary>
public class DefaultSubscriberErrorHandler : ISubscriberErrorHandler
{
    /// <inheritdoc />
    public void Handle(ITinyMessage message, Exception exception)
    {
        // Default behaviour is to do nothing.
    }
}
