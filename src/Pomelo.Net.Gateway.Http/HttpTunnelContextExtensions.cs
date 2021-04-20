using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace Pomelo.Net.Gateway.Http
{
    public static class HttpTunnelContextExtensions
    {
        private static Encoding utf8EncodingWithoutBOM = new UTF8Encoding(false);

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
            int bomLength = 0,
            CancellationToken cancellationToken = default)
        {
            if (encoding == null)
            {
                encoding = utf8EncodingWithoutBOM;
                bomLength = 0;
            }
            var jsonStr = JsonConvert.SerializeObject(obj);
            self.Headers.AddOrUpdate("content-length", (encoding.GetByteCount(jsonStr) + bomLength).ToString());
            self.Headers.AddOrUpdate("content-type", "application/json");
            self.Headers.TryRemove("transfer-encoding");
            self.Headers.TryRemove("content-encoding");
            await self.Headers.WriteToStreamAsync(self.DestinationStream, self.HttpAction, cancellationToken);
            using (var sw = new StreamWriter(self.DestinationStream, encoding, -1, true))
            {
                await sw.WriteAsync(jsonStr);
            }
        }

        public static async ValueTask WriteTextAsync(
            this HttpTunnelContextPart self,
            string text,
            string contentType = "text/plain",
            Encoding encoding = null,
            int bomLength = 0,
            CancellationToken cancellationToken = default)
        {
            if (encoding == null)
            {
                encoding = utf8EncodingWithoutBOM;
                bomLength = 0;
            }
            self.Headers.AddOrUpdate("content-length", (encoding.GetByteCount(text) + bomLength).ToString());
            self.Headers.AddOrUpdate("content-type", contentType);
            self.Headers.TryRemove("transfer-encoding");
            self.Headers.TryRemove("content-encoding");
            await self.Headers.WriteToStreamAsync(self.DestinationStream, self.HttpAction, cancellationToken);
            using (var sw = new StreamWriter(self.DestinationStream, encoding, -1, true))
            {
                await sw.WriteAsync(text);
            }
        }
    }
}
