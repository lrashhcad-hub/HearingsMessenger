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

using System.Runtime.CompilerServices;

namespace HearingsMessenger.Tests.TestData;

public static class UtilityMethods
{
    public static ITinyMessengerHub GetMessenger()
    {
        return new TinyMessengerHub();
    }

    public static ITinyMessengerHub GetMessengerWithSubscriptionErrorHandler()
    {
        return new TinyMessengerHub(new TestSubscriptionErrorHandler());
    }

    public static void FakeDeliveryAction<T>(T message)
        where T : ITinyMessage
    {
    }

    public static bool FakeMessageFilter<T>(T message)
        where T : ITinyMessage
    {
        return true;
    }

    // NoInlining so the hub local genuinely goes out of scope before the
    // caller's GC.Collect — modern JITs can otherwise extend lifetimes.
    [MethodImpl(MethodImplOptions.NoInlining)]
    public static TinyMessageSubscriptionToken GetTokenWithOutOfScopeMessenger()
    {
        var messenger = GetMessenger();

        var token = new TinyMessageSubscriptionToken(messenger, typeof(TestMessage));

        return token;
    }
}
