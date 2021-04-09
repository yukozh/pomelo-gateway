using System.Net;

namespace Pomelo.Net.Gateway.Tunnel
{
    public interface IUdpServerProvider
    {
        public PomeloUdpClient FindServerByEndpoint(IPEndPoint endpoint);
    }
}
