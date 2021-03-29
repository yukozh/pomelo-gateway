using System;
using System.Buffers;
using System.Net.Sockets;
using Pomelo.Net.Gateway.Association.Authentication;

namespace Pomelo.Net.Gateway.Association
{
    public class AssociateContext : IDisposable
    {
        private TcpClient client;
        private NetworkStream stream;
        private IMemoryOwner<byte> header, body, response;

        public AssociateContext(TcpClient client)
        {
            this.client = client;
            this.header = MemoryPool<byte>.Shared.Rent(2);
            this.body = MemoryPool<byte>.Shared.Rent(256);
            this.response = MemoryPool<byte>.Shared.Rent(256);
        }

        public TcpClient Client => client;

        public NetworkStream Stream
        {
            get 
            {
                if (stream == null)
                {
                    stream = client.GetStream();
                }
                return stream;
            }
        }

        public Credential Credential { get; set; }
        public Memory<byte> HeaderBuffer => header.Memory.Slice(0, 2);
        public Memory<byte> BodyBuffer => body.Memory.Slice(0, 256);
        public Memory<byte> ResponseBuffer => response.Memory.Slice(0, 256);
        public bool IsAuthenticated => Credential.IsSucceeded;

        public void Dispose()
        {
            header?.Dispose();
            header = null;
            body?.Dispose();
            body = null;
            response?.Dispose();
            response = null;
            client?.Dispose();
            client = null;
        }
    }
}
