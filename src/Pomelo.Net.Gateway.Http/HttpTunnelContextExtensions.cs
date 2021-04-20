using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace Pomelo.Net.Gateway.Http
{
    public static class HttpTunnelContextExtensions
    {
        public static async ValueTask<string> ReadAsStringAsync(
            this HttpTunnelContextPart self,
            Encoding encoding = null,
            CancellationToken cancellationToken = default)
        {
            if (encoding == null)
            {
                encoding = Encoding.UTF8;
            }
            if (self.Body == null)
            {
                return null;
            }
            using (var sr = new StreamReader(self.Body, encoding, true, -1, true))
            {
                return await sr.ReadToEndAsync();
            }
        }

        public static async ValueTask<T> ReadAsJsonObjectAsync<T>(
            this HttpTunnelContextPart self,
            Encoding encoding = null,
            CancellationToken cancellationToken = default)
        {
            if (encoding == null)
            {
                encoding = Encoding.UTF8;
            }
            if (self.Body == null)
            {
                return default;
            }
            using (var sr = new StreamReader(self.Body, encoding, true, -1, true))
            {
                var jsonStr = await sr.ReadToEndAsync();
                return JsonConvert.DeserializeObject<T>(jsonStr);
            }
        }

        public static async ValueTask WriteJsonAsync(
            this HttpTunnelContextPart self,
            object obj,
            Encoding encoding = null,
            CancellationToken cancellationToken = default)
        {
            if (encoding == null)
            {
                encoding = Encoding.UTF8;
            }
            var jsonStr = JsonConvert.SerializeObject(obj);
            var bytes = encoding.GetBytes(jsonStr);
            self.Headers.AddOrUpdate("content-length", bytes.Length.ToString());
            self.Headers.AddOrUpdate("content-type", "application/json");
            self.Headers.TryRemove("transfer-encoding");
            self.Headers.TryRemove("content-encoding");
            await self.Headers.WriteToStreamAsync(self.DestinationStream, self.HttpAction, cancellationToken);
            await self.DestinationStream.WriteAsync(bytes, 0, bytes.Length, cancellationToken);
        }
    }
}
