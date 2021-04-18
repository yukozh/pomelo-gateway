using System;
using System.Buffers;
using System.IO;
using System.IO.Compression;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace Pomelo.Net.Gateway.Http
{
    public class HttpTunnelContext
    {
        public Guid ConnectionId { get; set; }

        public HttpTunnelContextPart Request { get; private set; } = new HttpTunnelContextPart { HttpAction = HttpAction.Request };

        public HttpTunnelContextPart Response { get; private set; } = new HttpTunnelContextPart { HttpAction = HttpAction.Response };
    }

    public class HttpTunnelContextPart
    { 
        public Stream SourceStream { get; set; }

        public Stream DestinationStream { get; set; }

        public HttpHeader Headers { get; set; }

        public HttpAction HttpAction { get; internal set; }
    }

    public static class HttpTunnelContextPartExtensions
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
            if (self.Headers.ContentLength == -1)
            {
                using (var sr = new StreamReader(self.SourceStream, encoding))
                {
                    var prevIsEmpty = false;
                    var sb = new StringBuilder();
                    try
                    {
                        while (true)
                        {
                            var line = await sr.ReadLineAsync();
                            if (!string.IsNullOrEmpty(line))
                            {
                                prevIsEmpty = false;
                            }
                            else
                            {
                                if (prevIsEmpty)
                                {
                                    return sb.ToString();
                                }
                                else
                                {
                                    prevIsEmpty = true;
                                }
                            }
                        }
                    }
                    catch 
                    {
                        return sb.ToString();
                    }
                }
            }
            else
            {
                using (var buffer = MemoryPool<byte>.Shared.Rent(self.Headers.ContentLength))
                {
                    var _buffer = buffer.Memory.Slice(0, self.Headers.ContentLength);
                    await self.SourceStream.ReadExAsync(
                        _buffer,
                        cancellationToken);
                    return encoding.GetString(_buffer.Span);
                }
            }
        }

        public static async ValueTask WriteJsonAsync(
            this HttpTunnelContextPart self,
            object obj,
            CancellationToken cancellationToken = default)
        {
            var jsonStr = JsonConvert.SerializeObject(obj);
            var bytes = Encoding.UTF8.GetBytes(jsonStr);
            self.Headers.AddOrUpdate("content-length", bytes.Length.ToString());
            self.Headers.AddOrUpdate("content-type", "application/json");
            self.Headers.TryRemove("transfer-encoding");
            self.Headers.TryRemove("content-encoding");
            await self.Headers.WriteToStreamAsync(self.DestinationStream, self.HttpAction, cancellationToken);
            await self.DestinationStream.WriteAsync(bytes, 0, bytes.Length);
        }
    }
}
