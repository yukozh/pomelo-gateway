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
            try
            {
                return IPEndPoint.Parse(address);
            }
            catch
            {
                if (!address.Contains(":"))
                {
                    address += ":" + defaultPort;
                }

                var splited = address.Split(':');
                var entry = await Dns.GetHostEntryAsync(splited[0]);
                return new IPEndPoint(entry.AddressList.Last(), Convert.ToInt32(splited[1]));
            }
        }
    }
}
