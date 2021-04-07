using System.Net;

namespace Pomelo.Net.Gateway.Association.Udp
{
    public interface IUdpAssociator
    {
        void SetAgentUdpEndpoint(string identifier, IPEndPoint endpoint);
        IPEndPoint FindEndpointByIdentifier(string identifier);
    }
}
