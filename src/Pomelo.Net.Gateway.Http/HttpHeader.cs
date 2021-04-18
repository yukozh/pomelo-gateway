using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Pomelo.Net.Gateway.Http
{
    public enum HttpAction
    { 
        Request,
        Response
    }

    public class HttpHeader
    {
        private Dictionary<string, string> fields = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        public HttpAction Type { get; set; }
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

        public HttpHeader(Stream stream, HttpAction type)
        {
            ParseHeaderAsync(stream, type).GetAwaiter().GetResult();
        }

        public bool Contains(string key) => fields.ContainsKey(key);

        public async ValueTask<bool> ParseHeaderAsync(Stream stream, HttpAction type)
        {
            var firstLine = true;
            while (true)
            {
                var line = await stream.ReadLineExAsync();
                if (string.IsNullOrEmpty(line))
                {
                    break;
                }

                if (firstLine)
                {
                    firstLine = false;
                    var index1 = line.IndexOf(' ');
                    if (index1 == -1)
                    {
                        return false;
                    }
                    if (type == HttpAction.Request)
                    {
                        Method = line.Substring(0, index1).ToUpper();
                    }
                    else
                    {
                        Protocol = line.Substring(0, index1).ToUpper();
                    }
                    var index2 = line.IndexOf(' ', index1 + 1);
                    if (index1 == index2)
                    {
                        if (index1 == -1)
                        {
                            return false;
                        }
                    }
                    if (type == HttpAction.Request)
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
                    return false;
                }
                var key = line.Substring(0, index3);
                var value = line.Substring(index3 + 1);
                fields.Add(key, value.TrimStart());
            }
            if (type == HttpAction.Request && (Method == null || Url == null || Protocol == null))
            {
                return false;
            }
            else if (type == HttpAction.Response && (Protocol == null || StatusCode == default || StatusCodeString == null))
            {
                return false;
            }
            return true;
        }

        public async ValueTask WriteToStreamAsync(
            Stream stream, 
            HttpAction type,
            CancellationToken cancellationToken = default)
        {
            using (var sw = new StreamWriter(stream, Encoding.ASCII, -1, true))
            {
                if (type == HttpAction.Request)
                {
                    await sw.WriteLineAsync(new StringBuilder($"{Method} {Url} {Protocol}"), cancellationToken);
                }
                else
                {
                    await sw.WriteLineAsync(new StringBuilder($"{Protocol} {StatusCode} {StatusCodeString}"), cancellationToken);
                }
                foreach (var field in fields)
                {
                    await sw.WriteLineAsync(new StringBuilder($"{field.Key}: {field.Value}"), cancellationToken);
                }
                await sw.WriteLineAsync(new StringBuilder(""), cancellationToken);
                await sw.FlushAsync();
            }
        }

        public int WriteToMemory(HttpAction type, Memory<byte> buffer)
        {
            var count = 0;

            if (type == HttpAction.Request)
            {
                count += Encoding.ASCII.GetBytes($"{Method} {Url} {Protocol}\r\n", buffer.Slice(count).Span);
            } 
            else
            {
                count += Encoding.ASCII.GetBytes($"{Protocol} {StatusCode} {StatusCodeString}\r\n", buffer.Slice(count).Span);
            }
            foreach (var field in fields)
            {
                count += Encoding.ASCII.GetBytes($"{field.Key}: {field.Value}\r\n", buffer.Slice(count).Span);
            }
            count += Encoding.ASCII.GetBytes("\r\n", buffer.Slice(count).Span);
            return count;
        }
    }
}
