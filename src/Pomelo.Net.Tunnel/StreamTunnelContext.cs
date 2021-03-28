using System;
using System.Buffers;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Net.Sockets;

namespace Pomelo.Net.Tunnel
{
    public enum StreamTunnelStatus
    { 
        WaitingForClient,
        Running
    }

    public class StreamTunnelContext : IDisposable
    {
        private Guid connectionId;
        public TcpClient LeftClient { get; set; }
        public TcpClient RightClient { get; set; }

        public StreamTunnelContext()
        {
            this.connectionId = Guid.NewGuid();
        }

        public Guid ConnectionId => connectionId;

        public StreamTunnelStatus Status 
            => LeftClient != null && RightClient != null ? StreamTunnelStatus.Running : StreamTunnelStatus.WaitingForClient;

        public void Dispose()
        {
            throw new NotImplementedException();
        }
    }
}
