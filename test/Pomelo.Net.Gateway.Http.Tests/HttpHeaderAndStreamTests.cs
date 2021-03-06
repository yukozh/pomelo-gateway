using System.IO;
using System.Text;
using System.Threading.Tasks;
using Xunit;

namespace Pomelo.Net.Gateway.Http.Tests
{
    public class HttpHeaderAndStreamTests
    {
        [Fact]
        public async Task ChunkedBodyTest()
        {
            // Arrange
            var testData = "6\r\nDotNet\r\n9\r\nDeveloper\r\n0\r\n\r\nHTTP/1.1 200 OK\r\nContent-Type: text/plain\r\nTransfer-Encoding: chunked\r\n\r\n6\r\nPomelo\r\na\r\nFoundation\r\n0\r\n\r\nHTTP/1.1 200 OK\r\nContent-Type: text/plain\r\nContent-Length: 11\r\n\r\nHello World";
            var sourceStream = new MemoryStream(Encoding.ASCII.GetBytes(testData));

            // Act 1
            var stream1 = new HttpBodyStream(null, sourceStream, null);
            var sr1 = new StreamReader(stream1);
            var result1 = sr1.ReadToEnd();

            // Assert 1
            Assert.Equal("DotNetDeveloper", result1);

            // Act 2
            var header = new HttpHeader();
            await header.ParseHeaderAsync(sourceStream, HttpAction.Response);
            var stream2 = new HttpBodyStream(null, sourceStream, null);
            var sr2 = new StreamReader(stream2);
            var result2 = sr2.ReadToEnd();

            // Assert 2
            Assert.Equal("PomeloFoundation", result2);

            // Act 3
            var header2 = new HttpHeader();
            await header2.ParseHeaderAsync(sourceStream, HttpAction.Response);
            var stream3 = new HttpBodyStream(null, sourceStream, null, header2.ContentLength);
            var sr3 = new StreamReader(stream3);
            var result3 = sr3.ReadToEnd();

            // Assert 3
            Assert.Equal("Hello World", result3);
        }

        [Fact]
        public async Task NonKeepAliveTest()
        {
            // Arrange
            var testData = "HTTP/1.0 200 OK\r\nContent-Type: text/plain\r\n\r\nHello World\r\n\r\n";
            var sourceStream = new MemoryStream(Encoding.ASCII.GetBytes(testData));

            // Act
            var header = new HttpHeader();
            await header.ParseHeaderAsync(sourceStream, HttpAction.Response);
            var stream = new HttpBodyStream(null, sourceStream, null, HttpBodyType.NonKeepAlive);
            var sr = new StreamReader(stream);
            var result = sr.ReadToEnd();

            // Assert
            Assert.Equal("Hello World\r\n\r\n", result, ignoreLineEndingDifferences: false);
        }

        [Fact]
        public async Task ChunkForwardTest()
        {
            // Arrange
            var sb = new StringBuilder();
            for (var i = 0; i < 65536; ++i)
            {
                sb.Append((i % 10).ToString());
            }
            var sourceStream = new MemoryStream(Encoding.ASCII.GetBytes(sb.ToString()));
            var stream = new HttpBodyStream(null, sourceStream, null, 65536);
            var destStream = new MemoryStream(new byte[65536 * 2]);
            var sr = new StreamReader(destStream);

            // Act
            await stream.ChunkedCopyToAsync(destStream, 200);
            destStream.Position = 0;

            // Assert
            var text = sr.ReadToEnd();
            Assert.True(text.EndsWith("0\r\n\r\n"));
        }
    }
}
