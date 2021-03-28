using System;
using System.Buffers;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Pomelo.Net.Gateway.Association.Authentication
{
    public abstract class BasicAuthenticator : IAuthenticator
    {
        public async ValueTask<Credential> AuthenticateAsync(Memory<byte> body, CancellationToken cancellationToken = default)
        {
            // 0: Length of Username
            // 1 ~ 1 + len(username): ASCII Encoded Username
            // 2 + len(username): Length of Password
            // 3 + len(username) ~ 3 + len(username) + len(password): ASCII Encoded Password

            Credential ret = new Credential();
            using (var memoryOwner = MemoryPool<byte>.Shared.Rent(128))
            {
                var credential = ParseCredentialFromBuffer(body);
                var result = await ValidateUserNameAndPasswordAsync(credential.Username, credential.Password, cancellationToken);
                if (result)
                {
                    ret.IsSucceeded = true;
                    ret.Identifier = credential.Username;
                }
            }
            return ret;
        }

        internal static (string Username, string Password) ParseCredentialFromBuffer(Memory<byte> buffer)
        {
            var username = Encoding.ASCII.GetString(buffer.Slice(1, buffer.Span[0]).Span);
            var password = Encoding.ASCII.GetString(buffer.Slice(2 + buffer.Span[0], buffer.Span[1 + buffer.Span[0]]).Span);
            return (username, password);
        }

        public static void BuildCredentialPacket(Memory<byte> buffer, string username, string password)
        {
            buffer.Span[0] = (byte)username.Length;
            Encoding.ASCII.GetBytes(username, buffer.Slice(1, username.Length).Span);
            buffer.Span[1 + username.Length] = (byte)password.Length;
            Encoding.ASCII.GetBytes(password, buffer.Slice(2 + username.Length, password.Length).Span);
        }

        public abstract ValueTask<bool> ValidateUserNameAndPasswordAsync(string username, string password, CancellationToken cancellationToken = default);
    }
}
