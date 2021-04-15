using System;
using System.Buffers;
using System.Net;
using System.Net.Sockets;
using Pomelo.Net.Gateway.Router;
using Newtonsoft.Json;

namespace Pomelo.Net.Gateway.Tunnel
{
    public enum StreamTunnelStatus
    { 
        WaitingForClient,
        Connected
    }

    public class StreamTunnelContext : IDisposable
    {
        private Guid connectionId;
        private DateTime createdTime;
        private IMemoryOwner<byte> headerBuffer;
        private string userIdentifier;
        private IStreamRouter router;
        private IStreamTunnel tunnel;

        [JsonIgnore]
        public TcpClient LeftClient { get; set; }

        [JsonIgnore]
        public TcpClient RightClient { get; set; }

        public IPEndPoint? LeftEndpoint => (IPEndPoint?)LeftClient?.Client?.RemoteEndPoint;

        public IPEndPoint? RightEndpoint => (IPEndPoint?)RightClient?.Client?.RemoteEndPoint;

        public DateTime LastCommunicationTimeUtc { get; set; } = DateTime.UtcNow;
        public DateTime CreatedTimeUtc => createdTime;
        public string UserIdentifier => userIdentifier;
        public IStreamRouter Router => router;
        public IStreamTunnel Tunnel => tunnel;
        
        internal StreamTunnelContext(
            Guid connectionId,
            IMemoryOwner<byte> headerBuffer,
            string userIdentifier,
            IStreamRouter router,
            IStreamTunnel tunnel)
        {
            this.headerBuffer = headerBuffer;
            this.userIdentifier = userIdentifier;
            this.router = router;
            this.tunnel = tunnel;
            this.connectionId = connectionId;
            this.createdTime = DateTime.UtcNow;
        }

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
        {
            get 
            {
                return LeftClient != null && LeftClient.Connected && RightClient != null && RightClient.Connected 
                    ? StreamTunnelStatus.Connected 
                    : StreamTunnelStatus.WaitingForClient;
            }
        }

        public HeaderStream GetHeaderStream() 
            => headerBuffer == null ? null : new HeaderStream(headerBuffer.Memory);

        public void DestroyHeaderBuffer()
        {
            headerBuffer?.Dispose();
            headerBuffer = null;
        }

        public void Dispose()
        {
            DestroyHeaderBuffer();
            LeftClient?.Close();
            LeftClient?.Dispose();
            LeftClient = null;
            RightClient?.Close();
            RightClient?.Dispose();
            RightClient = null;
        }
    }
}
