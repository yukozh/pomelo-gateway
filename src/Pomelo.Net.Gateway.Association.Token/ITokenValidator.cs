using System.Threading.Tasks;

namespace Pomelo.Net.Gateway.Association.Token
{
    public interface ITokenValidator
    {
        ValueTask<bool> ValidateAsync(long token, string userIdentifier);
    }
}
