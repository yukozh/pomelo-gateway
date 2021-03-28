using System;
using System.Threading;
using System.Threading.Tasks;

namespace Pomelo.Net.Gateway.Association.Authentication
{
    public interface IAuthenticator
    {
        public ValueTask<Credential> AuthenticateAsync(Memory<byte> body, CancellationToken cancellationToken = default);
    }
}
