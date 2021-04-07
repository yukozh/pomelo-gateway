using System;
using System.Threading;
using System.Threading.Tasks;
using System.Net;
using System.Net.Sockets;

namespace Pomelo.Net
{
    public struct ReceiveResult
    {
        public int ReceivedBytes;
        public IPEndPoint RemoteEndPoint;
    }

    public class PomeloUdpClient : IDisposable
    {
        public const int MaxUDPSize = 0x10000;

        public Socket Client { get; private set; }

        private static IPEndPoint AllEndpoint = new IPEndPoint(IPAddress.Any, 0);

        public PomeloUdpClient()
        {
            Client = new Socket(SocketType.Dgram, ProtocolType.Udp);
        }

        public PomeloUdpClient(int port) : this()
        {
            Bind(new IPEndPoint(IPAddress.Any, port));
        }

        public PomeloUdpClient(IPEndPoint endpoint)
        {
            Bind(endpoint);
        }

        public void Bind(EndPoint localEP)
        {
            Client.Bind(localEP);
        }

        public async ValueTask<ReceiveResult> ReceiveAsync(ArraySegment<byte> buffer)
        {
            var result = await Client.ReceiveFromAsync(buffer, SocketFlags.None, AllEndpoint).ConfigureAwait(false);
            if (result.RemoteEndPoint.AddressFamily == AddressFamily.InterNetworkV6)
            {
                return new ReceiveResult
                {
                    ReceivedBytes = result.ReceivedBytes,
                    RemoteEndPoint = new IPEndPoint(((IPEndPoint)result.RemoteEndPoint).Address.MapToIPv4(), ((IPEndPoint)result.RemoteEndPoint).Port)
                };
            }
            else
            {
                return new ReceiveResult
                {
                    ReceivedBytes = result.ReceivedBytes,
                    RemoteEndPoint = (IPEndPoint)result.RemoteEndPoint
                };
            }
        }

        public async ValueTask<int> SendAsync(ArraySegment<byte> buffer, IPEndPoint endpoint)
        {
            return await Client.SendToAsync(buffer, SocketFlags.None, endpoint).ConfigureAwait(false);
        }

        public async ValueTask<int> SendAsync(ArraySegment<byte> buffer, string endpoint)
        {
            return await SendAsync(buffer, await AddressHelper.ParseAddressAsync(endpoint, 0));
        }

        public void Dispose()
        {
            Client?.Dispose();
        }
    }
}