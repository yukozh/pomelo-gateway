using System.Threading;
using System.Threading.Tasks;

namespace System.IO
{
    public static class StreamExtensions
    {
        public static async ValueTask ReadExAsync(this Stream stream, Memory<byte> buffer, CancellationToken cancellationToken = default)
        {
            var len = 0;
            var _buffer = buffer;
            var bufferSize = buffer.Length;
            while (len < bufferSize)
            {
                var read = await stream.ReadAsync(buffer, cancellationToken);
                if (read == 0)
                {
                    throw new IOException($"Unexpected Stream End. Expected {bufferSize} bytes, Actual {len} bytes.");
                }
                len += read;
                buffer = _buffer.Slice(len, bufferSize - len);
            }
        }
    }
}
