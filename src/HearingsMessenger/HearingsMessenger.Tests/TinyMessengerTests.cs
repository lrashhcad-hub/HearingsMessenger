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

using Microsoft.VisualStudio.TestTools.UnitTesting;
using HearingsMessenger.Tests.TestData;

namespace HearingsMessenger.Tests;

[TestClass]
public class TinyMessengerTests
{
    [TestMethod]
    public void TinyMessenger_Ctor_DoesNotThrow()
    {
        _ = UtilityMethods.GetMessenger();
    }

    [TestMethod]
    public void Subscribe_ValidDeliverAction_DoesNotThrow()
    {
        var messenger = UtilityMethods.GetMessenger();

        messenger.Subscribe<TestMessage>(UtilityMethods.FakeDeliveryAction);
    }

    [TestMethod]
    public void Subscribe_ValidDeliveryAction_ReturnsRegistrationObject()
    {
        var messenger = UtilityMethods.GetMessenger();

        var output = messenger.Subscribe<TestMessage>(UtilityMethods.FakeDeliveryAction);

        Assert.IsInstanceOfType<TinyMessageSubscriptionToken>(output);
    }

    [TestMethod]
    public void Subscribe_ValidDeliverActionWithStrongReferences_DoesNotThrow()
    {
        var messenger = UtilityMethods.GetMessenger();

        messenger.Subscribe<TestMessage>(UtilityMethods.FakeDeliveryAction, true);
    }

    [TestMethod]
    public void Subscribe_ValidDeliveryActionAndFilter_DoesNotThrow()
    {
        var messenger = UtilityMethods.GetMessenger();

        messenger.Subscribe<TestMessage>(UtilityMethods.FakeDeliveryAction, UtilityMethods.FakeMessageFilter);
    }

    [TestMethod]
    public void Subscribe_NullDeliveryAction_Throws()
    {
        var messenger = UtilityMethods.GetMessenger();

        Assert.ThrowsException<ArgumentNullException>(
            () => messenger.Subscribe<TestMessage>(null!, UtilityMethods.FakeMessageFilter));
    }

    [TestMethod]
    public void Subscribe_NullFilter_Throws()
    {
        var messenger = UtilityMethods.GetMessenger();

        Assert.ThrowsException<ArgumentNullException>(
            () => messenger.Subscribe<TestMessage>(UtilityMethods.FakeDeliveryAction, (Func<TestMessage, bool>)null!));
    }

    [TestMethod]
    public void Subscribe_NullProxy_Throws()
    {
        var messenger = UtilityMethods.GetMessenger();

        Assert.ThrowsException<ArgumentNullException>(
            () => messenger.Subscribe<TestMessage>(UtilityMethods.FakeDeliveryAction, (ITinyMessageProxy)null!));
    }

    [TestMethod]
    public void Unsubscribe_NullSubscriptionObject_Throws()
    {
        var messenger = UtilityMethods.GetMessenger();

        Assert.ThrowsException<ArgumentNullException>(
            () => messenger.Unsubscribe<TestMessage>(null!));
    }

    [TestMethod]
    public void Unsubscribe_PreviousSubscription_DoesNotThrow()
    {
        var messenger = UtilityMethods.GetMessenger();
        var subscription = messenger.Subscribe<TestMessage>(UtilityMethods.FakeDeliveryAction);

        messenger.Unsubscribe<TestMessage>(subscription);
    }

    [TestMethod]
    public void Subscribe_PreviousSubscription_ReturnsDifferentSubscriptionObject()
    {
        var messenger = UtilityMethods.GetMessenger();
        var sub1 = messenger.Subscribe<TestMessage>(UtilityMethods.FakeDeliveryAction);
        var sub2 = messenger.Subscribe<TestMessage>(UtilityMethods.FakeDeliveryAction);

        Assert.AreNotSame(sub1, sub2);
    }

    [TestMethod]
    public void Subscribe_CustomProxyNoFilter_DoesNotThrow()
    {
        var messenger = UtilityMethods.GetMessenger();
        var proxy = new TestProxy();

        messenger.Subscribe<TestMessage>(UtilityMethods.FakeDeliveryAction, proxy);
    }

