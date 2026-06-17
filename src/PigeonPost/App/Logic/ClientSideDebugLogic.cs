using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using PigeonPost.Bridge;
using PigeonPost.Tun;
using PigeonPost.Tun.Virtual;
using Scriba;

namespace PigeonPost;

public sealed class ClientSideDebugLogic : ClientSideLogic
{
    private const int MessagesPerClient = 100;
    private static readonly TimeSpan PeriodBetweenMessages = TimeSpan.FromMilliseconds(10);

    private readonly Queue<byte[]> _pending;
    private readonly VirtualNetwork _network;

    public ClientSideDebugLogic(
        ITunDevice tun,
        ClientId clientId,
        IPv4 clientIp,
        IPv4 serverIp,
        string clientUrl,
        ILogger logger,
        CancellationToken externalCt,
        Pontifex.ITransportFactory transportFactory,
        int bufferSizeBytes,
        bool verbose,
        Queue<byte[]> pending,
        VirtualNetwork network)
        : base(tun, clientId, clientIp, serverIp, clientUrl, logger, externalCt,
               transportFactory, bufferSizeBytes, verbose)
    {
        _pending = pending;
        _network = network;
    }

    public override async Task Start()
    {
        await base.Start();

        try
        {
            for (int seq = 0; seq < MessagesPerClient; seq++)
            {
                await Task.Delay(PeriodBetweenMessages, _externalCt);

                int size = Random.Shared.Next(1, 1025);
                byte[] msg = new byte[size];
                Random.Shared.NextBytes(msg);

                lock (_pending)
                    _pending.Enqueue(msg);

                _network.SendFromTo(_clientIp, _serverIp, msg);
            }

            while (true)
            {
                if (_externalCt.IsCancellationRequested)
                    return;

                lock (_pending)
                {
                    if (_pending.Count == 0)
                        break;
                }

                await Task.Delay(10, _externalCt);
            }

            _logger.i($"Client {_clientId} completed: all {MessagesPerClient} messages sent and verified.");
        }
        catch (OperationCanceledException)
        {
        }
        finally
        {
            Stop();
        }
    }
}
