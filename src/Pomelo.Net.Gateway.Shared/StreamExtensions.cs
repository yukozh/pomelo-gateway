using System.Buffers;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace System.IO
{
    public static class StreamExtensions
    {
        public static async ValueTask ReadExAsync(this Stream stream, Memory<byte> buffer, CancellationToken cancellationToken = default)
        {
            if (!stream.CanRead)
            {
                throw new InvalidOperationException("The stream cannot be read");
            }

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

        public static void ReadEx(this Stream stream, byte[] buffer, int offset, int count)
        {
            if (!stream.CanRead)
            {
                throw new InvalidOperationException("The stream cannot be read");
            }

            var len = 0;
            var bufferSize = count;
            while (len < bufferSize)
            {
                var read = stream.Read(buffer, len + offset, bufferSize - len);
                if (read == 0)
                {
                    throw new IOException($"Unexpected Stream End. Expected {bufferSize} bytes, Actual {len} bytes.");
                }
                len += read;
            }
        }

        public static async ValueTask<string> ReadLineExAsync(this Stream stream, CancellationToken cancellationToken = default)
        {
            var sb = new StringBuilder();
            using (var buffer = MemoryPool<byte>.Shared.Rent(1))
            {
                var _buffer = buffer.Memory.Slice(0, 1);
                while (true)
                {
                    var count = await stream.ReadAsync(_buffer);
                    if (count == 0)
                    {
                        break;
                    }
                    if (_buffer.Span[0] == '\n')
                    {
                        break;
                    }
                    sb.Append((char)_buffer.Span[0]);
                }
            }
            if (sb.Length - 1 >= 0 && sb[sb.Length - 1] == '\r')
            {
                sb.Remove(sb.Length - 1, 1);
            }
            return sb.ToString();
        }
    }
}
