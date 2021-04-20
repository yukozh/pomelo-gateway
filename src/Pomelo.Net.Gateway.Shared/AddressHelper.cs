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
            if (IPEndPoint.TryParse(address, out var result))
            {
                return result;
            }

            if (!address.Contains(":"))
            {
                address += ":" + defaultPort;
            }

            var splited = address.Split(':');
            var entry = await Dns.GetHostEntryAsync(splited[0]);
            return new IPEndPoint(entry.AddressList.Last(), Convert.ToInt32(splited[1]));
        }

        public static string TrimPort(string endpoint)
        {
            return endpoint.Split(':')[0];
        }
    }
}