    [TestMethod]
    public void Subscribe_CustomProxyWithFilter_DoesNotThrow()
    {
        var messenger = UtilityMethods.GetMessenger();
        var proxy = new TestProxy();

        messenger.Subscribe<TestMessage>(UtilityMethods.FakeDeliveryAction, UtilityMethods.FakeMessageFilter, proxy);
    }

    [TestMethod]
    public void Subscribe_CustomProxyNoFilterStrongReference_DoesNotThrow()
    {
        var messenger = UtilityMethods.GetMessenger();
        var proxy = new TestProxy();

        messenger.Subscribe<TestMessage>(UtilityMethods.FakeDeliveryAction, true, proxy);
    }

    [TestMethod]
    public void Subscribe_CustomProxyFilterStrongReference_DoesNotThrow()
    {
        var messenger = UtilityMethods.GetMessenger();
        var proxy = new TestProxy();

        messenger.Subscribe<TestMessage>(UtilityMethods.FakeDeliveryAction, UtilityMethods.FakeMessageFilter, true, proxy);
    }

    [TestMethod]
    public void Publish_CustomProxyNoFilter_UsesCorrectProxy()
    {
        var messenger = UtilityMethods.GetMessenger();
        var proxy = new TestProxy();
        messenger.Subscribe<TestMessage>(UtilityMethods.FakeDeliveryAction, proxy);
        var message = new TestMessage(this);

        messenger.Publish(message);

        // Original used the unasserted Assert.ReferenceEquals — AreSame actually asserts.
        Assert.AreSame(message, proxy.Message);
    }

    [TestMethod]
    public void Publish_CustomProxyWithFilter_UsesCorrectProxy()
    {
        var messenger = UtilityMethods.GetMessenger();
        var proxy = new TestProxy();
        messenger.Subscribe<TestMessage>(UtilityMethods.FakeDeliveryAction, UtilityMethods.FakeMessageFilter, proxy);
        var message = new TestMessage(this);

        messenger.Publish(message);

        Assert.AreSame(message, proxy.Message);
    }

    [TestMethod]
    public void Publish_CustomProxyNoFilterStrongReference_UsesCorrectProxy()
    {
        var messenger = UtilityMethods.GetMessenger();
        var proxy = new TestProxy();
        messenger.Subscribe<TestMessage>(UtilityMethods.FakeDeliveryAction, true, proxy);
        var message = new TestMessage(this);

        messenger.Publish(message);

        Assert.AreSame(message, proxy.Message);
    }

    [TestMethod]
    public void Publish_CustomProxyFilterStrongReference_UsesCorrectProxy()
    {
        var messenger = UtilityMethods.GetMessenger();
        var proxy = new TestProxy();
        messenger.Subscribe<TestMessage>(UtilityMethods.FakeDeliveryAction, UtilityMethods.FakeMessageFilter, true, proxy);
        var message = new TestMessage(this);

        messenger.Publish(message);

        Assert.AreSame(message, proxy.Message);
    }

    [TestMethod]
    public void Publish_NullMessage_Throws()
    {
        var messenger = UtilityMethods.GetMessenger();

        Assert.ThrowsException<ArgumentNullException>(
            () => messenger.Publish<TestMessage>(null!));
    }

    [TestMethod]
    public void Publish_NoSubscribers_DoesNotThrow()
    {
        var messenger = UtilityMethods.GetMessenger();

        messenger.Publish(new TestMessage(this));
    }

    [TestMethod]
    public void Publish_Subscriber_DoesNotThrow()
    {
        var messenger = UtilityMethods.GetMessenger();
        messenger.Subscribe<TestMessage>(UtilityMethods.FakeDeliveryAction);

        messenger.Publish(new TestMessage(this));
    }

    [TestMethod]
    public void Publish_SubscribedMessageNoFilter_GetsMessage()
    {
        var messenger = UtilityMethods.GetMessenger();
        bool received = false;
        messenger.Subscribe<TestMessage>(_ => received = true);

        messenger.Publish(new TestMessage(this));

        Assert.IsTrue(received);
    }

    [TestMethod]
    public void Publish_SubscribedThenUnsubscribedMessageNoFilter_DoesNotGetMessage()
    {
        var messenger = UtilityMethods.GetMessenger();
        bool received = false;
        var token = messenger.Subscribe<TestMessage>(_ => received = true);
        messenger.Unsubscribe<TestMessage>(token);

        messenger.Publish(new TestMessage(this));

        Assert.IsFalse(received);
    }

