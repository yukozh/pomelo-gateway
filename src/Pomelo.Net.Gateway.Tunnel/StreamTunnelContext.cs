using System;
using System.Buffers;
using System.Net.Sockets;

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

        public TcpClient LeftClient { get; set; }
        public TcpClient RightClient { get; set; }
        public DateTime CreatedTimeUtc => createdTime;
        
        internal StreamTunnelContext(IMemoryOwner<byte> headerBuffer)
        {
            this.headerBuffer = headerBuffer;
            this.connectionId = Guid.NewGuid();
            this.createdTime = DateTime.UtcNow;
        }

        public Guid ConnectionId => connectionId;

        public StreamTunnelStatus Status 
            => (LeftClient != null && RightClient != null) ? StreamTunnelStatus.Running : StreamTunnelStatus.WaitingForClient;

        public void Dispose()
        {
            headerBuffer.Dispose();
            LeftClient?.Dispose();
            RightClient?.Dispose();
        }
    }
}
