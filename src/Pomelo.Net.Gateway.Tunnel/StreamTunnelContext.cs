using System;
using System.Buffers;
using System.Net.Sockets;
using Pomelo.Net.Gateway.Router;

namespace Pomelo.Net.Gateway.Tunnel
{
    public enum StreamTunnelStatus
    { 
        WaitingForClient,
        Running
    }

    public class StreamTunnelContext : IDisposable
    {
        private Guid connectionId;
        private DateTime createdTime;
        private IMemoryOwner<byte> headerBuffer;
        private string userIdentifier;
        private IStreamRouter router;
        private IStreamTunnel tunnel;

        public TcpClient LeftClient { get; set; }
        public TcpClient RightClient { get; set; }
        public DateTime CreatedTimeUtc => createdTime;
        public string UserIdentifier => userIdentifier;
        public IStreamRouter Router => router;
        public IStreamTunnel Tunnel => tunnel;
        
        internal StreamTunnelContext(
            IMemoryOwner<byte> headerBuffer, 
            string userIdentifier,
            IStreamRouter router,
            IStreamTunnel tunnel)
        {
            this.headerBuffer = headerBuffer;
            this.userIdentifier = userIdentifier;
            this.router = router;
            this.tunnel = tunnel;
            this.connectionId = Guid.NewGuid();
            this.createdTime = DateTime.UtcNow;
        }

        public Guid ConnectionId => connectionId;

        public StreamTunnelStatus Status 
            => (LeftClient != null && RightClient != null) 
               ? StreamTunnelStatus.Running 
               : StreamTunnelStatus.WaitingForClient;

        public HeaderStream GetHeaderStream() 
            => new HeaderStream(headerBuffer.Memory);

        public void DestroyHeaderBuffer()
        {
            headerBuffer?.Dispose();
            headerBuffer = null;
        }

        public void Dispose()
        {
            DestroyHeaderBuffer();
            LeftClient?.Dispose();
            LeftClient = null;
            RightClient?.Dispose();
            RightClient = null;
        }
    }
}
