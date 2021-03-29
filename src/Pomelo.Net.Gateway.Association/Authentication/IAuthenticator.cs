using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace Pomelo.Net.Gateway.Association.Authentication
{
    public interface IAuthenticator
    {
        public string UserIdentifier { get; }
        public ValueTask<Credential> AuthenticateAsync(Memory<byte> body, CancellationToken cancellationToken = default);
        public ValueTask SendAuthenticatePacketAsync(Stream stream);
    }
}
