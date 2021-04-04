using Xunit;
using System.Net;
using System.Threading.Tasks;

namespace Pomelo.Net.Gateway.Shared.Tests
{
    public class AddressHelperTests
    {
        [Fact]
        public async Task ParseIpStringTest()
        {
            // Arrange
            var address = "127.0.0.1:1234";

            // Act
            var ipAddress = await AddressHelper.ParseAddressAsync(address, 4321);

            // Assert
            Assert.EndsWith(":1234", ipAddress.ToString());
        }

        [Fact]
        public async Task ParseDomainStringTest()
        {
            // Arrange
            var address = "localhost:1234";

            // Act
            var ipAddress = await AddressHelper.ParseAddressAsync(address, 4321);

            // Assert
            Assert.EndsWith(":1234", ipAddress.ToString());
        }
    }
}
