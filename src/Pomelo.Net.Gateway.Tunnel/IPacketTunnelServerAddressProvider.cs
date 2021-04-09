using System.Net;

namespace Pomelo.Net.Gateway.Tunnel
{
    public interface IPacketTunnelServerAddressProvider
    {
        public IPEndPoint PacketTunnelServerEndpoint { get; }
    }
}
