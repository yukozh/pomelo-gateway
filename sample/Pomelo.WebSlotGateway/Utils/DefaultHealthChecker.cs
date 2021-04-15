using System;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

namespace Pomelo.WebSlotGateway.Utils
{
    public class DefaultHealthChecker : IHealthChecker, IDisposable
    {
        private TcpClient client;

        public DefaultHealthChecker()
        {
            client = new TcpClient();
        }

        public void Dispose()
        {
            client?.Dispose();
        }

        public async ValueTask<bool> IsHealthAsync(IPEndPoint destination, CancellationToken cancellationToken = default)
        {
            try
            {
                await client.ConnectAsync(destination.Address, destination.Port, cancellationToken);
                return true;
            }
            catch
            {
                return false;
            }
            finally
            {
                if (client.Connected)
                {
                    client.Client.Disconnect(true);
                }
            }
        }
    }
}
