//===============================================================================
// TinyMessenger — see licence.txt.
//===============================================================================

using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using HearingsMessenger.Broadcast;

namespace HearingsMessenger.Tests;

[TestClass]
public class BroadcastPublisherTests
{
    private static BroadcastNotification MakeNotification(string title = "Test") => new()
    {
        Title = title,
        Body = "Body",
        Severity = BroadcastSeverity.Information,
        Sender = "UnitTests",
    };

    private static MachineGroup MakeGroup(params string[] hosts)
        => MachineGroup.FromHostNames("TestGroup", hosts.Length > 0 ? hosts : ["ws-01", "ws-02"]);

    [TestMethod]
    public async Task PublishAsync_SingleTransport_TransportReceivesNotificationAndGroup()
    {
        var loopback = new LoopbackBroadcastTransport();
        var publisher = new BroadcastPublisher([loopback]);
        var notification = MakeNotification();
        var group = MakeGroup("ws-01", "ws-02", "ws-03");

        await publisher.PublishAsync(notification, group);

        Assert.AreEqual(1, loopback.Sent.Count);
        var record = loopback.Sent.Single();
        Assert.AreSame(notification, record.Notification);
        Assert.AreSame(group, record.Group);
        Assert.AreEqual(3, record.Group.Machines.Count);
    }

    [TestMethod]
    public async Task PublishAsync_MultipleTransports_AllTransportsReceiveNotification()
    {
        var loopback1 = new LoopbackBroadcastTransport();
        var loopback2 = new LoopbackBroadcastTransport();
        var publisher = new BroadcastPublisher([loopback1, loopback2]);

        await publisher.PublishAsync(MakeNotification(), MakeGroup());

        Assert.AreEqual(1, loopback1.Sent.Count);
        Assert.AreEqual(1, loopback2.Sent.Count);
    }

    [TestMethod]
    public async Task PublishAsync_TransportThrows_OtherTransportsStillReceive()
    {
        var failing = new Mock<IBroadcastTransport>();
        failing.SetupGet(t => t.Name).Returns("Failing");
        failing
            .Setup(t => t.SendAsync(It.IsAny<BroadcastNotification>(), It.IsAny<MachineGroup>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("boom"));

        var loopback = new LoopbackBroadcastTransport();
        var publisher = new BroadcastPublisher([failing.Object, loopback]);

        // Fire-and-forget contract: the failing transport must not surface or
        // prevent delivery via the healthy transport.
        await publisher.PublishAsync(MakeNotification(), MakeGroup());

        Assert.AreEqual(1, loopback.Sent.Count);
    }

    [TestMethod]
    public async Task PublishAsync_NoTransports_DoesNotThrow()
    {
        var publisher = new BroadcastPublisher([]);

        await publisher.PublishAsync(MakeNotification(), MakeGroup());
    }

    [TestMethod]
    public async Task PublishAsync_WithLocalHub_EchoesBroadcastNotificationMessage()
    {
        var hub = new TinyMessengerHub();
        BroadcastNotificationMessage? echoed = null;
        hub.Subscribe<BroadcastNotificationMessage>(m => echoed = m);

        var loopback = new LoopbackBroadcastTransport();
        var publisher = new BroadcastPublisher([loopback], localHub: hub);
        var notification = MakeNotification("Echo me");
        var group = MakeGroup();

        await publisher.PublishAsync(notification, group);

        Assert.IsNotNull(echoed);
        Assert.AreSame(notification, echoed.Notification);
        Assert.AreSame(group, echoed.Group);
    }

    [TestMethod]
    public async Task PublishAsync_EchoDisabled_DoesNotPublishOnLocalHub()
    {
        var hub = new TinyMessengerHub();
        bool echoed = false;
        hub.Subscribe<BroadcastNotificationMessage>(_ => echoed = true);

        var publisher = new BroadcastPublisher(
            [new LoopbackBroadcastTransport()],
            Microsoft.Extensions.Options.Options.Create(new BroadcastOptions { EchoOnLocalHub = false }),
            localHub: hub);

        await publisher.PublishAsync(MakeNotification(), MakeGroup());

        Assert.IsFalse(echoed);
    }

    [TestMethod]
    public async Task PublishAsync_CancelledToken_Throws()
    {
        var publisher = new BroadcastPublisher([new LoopbackBroadcastTransport()]);
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        await Assert.ThrowsExceptionAsync<OperationCanceledException>(
            () => publisher.PublishAsync(MakeNotification(), MakeGroup(), cts.Token));
    }

    [TestMethod]
    public async Task PublishAsync_NullNotification_Throws()
    {
        var publisher = new BroadcastPublisher([new LoopbackBroadcastTransport()]);

        await Assert.ThrowsExceptionAsync<ArgumentNullException>(
            () => publisher.PublishAsync(null!, MakeGroup()));
    }

    [TestMethod]
    public async Task PublishAsync_NullGroup_Throws()
    {
        var publisher = new BroadcastPublisher([new LoopbackBroadcastTransport()]);

        await Assert.ThrowsExceptionAsync<ArgumentNullException>(
            () => publisher.PublishAsync(MakeNotification(), null!));
    }

    [TestMethod]
    public void MachineGroup_FromHostNames_BuildsTargets()
    {
        var group = MachineGroup.FromHostNames("Appraisers", "ws-01.harriscad.org", "ws-02.harriscad.org");

        Assert.AreEqual("Appraisers", group.Name);
        Assert.AreEqual(2, group.Machines.Count);
        Assert.AreEqual("ws-01.harriscad.org", group.Machines[0].HostName);
        Assert.IsNull(group.Machines[0].Port);
    }
}
