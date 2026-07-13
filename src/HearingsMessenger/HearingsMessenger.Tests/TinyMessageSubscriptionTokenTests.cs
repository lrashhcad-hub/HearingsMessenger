//===============================================================================
// TinyMessenger — see licence.txt.
//===============================================================================

using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using HearingsMessenger.Tests.TestData;

namespace HearingsMessenger.Tests;

[TestClass]
public class TinyMessageSubscriptionTokenTests
{
    [TestMethod]
    public void Dispose_WithValidHubReference_UnregistersWithHub()
    {
        // Modernized: token disposal now calls the non-generic Unsubscribe(token)
        // directly (unsubscription is by token identity) instead of reflecting
        // over Unsubscribe<TMessage>.
        var messengerMock = new Mock<ITinyMessengerHub>();
        messengerMock.Setup(messenger => messenger.Unsubscribe(It.IsAny<TinyMessageSubscriptionToken>())).Verifiable();
        var token = new TinyMessageSubscriptionToken(messengerMock.Object, typeof(TestMessage));

        token.Dispose();

        messengerMock.VerifyAll();
    }

    [TestMethod]
    public void Dispose_WithInvalidHubReference_DoesNotThrow()
    {
        var token = UtilityMethods.GetTokenWithOutOfScopeMessenger();
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();

        token.Dispose();
    }

    [TestMethod]
    public void Ctor_NullHub_ThrowsArgumentNullException()
    {
        Assert.ThrowsException<ArgumentNullException>(
            () => new TinyMessageSubscriptionToken(null!, typeof(ITinyMessage)));
    }

    [TestMethod]
    public void Ctor_InvalidMessageType_ThrowsArgumentOutOfRangeException()
    {
        var messenger = UtilityMethods.GetMessenger();

        Assert.ThrowsException<ArgumentOutOfRangeException>(
            () => new TinyMessageSubscriptionToken(messenger, typeof(object)));
    }

    [TestMethod]
    public void Ctor_ValidHubAndMessageType_DoesNotThrow()
    {
        var messenger = UtilityMethods.GetMessenger();

        _ = new TinyMessageSubscriptionToken(messenger, typeof(TestMessage));
    }
}
