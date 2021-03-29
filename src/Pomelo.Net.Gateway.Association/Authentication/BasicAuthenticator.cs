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
        private string username, password;
        private static Random random = new Random();

        public BasicAuthenticator()
        { }

        public BasicAuthenticator(string username, string password)
        {
            this.username = username;
            this.password = password;
        }

        public async ValueTask<Credential> AuthenticateAsync(Memory<byte> body, CancellationToken cancellationToken = default)
        {
            // 0: Length of Username
            // 1 ~ 1 + len(username): ASCII Encoded Username
            // 2 + len(username): Length of Password
            // 3 + len(username) ~ 3 + len(username) + len(password): ASCII Encoded Password

            Credential ret = new Credential();
            using (var tokenBuffer = MemoryPool<byte>.Shared.Rent(8))
            using (var memoryOwner = MemoryPool<byte>.Shared.Rent(128))
            {
                var credential = ParseCredentialFromBuffer(body);
                var result = await ValidateUserNameAndPasswordAsync(credential.Username, credential.Password, cancellationToken);
                if (result)
                {
                    ret.IsSucceeded = true;
                    ret.Identifier = credential.Username;
                    while (ret.Token == 0)
                    {
                        random.NextBytes(tokenBuffer.Memory.Slice(0, 8).Span);
                        ret.Token = BitConverter.ToInt64(tokenBuffer.Memory.Slice(0, 8).Span);
                    }
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

        public static int BuildCredentialPacket(Memory<byte> buffer, string username, string password)
        {
            buffer.Span[0] = (byte)username.Length;
            Encoding.ASCII.GetBytes(username, buffer.Slice(1, username.Length).Span);
            buffer.Span[1 + username.Length] = (byte)password.Length;
            Encoding.ASCII.GetBytes(password, buffer.Slice(2 + username.Length, password.Length).Span);
            return 2 + username.Length + password.Length;
        }

        public abstract ValueTask<bool> ValidateUserNameAndPasswordAsync(string username, string password, CancellationToken cancellationToken = default);

        public async ValueTask SendAuthenticatePacketAsync(Stream stream)
        {
            if (username == null || password == null)
            {
                throw new ArgumentNullException("Missing username or password");
            }

            using (var buffer = MemoryPool<byte>.Shared.Rent(256))
            {
                buffer.Memory.Span[0] = (byte)AssociateOpCode.BasicAuthLogin;
                var length = (byte)BuildCredentialPacket(buffer.Memory.Slice(2), username, password);
                buffer.Memory.Span[1] = length;
                await stream.WriteAsync(buffer.Memory.Slice(0, 2 + length));
            }
        }
    }
}
