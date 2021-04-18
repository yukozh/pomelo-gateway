using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading.Tasks;

namespace Pomelo.Net.Gateway.Http
{
    public enum HttpHeaderType
    { 
        Request,
        Response
    }

    public class HttpHeader
    {
        private Dictionary<string, string> fields = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        public HttpHeaderType Type { get; set; }
        public string Method { get; set; }
        public string Url { get; set; }
        public string Protocol { get; set; }
        public int StatusCode { get; set; }
        public string StatusCodeString { get; set; }
        public Dictionary<string, string> HeaderCollection => fields;
        public string Host => GetHeaderField("host");
        public int ContentLength => Convert.ToInt32(GetHeaderField("content-length") ?? "-1");
        public string UserAgent => GetHeaderField("user-agent");
        public string ContentType => GetHeaderField("content-type");
        public string Accept => GetHeaderField("accept");
        public string AcceptEncoding => GetHeaderField("accept-encoding");
        public string AcceptLanguage => GetHeaderField("accept-language");
        public string Referer => GetHeaderField("referer");
        public string Origin => GetHeaderField("origin");
        public string Authorization => GetHeaderField("authorization");

        private string GetHeaderField(string key) => fields.ContainsKey(key) ? fields[key] : null;

        public HttpHeader()
        {
        }

        public HttpHeader(Stream stream, HttpHeaderType type)
        {
            ParseHeaderAsync(stream, type).GetAwaiter().GetResult();
        }

        public async ValueTask ParseHeaderAsync(Stream stream, HttpHeaderType type)
        {
            var firstLine = true;
            var sr = new StreamReader(stream);
            while (true)
            {
                var line = await sr.ReadLineAsync();
                if (line == null)
                {
                    break;
                }

                if (firstLine)
                {
                    firstLine = false;
                    var index1 = line.IndexOf(' ');
                    if (index1 == -1)
                    {
                        throw new InvalidDataException("Invalid first line of stream, maybe it is not an HTTP stream.");
                    }
                    if (type == HttpHeaderType.Request)
                    {
                        Method = line.Substring(0, index1).ToUpper();
                    }
                    else
                    {
                        Protocol = line.Substring(0, index1).ToUpper();
                    }
                    var index2 = line.LastIndexOf(' ');
                    if (index1 == index2)
                    {
                        if (index1 == -1)
                        {
                            throw new InvalidDataException("Invalid first line of stream, maybe it is not an HTTP stream.");
                        }
                    }
                    if (type == HttpHeaderType.Request)
                    {
                        Url = line.Substring(index1, index2 - index1).Trim();
                        Protocol = line.Substring(index2).Trim();
                    }
                    else
                    {
                        StatusCode = Convert.ToInt32(line.Substring(index1, index2 - index1).Trim());
                        StatusCodeString = line.Substring(index2).Trim();
                    }
                    continue;
                }

                if (string.IsNullOrEmpty(line))
                {
                    break;
                }
                var index3 = line.IndexOf(':');
                if (index3 == -1)
                {
                    throw new InvalidDataException("Invalid first line of stream, maybe it is not an HTTP stream.");
                }
                var key = line.Substring(0, index3);
                var value = line.Substring(index3 + 1);
                fields.Add(key, value.TrimStart());
            }
            if (Method == null || Url == null || Protocol == null)
            {
                throw new InvalidDataException("Invalid first line of stream, maybe it is not an HTTP stream.");
            }
        }

        public async ValueTask WriteToStream(Stream stream)
        {
            var sw = new StreamWriter(stream);
            await sw.WriteLineAsync($"{Method} {Url} {Protocol}");
            foreach (var field in fields)
            {
                await sw.WriteLineAsync($"{field.Key}: {field.Value}");
            }
        }

        public int WriteToMemory(Memory<byte> buffer)
        {
            var count = 0;
            count += Encoding.ASCII.GetBytes($"{Method} {Url} {Protocol}\r\n", buffer.Slice(count).Span);
            foreach (var field in fields)
            {
                count += Encoding.ASCII.GetBytes($"{field.Key}: {field.Value}\r\n", buffer.Slice(count).Span);
            }
            count += Encoding.ASCII.GetBytes("\r\n", buffer.Slice(count).Span);
            return count;
        }
    }
}
