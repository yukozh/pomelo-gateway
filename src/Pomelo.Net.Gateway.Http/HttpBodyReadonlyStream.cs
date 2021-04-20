using System;
using System.Buffers;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Pomelo.Net.Gateway.Http
{
    public enum HttpBodyType
    { 
        FixedLength,
        Chunked,
        WebSocket,
        NonKeepAlive,
    }

    public class HttpBodyReadonlyStream : Stream, IDisposable
    {
        private const int MaxChunkBodySize = 0xffff;
        private const int TempBufferSize = MaxChunkBodySize + 10;

        private long length;
        private long position = 0;
        private Stream baseStream;
        private HttpBodyType type;
        private byte[] temp;
        private int tempReadPos = 0;
        private int tempStoredBytes = 0;
        private bool streamEnd = false;

        public HttpBodyReadonlyStream(Stream baseSourceStream, long length)
        {
            this.baseStream = baseSourceStream;
            this.length = length;
            this.type = HttpBodyType.FixedLength;
            if (length <= 0)
            {
                this.length = 0;
                this.temp = ArrayPool<byte>.Shared.Rent(TempBufferSize);
            }
        }
        public HttpBodyReadonlyStream(Stream baseStream, HttpBodyType type = HttpBodyType.Chunked)
        {
            if (type == HttpBodyType.FixedLength)
            {
                throw new InvalidOperationException("Please use HttpBodyReadonlyStream(Stream, long) to init fixed length body");
            }
            this.baseStream = baseStream;
            this.length = -1;
            this.type = type;
            this.temp = ArrayPool<byte>.Shared.Rent(TempBufferSize);
        }

        public HttpBodyType Type => type;

        public override bool CanRead => true;

        public override bool CanSeek => false;

        public override bool CanWrite => false;

        public override long Length => length;

        public override long Position 
        { 
            get => position; 
            set => throw new InvalidOperationException("This stream does not support modify position.");
        }

        public override void Flush()
        {
            throw new NotSupportedException();
        }

        private (int StartIndex, int Count) ReadFromTempbuffer(int count)
        {
            var bytesToRead = Math.Min(tempStoredBytes, count);
            tempStoredBytes -= bytesToRead;
            var ret = (tempReadPos, bytesToRead);
            tempReadPos += bytesToRead;
            return ret;
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            if (type == HttpBodyType.FixedLength)
            {
                var bytesToRead = Math.Min(length - position, count);
                if (bytesToRead == 0)
                {
                    return 0;
                }
                var read = baseStream.Read(buffer, offset, (int)bytesToRead);
                position += read;
                return read;
            }
            else if (type == HttpBodyType.NonKeepAlive 
                || type == HttpBodyType.WebSocket)
            {
                var read = baseStream.Read(buffer, offset, count);
                position += read;
                return read;
            }
            else
            {
                if (!streamEnd && tempStoredBytes == 0)
                {
                    // Read a chunk
                    tempReadPos = 0;
                    tempStoredBytes = 0;
                    var chunkHeaderLine = baseStream.ReadLineExAsync(default).GetAwaiter().GetResult();
                    var chunkLength = Convert.ToInt32("0x" + chunkHeaderLine, 16);
                    if (chunkLength == 0)
                    {
                        streamEnd = true;
                    }
                    else
                    {
                        if (chunkLength > MaxChunkBodySize)
                        {
                            throw new InvalidDataException("Chunk size is too large");
                        }
                        baseStream.ReadEx(temp, 0, chunkLength);
                        tempStoredBytes += chunkLength;
                    }
                    baseStream.ReadEx(temp, MaxChunkBodySize, 2);
                }

                // Send chunk data to destination buffer
                if (tempStoredBytes > 0)
                {
                    var pos = ReadFromTempbuffer(count);
                    Buffer.BlockCopy(temp, pos.StartIndex, buffer, offset, pos.Count);
                    position += pos.Count;
                    return pos.Count;
                }
                else
                {
                    return 0;
                }
            }
        }

        public override long Seek(long offset, SeekOrigin origin)
        {
            throw new NotSupportedException();
        }

        public override void SetLength(long value)
        {
            throw new NotSupportedException();
        }

        public override void Write(byte[] buffer, int offset, int count)
        {
            throw new NotSupportedException();
        }

        public async ValueTask ChunkedCopyToAsync(
            Stream destination, 
            int bufferSize = 2048,
            CancellationToken cancellationToken = default)
        {
            using (var buffer = MemoryPool<byte>.Shared.Rent(bufferSize + 8))
            {
                var _buffer = buffer.Memory.Slice(8, bufferSize);
                while (true)
                {
                    var count = await ReadAsync(_buffer, cancellationToken);
                    var headerCount = Encoding.ASCII.GetBytes($"{count.ToString("x")}\r\n", buffer.Memory.Slice(0, 8).Span);
                    await destination.WriteAsync(buffer.Memory.Slice(0, headerCount), cancellationToken);
                    if (count > 0)
                    {
                        await destination.WriteAsync(_buffer.Slice(0, count), cancellationToken);
                    }
                    Encoding.ASCII.GetBytes($"\r\n", buffer.Memory.Slice(0, 2).Span);
                    await destination.WriteAsync(buffer.Memory.Slice(0, 2), cancellationToken);
                    if (count == 0)
                    {
                        break;
                    }
                }
            }
        }

        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);
            if (temp != null)
            {
                ArrayPool<byte>.Shared.Return(temp);
            }
        }
    }
}
