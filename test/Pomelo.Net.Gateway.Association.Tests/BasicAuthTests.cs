using System;
using System.Collections.Generic;
using System.Text;
using System.Linq;
using Xunit;

namespace Pomelo.Net.Gateway.Association.Tests
{
    public class BasicAuthTests
    {
        [Fact]
        public void BuildCredentialPacketTest()
        {
            // Arrange
            var bytes = new List<byte>();
            var username = "admin";
            var password = "123456";
            bytes.Add(0x05);
            bytes.AddRange(Encoding.ASCII.GetBytes(username));
            bytes.Add(0x06);
            bytes.AddRange(Encoding.ASCII.GetBytes(password));
            var expected = bytes.ToArray();

            // Act
            var buffer = new Memory<byte>(new byte[128]);
            Authentication.BasicAuthenticator.BuildCredentialPacket(buffer, username, password);

            // Assert
            Assert.True(expected.SequenceEqual(buffer.Slice(0, 13).Span.ToArray()));
        }

        [Fact]
        public void ParseCredentialTest()
        {
            // Arrange
            var username = "admin";
            var password = "123456";
            var buffer = new Memory<byte>(new byte[128]);
            Authentication.BasicAuthenticator.BuildCredentialPacket(buffer, username, password);

            // Act
            var result = Authentication.BasicAuthenticator.ParseCredentialFromBuffer(buffer);

            // Assert
            Assert.Equal(username, result.Username);
            Assert.Equal(password, result.Password);
        }
    }
}
