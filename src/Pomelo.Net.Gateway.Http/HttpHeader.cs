using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
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
        private bool isWroteToStream = false;
        private Dictionary<string, List<string>> fields = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
        public HttpAction Type { get; set; }
        public string Method { get; set; }
        public string Url { get; set; }
        public string Protocol { get; set; }
        public int StatusCode { get; set; }
        public string StatusCodeString { get; set; }
        public Dictionary<string, List<string>> HeaderCollection => fields;
        public string Host => GetHeaderField("host");
        public int ContentLength => Convert.ToInt32(GetHeaderField("content-length") ?? "-1");
        public IEnumerable<string> TransferEncoding => GetHeaderFields("transfer-encoding");
        public string UserAgent => GetHeaderField("user-agent");
        public string ContentType => GetHeaderField("content-type");
        public string ContentEncoding => GetHeaderField("content-encoding");
        public string Connection => GetHeaderField("connection");
        public string KeepAlive => GetHeaderField("keep-alive");
        public string Accept => GetHeaderField("accept");
        public string AcceptEncoding => GetHeaderField("accept-encoding");
        public string AcceptLanguage => GetHeaderField("accept-language");
        public string Referer => GetHeaderField("referer");
        public string Origin => GetHeaderField("origin");
        public string Authorization => GetHeaderField("authorization");
        public string Upgrade => GetHeaderField("upgrade");
        public bool IsWroteToStream => isWroteToStream;

        public string Path
        {
            get
            {
                if (Url == null)
                {
                    return null;
                }

                var index = Url.IndexOf('?');
                if (index < 0)
                {
                    return Url;
                }
                return Url.Substring(0, Url.IndexOf('?'));
            }
        }

        private UrlEncodedValueCollection query;

        public UrlEncodedValueCollection Query
        {
            get
            {
                if (Url == null)
                {
                    return null;
                }

                if (query == null)
                {
                    var index = Url.IndexOf('?');
                    if (index >= 0)
                    {
                        query = new UrlEncodedValueCollection(Url.Substring(Url.IndexOf('?') + 1));
                    }
                    else
                    {
                        query = UrlEncodedValueCollection.Empty;
                    }
                }

                return query;
            }
        }

        public bool IsKeepAlive
        {
            get
            {
                if (Connection == null 
                    && Protocol.ToLower() == "http/1.0")
                {
                    return false;
                }

                if (Connection != null 
                    && Connection.ToLower() != "keep-alive")
                {
                    return false;
                }

                return true;
            }
        }

        private string GetHeaderField(string key) => fields.ContainsKey(key) ? fields[key].First() : null;
        private IEnumerable<string> GetHeaderFields(string key)
        {
            if (!Contains(key) || fields[key].Count == 0)
            {
                return null;
            }

            if (fields[key].Count == 1)
            {
                return fields[key]
                    .Single()
                    .Split(',')
                    .Select(x => x.Trim());
            }

            return fields[key];
        }

        public HttpHeader()
        {
        }

        public HttpHeader(Stream stream, HttpAction type)
        {
            ParseHeaderAsync(stream, type).GetAwaiter().GetResult();
        }

        public bool Contains(string key) => fields.ContainsKey(key);

        public bool TryAdd(string key, string value)
        {
            if (Contains(key))
            {
                return false;
            }
            HeaderCollection.Add(key, new List<string>() { value });
            return true;
        }

        public void AddOrUpdate(string key, string value)
        { 
            if (Contains(key))
            {
                if (HeaderCollection[key].Count == 0)
                {
                    HeaderCollection[key].Add(value);
                }
                else
                {
                    HeaderCollection[key][0] = value;
                }
            }
            else
            {
                HeaderCollection.Add(key, new List<string> { value });
            }
        }

        public bool TryRemove(string key)
        {
            if (Contains(key))
            {
                HeaderCollection.Remove(key);
                return true;
            }
            return false;
        }

        public async ValueTask<bool> ParseHeaderAsync(Stream stream, HttpAction type)
        {
            isWroteToStream = false;
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
                    fields.Clear();
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
                if (!fields.ContainsKey(key))
                {
                    fields.Add(key, new List<string>());
                }
                fields[key].Add(value.TrimStart());
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
            if (isWroteToStream)
            {
                return;
            }
            isWroteToStream = true;
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
                    foreach (var val in field.Value)
                    {
                        if (string.IsNullOrWhiteSpace(val))
                        {
                            continue;
                        }
                        await sw.WriteAsync(new StringBuilder($"{field.Key}: {val}\r\n"), cancellationToken);
                    }
                }
                await sw.WriteAsync(new StringBuilder("\r\n"), cancellationToken);
                await sw.FlushAsync();
            }
        }

        public int CopyToMemory(HttpAction type, Memory<byte> buffer)
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
                foreach (var val in field.Value)
                {
                    if (string.IsNullOrWhiteSpace(val))
                    {
                        continue;
                    }
                    count += Encoding.ASCII.GetBytes($"{field.Key}: {val}\r\n", buffer.Slice(count).Span);
                }
            }
            count += Encoding.ASCII.GetBytes("\r\n", buffer.Slice(count).Span);
            return count;
        }
    }
}
