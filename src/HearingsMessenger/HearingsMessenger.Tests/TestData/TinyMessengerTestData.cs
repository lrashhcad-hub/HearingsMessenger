//===============================================================================
// TinyMessenger — see licence.txt.
//===============================================================================

namespace HearingsMessenger.Tests.TestData;

public class TestMessage : TinyMessageBase
{
    public TestMessage(object sender) : base(sender)
    {
    }
}

public class DerivedMessage<TThings> : TestMessage
{
    public TThings? Things { get; set; }

    public DerivedMessage(object sender)
        : base(sender)
    {
    }
}

public interface ITestMessageInterface : ITinyMessage
{
}

public class InterfaceDerivedMessage<TThings> : ITestMessageInterface
{
    public object? Sender { get; }

    public TThings? Things { get; set; }

    public InterfaceDerivedMessage(object sender)
    {
        Sender = sender;
    }
}

public class TestProxy : ITinyMessageProxy
{
    public ITinyMessage? Message { get; private set; }

    public Task DeliverAsync(ITinyMessage message, ITinyMessageSubscription subscription, CancellationToken cancellationToken)
    {
        Message = message;
        return subscription.DeliverAsync(message, cancellationToken);
    }
}

public class TestSubscriptionErrorHandler : ISubscriberErrorHandler
{
    public void Handle(ITinyMessage message, Exception exception)
    {
        throw exception;
    }
}