    [TestMethod]
    public void Publish_SubscribedMessageButFiltered_DoesNotGetMessage()
    {
        var messenger = UtilityMethods.GetMessenger();
        bool received = false;
        messenger.Subscribe<TestMessage>(_ => received = true, _ => false);

        messenger.Publish(new TestMessage(this));

        Assert.IsFalse(received);
    }

    [TestMethod]
    public void Publish_SubscribedMessageNoFilter_GetsActualMessage()
    {
        var messenger = UtilityMethods.GetMessenger();
        ITinyMessage? receivedMessage = null;
        var payload = new TestMessage(this);
        messenger.Subscribe<TestMessage>(m => receivedMessage = m);

        messenger.Publish(payload);

        Assert.AreSame(payload, receivedMessage);
    }

    [TestMethod]
    public void GenericTinyMessage_String_SubscribeDoesNotThrow()
    {
        var messenger = UtilityMethods.GetMessenger();

        messenger.Subscribe<GenericTinyMessage<string>>(_ => { });
    }

    [TestMethod]
    public void GenericTinyMessage_String_PublishDoesNotThrow()
    {
        var messenger = UtilityMethods.GetMessenger();

        messenger.Publish(new GenericTinyMessage<string>(this, "Testing"));
    }

    [TestMethod]
    public void GenericTinyMessage_String_PublishAndSubscribeDeliversContent()
    {
        var messenger = UtilityMethods.GetMessenger();
        string? output = null;
        messenger.Subscribe<GenericTinyMessage<string>>(m => output = m.Content);

        messenger.Publish(new GenericTinyMessage<string>(this, "Testing"));

        Assert.AreEqual("Testing", output);
    }

    [TestMethod]
    public void Publish_SubscriptionThrowingException_DoesNotThrow()
    {
        var messenger = UtilityMethods.GetMessenger();
        messenger.Subscribe<GenericTinyMessage<string>>(_ => throw new NotImplementedException());

        messenger.Publish(new GenericTinyMessage<string>(this, "Testing"));
    }

    [TestMethod]
    public void Publish_SubscriptionThrowingException_DoesThrow()
    {
        var messenger = UtilityMethods.GetMessengerWithSubscriptionErrorHandler();
        messenger.Subscribe<GenericTinyMessage<string>>(_ => throw new NotImplementedException());

        Assert.ThrowsException<NotImplementedException>(
            () => messenger.Publish(new GenericTinyMessage<string>(this, "Testing")));
    }

    //===========================================================================
    // PublishAsync — Task-based. Replaces the old AsyncCallback overload tests:
    // Delegate.BeginInvoke throws PlatformNotSupportedException on modern .NET,
    // so the overload was removed; await the returned Task instead.
    //===========================================================================

    [TestMethod]
    public async Task PublishAsync_DoesNotThrow()
    {
        var messenger = UtilityMethods.GetMessenger();

        await messenger.PublishAsync(new TestMessage(this));
    }

    [TestMethod]
    public async Task PublishAsync_PublishesMessage()
    {
        var messenger = UtilityMethods.GetMessenger();
        bool received = false;
        messenger.Subscribe<TestMessage>(_ => received = true);

        // No horrible wait loop: awaiting the Task IS the completion signal.
        await messenger.PublishAsync(new TestMessage(this));

        Assert.IsTrue(received);
    }

    [TestMethod]
    public async Task PublishAsync_NullMessage_Throws()
    {
        var messenger = UtilityMethods.GetMessenger();

        await Assert.ThrowsExceptionAsync<ArgumentNullException>(
            () => messenger.PublishAsync<TestMessage>(null!));
    }

    [TestMethod]
    public async Task PublishAsync_CancelledToken_Throws()
    {
        var messenger = UtilityMethods.GetMessenger();
        messenger.Subscribe<TestMessage>(UtilityMethods.FakeDeliveryAction);
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        await Assert.ThrowsExceptionAsync<OperationCanceledException>(
            () => messenger.PublishAsync(new TestMessage(this), cts.Token));
    }

