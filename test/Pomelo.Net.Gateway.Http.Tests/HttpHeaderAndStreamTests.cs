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
            var testData = "6\r\nDotNet\r\n9\r\nDeveloper\r\n0\r\n\r\nHTTP/1.1 200 OK\r\nContent-Type: text/plain\r\nTransfer-Encoding: chunked\r\n\r\n6\r\nPomelo\r\n10\r\nFoundation\r\n0\r\n\r\nHTTP/1.1 200 OK\r\nContent-Type: text/plain\r\nContent-Length: 11\r\n\r\nHello World";
            var sourceStream = new MemoryStream(Encoding.ASCII.GetBytes(testData));

            // Act 1
            var stream1 = new HttpBodyReadonlyStream(sourceStream);
            var sr1 = new StreamReader(stream1);
            var result1 = sr1.ReadToEnd();

            // Assert 1
            Assert.Equal("DotNetDeveloper", result1);

            // Act 2
            var header = new HttpHeader();
            await header.ParseHeaderAsync(sourceStream, HttpAction.Response);
            var stream2 = new HttpBodyReadonlyStream(sourceStream);
            var sr2 = new StreamReader(stream2);
            var result2 = sr2.ReadToEnd();

            // Assert 2
            Assert.Equal("PomeloFoundation", result2);

            // Act 3
            var header2 = new HttpHeader();
            await header2.ParseHeaderAsync(sourceStream, HttpAction.Response);
            var stream3 = new HttpBodyReadonlyStream(sourceStream, header2.ContentLength);
            var sr3 = new StreamReader(stream3);
            var result3 = sr3.ReadToEnd();

            // Assert 3
            Assert.Equal("Hello World", result3);
        }

        [Fact]
        public async Task Http1_0Test()
        {
            // Arrange
            var testData = "HTTP/1.0 200 OK\r\nContent-Type: text/plain\r\n\r\nHello World\r\n\r\n";
            var sourceStream = new MemoryStream(Encoding.ASCII.GetBytes(testData));

            // Act
            var header = new HttpHeader();
            await header.ParseHeaderAsync(sourceStream, HttpAction.Response);
            var stream = new HttpBodyReadonlyStream(sourceStream, HttpBodyType.NonKeepAlive);
            var sr = new StreamReader(stream);
            var result = sr.ReadToEnd();

            // Assert
            Assert.Equal("Hello World\r\n\r\n", result, ignoreLineEndingDifferences: false);
        }
    }
}
