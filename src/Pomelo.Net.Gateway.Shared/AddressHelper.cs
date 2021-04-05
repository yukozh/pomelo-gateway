using System.Linq;
using System.Threading.Tasks;

namespace System.Net
{
    public static class AddressHelper
    {
        public static async ValueTask<IPEndPoint> ParseAddressAsync(
            string address,
            int defaultPort)
        {
            if (!address.Contains(":"))
            {
                address += ":" + defaultPort;
            }

            var splited = address.Split(':');
            var entry = await Dns.GetHostEntryAsync(splited[0]);
            return IPEndPoint.Parse($"{entry.AddressList.Last()}:{splited[1]}");
        }
    }
}
