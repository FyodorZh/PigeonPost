using System.Net.Sockets;
using System.Text;
using Actuarius.Memory;
using Pontifex;
using Pontifex.Abstractions.Acknowledgers;
using Pontifex.Abstractions.Endpoints.Client;
using Pontifex.Abstractions.Endpoints.Server;
using Pontifex.Abstractions.Handlers.Client;
using Pontifex.Abstractions.Handlers.Server;
using Pontifex.Utils;
using Scriba;
using Scriba.Consumers;

namespace PigeonPost
{
    internal class Program
    {
        public class ServerSideHandler : IAckRawServerHandler
        {
            private IAckRawClientEndpoint? _endpoint;
            
            public void OnDisconnected(StopReason reason)
            {
                // Handle disconnection logic 
            }

            public void OnReceived(UnionDataList receivedBuffer)
            {
                Log.i("Server received from client: " + receivedBuffer.ToString());
                
                var dataToSend = new UnionDataList();
                dataToSend.PutFirst(new UnionData(new StaticReadOnlyByteArray("Hello from server!"u8.ToArray())));
                _endpoint!.Send(dataToSend);
            }

            public void GetAckResponse(UnionDataList ackData)
            {
                ackData.PutFirst(new UnionData((long)888)); // optional server response
            }

            public void OnConnected(IAckRawClientEndpoint endPoint)
            {
                _endpoint = endPoint;
            }
        }

        public class ServerSideClientAcceptor : IRawServerAcknowledger<ServerSideHandler>
        {
            public ServerSideHandler? TryAck(UnionDataList ackData)
            {
                if (ackData.TryPopFirst(out long token) && token == 777) // secret request
                {
                    return new ServerSideHandler();
                }
                return null; // reject
            }
        }

        public class ClientHandler : IAckRawClientHandler
        {
            public IAckRawServerEndpoint? Endpoint { get; private set; }
            
            public void OnDisconnected(StopReason reason)
            {
                // Handle disconnection logic
            }

            public void OnReceived(UnionDataList receivedBuffer)
            {
                Log.i("Client received from server: " + receivedBuffer.ToString());
            }

            public void WriteAckData(UnionDataList ackData)
            {
                ackData.PutFirst(new UnionData((long)777)); // secret request to server
            }

            public void OnConnected(IAckRawServerEndpoint endPoint, UnionDataList ackResponse)
            {
                // Handle connection logic
                Endpoint = endPoint;
                // we can use ackResponse from server
            }

            public void OnStopped(StopReason reason)
            {
                // Handle disconnection logic
            }
        }
        
        static void Main(string[] args)
        {
            StaticLogger.Instance.AddConsumer(new ConsoleConsumer());
            
            Pontifex.Transports.Direct.AckRawDirectServer server = new Pontifex.Transports.Direct.AckRawDirectServer("server_name", StaticLogger.Instance, MemoryRental.Shared);
            server.Init(new ServerSideClientAcceptor());
            server.Start(reason =>
            {
                Log.i("Server stopped: " + reason);
            });
            
            Pontifex.Transports.Direct.AckRawDirectClient client = new Pontifex.Transports.Direct.AckRawDirectClient("server_name", StaticLogger.Instance, MemoryRental.Shared);
            
            ClientHandler clientHandler = new ClientHandler();
            client.Init(clientHandler);
            client.Start(reason =>
            {
                Log.i("Client stopped: " + reason);
            });

            var ep = clientHandler.Endpoint;
            if (ep != null)
            {
                var dataToSend = new UnionDataList();
                dataToSend.PutFirst(new UnionData(new StaticReadOnlyByteArray("Hello from client!"u8.ToArray())));
                ep.Send(dataToSend);
            }

            client.Stop();
            // server.Stop(); direct server doesn't need to be stopped
            
            
            // Expected output:
            // INFO: "Server received from client: [Array:[72,101,108,108,111,32,102,114,111,109,32,99,108,105,101,110,116,33]]"
            // INFO: "Client received from server: [Array:[72,101,108,108,111,32,102,114,111,109,32,115,101,114,118,101,114,33]]"
            // INFO: "Client stopped: {"Source": "direct", "Type": "Unknown"}"
        }
    }
}