    [TestMethod]
    public async Task SubscribeAsync_AsyncSubscriber_ReceivesMessage()
    {
        var messenger = UtilityMethods.GetMessenger();
        bool received = false;
        messenger.SubscribeAsync<TestMessage>(async (_, ct) =>
        {
            await Task.Yield();
            received = true;
        });

        await messenger.PublishAsync(new TestMessage(this));

        Assert.IsTrue(received);
    }

    [TestMethod]
    public async Task SubscribeAsync_WithFilter_OnlyMatchingMessagesDelivered()
    {
        var messenger = UtilityMethods.GetMessenger();
        int deliveries = 0;
        messenger.SubscribeAsync<GenericTinyMessage<string>>(
            (_, _) => { deliveries++; return Task.CompletedTask; },
            m => m.Content == "yes");

        await messenger.PublishAsync(new GenericTinyMessage<string>(this, "yes"));
        await messenger.PublishAsync(new GenericTinyMessage<string>(this, "no"));

        Assert.AreEqual(1, deliveries);
    }

    [TestMethod]
    public async Task SubscribeAsync_ThrowingSubscriber_ErrorHandled()
    {
        var messenger = UtilityMethods.GetMessenger();
        messenger.SubscribeAsync<TestMessage>((_, _) => throw new NotImplementedException());

        // Default error handler swallows subscriber exceptions.
        await messenger.PublishAsync(new TestMessage(this));
    }

    [TestMethod]
    public void Publish_AsyncSubscriber_CompletesInline()
    {
        // Synchronous Publish awaits async subscribers (blocking); verify delivery happens.
        var messenger = UtilityMethods.GetMessenger();
        bool received = false;
        messenger.SubscribeAsync<TestMessage>(async (_, ct) =>
        {
            await Task.Delay(10, ct);
            received = true;
        });

        messenger.Publish(new TestMessage(this));

        Assert.IsTrue(received);
    }

    //===========================================================================
    // Cancellable messages / polymorphic subscriptions.
    //===========================================================================

    [TestMethod]
    public void CancellableGenericTinyMessage_Publish_DoesNotThrow()
    {
        var messenger = UtilityMethods.GetMessenger();

        messenger.Publish(new CancellableGenericTinyMessage<string>(this, "Testing", () => { }));
    }

    [TestMethod]
    public void CancellableGenericTinyMessage_PublishWithNullAction_Throws()
    {
        var messenger = UtilityMethods.GetMessenger();

        Assert.ThrowsException<ArgumentNullException>(
            () => messenger.Publish(new CancellableGenericTinyMessage<string>(this, "Testing", null!)));
    }

    [TestMethod]
    public void CancellableGenericTinyMessage_SubscriberCancels_CancelActioned()
    {
        var messenger = UtilityMethods.GetMessenger();
        bool cancelled = false;
        messenger.Subscribe<CancellableGenericTinyMessage<string>>(m => m.Cancel());

        messenger.Publish(new CancellableGenericTinyMessage<string>(this, "Testing", () => cancelled = true));

        Assert.IsTrue(cancelled);
    }

    [TestMethod]
    public void CancellableGenericTinyMessage_SeveralSubscribersOneCancels_CancelActioned()
    {
        var messenger = UtilityMethods.GetMessenger();
        bool cancelled = false;
        messenger.Subscribe<CancellableGenericTinyMessage<string>>(_ => { });
        messenger.Subscribe<CancellableGenericTinyMessage<string>>(m => m.Cancel());
        messenger.Subscribe<CancellableGenericTinyMessage<string>>(_ => { });

        messenger.Publish(new CancellableGenericTinyMessage<string>(this, "Testing", () => cancelled = true));

        Assert.IsTrue(cancelled);
    }

    [TestMethod]
    public void Publish_SubscriptionOnBaseClass_HitsSubscription()
    {
        var received = false;
        var messenger = UtilityMethods.GetMessenger();
        messenger.Subscribe<TestMessage>(_ => received = true);

        messenger.Publish(new DerivedMessage<string>(this) { Things = "Hello" });

        Assert.IsTrue(received);
    }

    [TestMethod]
    public void Publish_SubscriptionOnImplementedInterface_HitsSubscription()
    {
        var received = false;
        var messenger = UtilityMethods.GetMessenger();
        messenger.Subscribe<ITestMessageInterface>(_ => received = true);

        messenger.Publish(new InterfaceDerivedMessage<string>(this) { Things = "Hello" });

        Assert.IsTrue(received);
    }
}
