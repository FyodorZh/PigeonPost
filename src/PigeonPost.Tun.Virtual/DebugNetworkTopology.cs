using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using PigeonPost.Tun;

namespace PigeonPost.Tun.Virtual;

public class DebugNetworkTopology
{
    private readonly CancellationTokenSource _cts;
    private readonly Task _networkTask;
    private readonly Task _completionTask;

    private static readonly IPv4 ServerIp = new IPv4(192, 168, 0, 1);
    private static readonly IPv4 ClientBaseIp = new IPv4(192, 168, 0, 2);

    public DebugNetworkTopology(
        VirtualNetwork network,
        int clientCount,
        int messagesPerClient,
        TimeSpan periodBetweenMessages)
    {
        _cts = new CancellationTokenSource();
        var tcs = new TaskCompletionSource();

        int totalExpected = clientCount * messagesPerClient;
        int totalVerified = 0;

        network.CreateNode("server", ServerIp, (fromIp, toIp, packet) =>
        {
            byte[] response = new byte[packet.Length];
            for (int i = 0; i < packet.Length; i++)
                response[i] = packet[packet.Length - 1 - i];
            network.SendFromTo(ServerIp, fromIp, response);
        });

        for (int i = 0; i < clientCount; i++)
        {
            uint ipVal = (uint)(ClientBaseIp.Value + (uint)i);
            var clientIp = new IPv4(ipVal);
            string name = $"client{i}";
            var pending = new Queue<byte[]>();

            network.CreateNode(name, clientIp, (fromIp, toIp, packet) =>
            {
                byte[] expected;
                lock (pending)
                {
                    expected = pending.Dequeue();
                }

                for (int j = 0; j < expected.Length; j++)
                {
                    if (packet[j] != expected[expected.Length - 1 - j])
                        throw new Exception($"Client {name}: response mismatch at byte {j}");
                }

                if (Interlocked.Increment(ref totalVerified) == totalExpected)
                    tcs.TrySetResult();
            });

            int idx = i;
            _ = Task.Run(async () =>
            {
                try
                {
                    for (int seq = 0; seq < messagesPerClient; seq++)
                    {
                        await Task.Delay(periodBetweenMessages, _cts.Token);

                        int size = Random.Shared.Next(1, 1025);
                        byte[] msg = new byte[size];
                        Random.Shared.NextBytes(msg);

                        lock (pending)
                            pending.Enqueue(msg);

                        network.SendFromTo(clientIp, ServerIp, msg);
                    }
                }
                catch (OperationCanceledException)
                {
                }
            });
        }

        _networkTask = network.RunAsync(_cts.Token);
        _completionTask = tcs.Task;
    }

    public Task WaitForCompletionAsync() => _completionTask;

    public void Stop()
    {
        _cts.Cancel();
    }
}
