using System;
using System.Buffers;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using Pomelo.Net.Gateway.Association.Authentication;
using Pomelo.Net.Gateway.Association.Models;

namespace Pomelo.Net.Gateway.Association
{
    public class AssociateClient : IDisposable
    {
        private TcpClient client;
        private IPEndPoint serverEndpoint;
        private IAuthenticator authenticator;
        private int retryDelay = 1000;
        private string serverVersion = "Unknown";
        private long token = 0;
        private List<Interface> tunnels;
        private List<Interface> routers;

        public string ServerVersion => serverVersion;
        public long Token => token;
        public IReadOnlyList<Interface> Tunnels => tunnels;
        public IReadOnlyList<Interface> Routers => routers;

        public AssociateClient(IPEndPoint serverEndpoint, IAuthenticator authenticator)
        {
            this.serverEndpoint = serverEndpoint;
            this.authenticator = authenticator;
            this.tunnels = new List<Interface>();
            this.routers = new List<Interface>();
            this.Reset();
        }

        private bool Reset()
        {
            tunnels.Clear();
            routers.Clear();
            client?.Dispose();
            client = new TcpClient();
            try
            {
                client.Connect(serverEndpoint);
                StartCommunicationAsync(client.GetStream());
            }
            catch (SocketException)
            {
                return false;
            }

            return true;
        }

        public async ValueTask StartCommunicationAsync(NetworkStream stream)
        {
            try
            {
                using (var buffer = MemoryPool<byte>.Shared.Rent(256))
                {
                    // Authentication
                    await authenticator.SendAuthenticatePacketAsync(stream);

                    // Receive Server Info
                    // +--------------+----------------+----------------+----------------+
                    // | Login Result | Server Version | Stream Routers | Stream Tunnels |
                    // +--------------+----------------+----------------+----------------+

                    // 1. Login Result
                    await stream.ReadExAsync(buffer.Memory.Slice(0, 1));
                    switch (buffer.Memory.Span[0])
                    {
                        case 0x00:
                            await stream.ReadExAsync(buffer.Memory.Slice(0, 8));
                            token = BitConverter.ToInt64(buffer.Memory.Slice(0, 8).Span);
                            break;
                        case 0x01:
                            throw new AssociateInvalidCredentialException("Credential is invalid");
                        case 0x02:
                            throw new AssociateClientConflictException("The current credential is using in another place");
                    }

                    // 2. Server Version
                    await stream.ReadExAsync(buffer.Memory.Slice(0, 1));
                    await stream.ReadExAsync(buffer.Memory.Slice(1, buffer.Memory.Span[0]));
                    serverVersion = string.Join(
                        '.', 
                        buffer.Memory.Slice(1, buffer.Memory.Span[0]).Span
                            .ToArray()
                            .Select(x => ((int)x)
                            .ToString()));

                    // 3. Stream Router List
                    await stream.ReadExAsync(buffer.Memory.Slice(0, 1));
                    var count = (int)buffer.Memory.Span[0];
                    for (var i = 0; i < count; ++i)
                    {
                        await stream.ReadExAsync(buffer.Memory.Slice(0, 17));
                        await stream.ReadExAsync(buffer.Memory.Slice(17, buffer.Memory.Span[0]));
                        routers.Add(new Interface 
                        {
                            Id = new Guid(buffer.Memory.Slice(1, 16).Span),
                            Name = Encoding.UTF8.GetString(buffer.Memory.Slice(17, buffer.Memory.Span[0]).Span)
                        });
                    }

                    // 4. Stream Tunnel List
                    await stream.ReadExAsync(buffer.Memory.Slice(0, 1));
                    count = (int)buffer.Memory.Span[0];
                    for (var i = 0; i < count; ++i)
                    {
                        await stream.ReadExAsync(buffer.Memory.Slice(0, 17));
                        await stream.ReadExAsync(buffer.Memory.Slice(17, buffer.Memory.Span[0]));
                        tunnels.Add(new Interface
                        {
                            Id = new Guid(buffer.Memory.Slice(1, 16).Span),
                            Name = Encoding.UTF8.GetString(buffer.Memory.Slice(17, buffer.Memory.Span[0]).Span)
                        });
                    }

                    // Begin Receive Notifications
                    while (true)
                    {

                    }
                }
            }
            catch 
            {
                await Task.Delay(retryDelay);
                retryDelay += 1000;
                if (retryDelay > 10000)
                {
                    retryDelay = 1000;
                }
                Reset();
            }
        }

        public void Dispose()
        {
            client?.Dispose();
            client = null;
        }
    }
}
