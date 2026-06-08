using System;
using System.Collections.Generic;
using Pontifex;
using Pontifex.Abstractions;
using Pontifex.Abstractions.Endpoints;
using Pontifex.Utils;

namespace PigeonPost.Bridge.Tests;

internal class FakeEndpoint : IAckRawBaseEndpoint
{
    public IEndPoint? RemoteEndPoint => null;
    public bool IsConnected => true;
    public int MessageMaxByteSize => 1_048_576;
    public List<UnionDataList> SentMessages { get; } = new();

    public SendResult Send(UnionDataList bufferToSend)
    {
        SentMessages.Add(bufferToSend);
        return SendResult.Ok;
    }

    public bool Disconnect(StopReason reason) => true;
    public void GetControls(List<IControl> dst, Predicate<IControl>? predicate = null) { }
}
