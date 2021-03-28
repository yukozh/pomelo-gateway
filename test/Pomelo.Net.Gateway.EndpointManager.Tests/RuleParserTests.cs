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

            // Act
            var length = RuleParser.BuildRulePacket(new Endpoint 
            {
                IPAddress = IPAddress.Parse(address),
                Port = 8000,
                Protocol = Protocol.TCP
            }, buffer);

            // Assert
            Assert.Equal(7, length);
            Assert.Equal(8000, BitConverter.ToUInt16(buffer.Slice(1, 2).Span));
            Assert.Equal(address, new IPAddress(buffer.Slice(3, 4).Span).ToString());
        }

        [Fact]
        public void BuildRulePacketIPv6Test()
        {
            // Arrange
            var buffer = new Memory<byte>(new byte[256]);
            var address = "fe80:a:1:b:440:44ff:1233:5678";

            // Act
            var length = RuleParser.BuildRulePacket(new Endpoint
            {
                IPAddress = IPAddress.Parse(address),
                Port = 8000,
                Protocol = Protocol.TCP
            }, buffer);

            // Assert
            Assert.Equal(19, length);
            Assert.Equal(8000, BitConverter.ToUInt16(buffer.Slice(1, 2).Span));
            Assert.Equal(address, new IPAddress(buffer.Slice(3, 16).Span).ToString());
        }
    }
}
