using System;
using System.Net;
using System.Net.Sockets;

namespace Pomelo.Net.Gateway.Association
{
    public class AssociateClient : IDisposable
    {
        private TcpClient client;
        private IPEndPoint serverEndpoint;
        public AssociateClient(IPEndPoint serverEndpoint)
        {
            this.serverEndpoint = serverEndpoint;
            this.Reset();
        }

        private bool Reset()
        {
            client?.Dispose();
            client = new TcpClient();
            try
            {
                client.Connect(serverEndpoint);
            }
            catch (SocketException)
            {
                return false;
            }

            return true;
        }

        private async ValueTask 

        public void Dispose()
        {
            client?.Dispose();
            client = null;
        }
    }
}
