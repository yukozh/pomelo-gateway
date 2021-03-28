using System;
using System.Net;
using Xunit;
using Pomelo.Net.Gateway.EndpointCollection;

namespace Pomelo.Net.Gateway.EndpointManager.Tests
{
    public class RuleParserTests
    {
        [Fact]
        public void BuildRulePacketIPv4Test()
        {
            // Arrange
            var buffer = new Memory<byte>(new byte[256]);
            var address = "127.0.0.1";
            var guid1 = Guid.NewGuid();
            var guid2 = Guid.NewGuid();

            // Act
            var length = RuleParser.BuildRulePacket(new Endpoint 
            {
                IPAddress = IPAddress.Parse(address),
                Port = 8000,
                Protocol = Protocol.TCP,
                RouterId = guid1,
                TunnelId = guid2
            }, buffer);

            // Assert
            Assert.Equal(39, length);
            Assert.Equal(guid1, new Guid(buffer.Slice(1, 16).Span));
            Assert.Equal(guid2, new Guid(buffer.Slice(17, 16).Span));
            Assert.Equal(8000, BitConverter.ToUInt16(buffer.Slice(33, 2).Span));
            Assert.Equal(address, new IPAddress(buffer.Slice(35, 4).Span).ToString());
        }

        [Fact]
        public void BuildRulePacketIPv6Test()
        {
            // Arrange
            var buffer = new Memory<byte>(new byte[256]);
            var address = "fe80:a:1:b:440:44ff:1233:5678";
            var guid1 = Guid.NewGuid();
            var guid2 = Guid.NewGuid();

            // Act
            var length = RuleParser.BuildRulePacket(new Endpoint
            {
                IPAddress = IPAddress.Parse(address),
                Port = 8000,
                Protocol = Protocol.TCP,
                RouterId = guid1,
                TunnelId = guid2
            }, buffer);

            // Assert
            Assert.Equal(51, length);
            Assert.Equal(guid1, new Guid(buffer.Slice(1, 16).Span));
            Assert.Equal(guid2, new Guid(buffer.Slice(17, 16).Span));
            Assert.Equal(8000, BitConverter.ToUInt16(buffer.Slice(33, 2).Span));
            Assert.Equal(address, new IPAddress(buffer.Slice(35, 16).Span).ToString());
        }
    }
}
