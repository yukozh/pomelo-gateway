using System;
using System.Buffers;
using System.IO;
using System.Linq;
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

    public class HttpBodyStream : Stream, IDisposable
    {
        private static byte[] EndChunk = Encoding.ASCII.GetBytes("0\r\n\r\n");
        private const int MaxChunkBodySize = 0xffff;
        private const int TempBufferSize = MaxChunkBodySize + 10;

        private long sourceLength;
        private long position = 0;
        private Stream baseSourceStream;
        private Stream baseDestinationStream;
        private HttpBodyType sourceType;
        private byte[] temp;
        private int tempReadPos = 0;
        private int tempStoredBytes = 0;
        private bool streamEnd = false;
        private bool canRead = true;
        private bool canWrite = true;
        private bool wroteBody = false;
        private bool wroteFooter = false;
        private HttpTunnelContextPart contextPart;

        public HttpBodyStream(
            HttpTunnelContextPart contextPart,
            Stream baseSourceStream, 
            Stream baseDestinationStream,
            long sourceLength)
        {
            this.baseSourceStream = baseSourceStream;
            this.baseDestinationStream = baseDestinationStream;
            this.sourceLength = sourceLength;
            this.sourceType = HttpBodyType.FixedLength;
            this.contextPart = contextPart;
            if (sourceLength > 0)
            {
                this.temp = ArrayPool<byte>.Shared.Rent(TempBufferSize);
            }
            else
            {
                this.sourceLength = 0;
            }
        }
        public HttpBodyStream(
            HttpTunnelContextPart contextPart,
            Stream baseSourceStream,
            Stream baseDestinationStream,
            HttpBodyType type = HttpBodyType.Chunked)
        {
            if (type == HttpBodyType.FixedLength)
            {
                throw new InvalidOperationException("Please use HttpBodyReadonlyStream(Stream, long) to init fixed length body");
            }
            this.baseSourceStream = baseSourceStream;
            this.baseDestinationStream = baseDestinationStream;
            this.sourceLength = -1;
            this.sourceType = type;
            this.temp = ArrayPool<byte>.Shared.Rent(TempBufferSize);
            this.contextPart = contextPart;
        }

        public HttpBodyType Type => sourceType;

        public HttpTunnelContext Context => contextPart.HttpContext;

        public HttpHeader Headers => contextPart?.Headers;

        public HttpAction HttpAction => contextPart.HttpAction;

        public override bool CanRead => canRead;

        public override bool CanSeek => false;

        public override bool CanWrite => canWrite;

        public override long Length => sourceLength;

        public override long Position 
        { 
            get => position; 
            set => throw new InvalidOperationException("This stream does not support modify position.");
        }

        public override void Flush()
        {
            baseDestinationStream.Flush();
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
            if (sourceType == HttpBodyType.FixedLength)
            {
                var bytesToRead = Math.Min(sourceLength - position, count);
                if (bytesToRead == 0)
                {
                    canRead = false;
                    return 0;
                }
                var read = baseSourceStream.Read(buffer, offset, (int)bytesToRead);
                position += read;
                return read;
            }
            else if (sourceType == HttpBodyType.NonKeepAlive 
                || sourceType == HttpBodyType.WebSocket)
            {
                var read = baseSourceStream.Read(buffer, offset, count);
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
                    var chunkHeaderLine = baseSourceStream.ReadLineExAsync(default).GetAwaiter().GetResult();
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
                        baseSourceStream.ReadEx(temp, 0, chunkLength);
                        tempStoredBytes += chunkLength;
                    }
                    baseSourceStream.ReadEx(temp, MaxChunkBodySize, 2);
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
                    canRead = false;
                    return 0;
                }
            }
        }

        public async ValueTask CompleteAsync(
            CancellationToken cancellationToken = default)
        {
            lock (this)
            {
                if (!wroteFooter)
                {
                    wroteFooter = true;
                }
                else
                {
                    return;
                }
            }
            await baseDestinationStream.WriteAsync(
                EndChunk,
                0, 
                EndChunk.Length, 
                cancellationToken);
            canWrite = false;
            await baseDestinationStream.FlushAsync();
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
            EnsureChunkedAsync().GetAwaiter().GetResult();
            WriteBase(buffer, offset, count);
        }

        private void WriteBase(byte[] buffer, int offset, int count)
        {
            var headerBuffer = ArrayPool<byte>.Shared.Rent(6);
            try
            {
                while (count > 0)
                {
                    wroteBody = true;
                    var bytesToWrite = Math.Min(0xff, count);
                    var header = $"{bytesToWrite.ToString("x")}\r\n";
                    var chunkHeaderLength = Encoding.ASCII.GetBytes(header, headerBuffer);
                    baseDestinationStream.Write(headerBuffer, 0, chunkHeaderLength);
                    baseDestinationStream.Write(buffer, offset, bytesToWrite);
                    baseDestinationStream.Write(EndChunk, 3, 2);
                    count -= bytesToWrite;
                    offset += bytesToWrite;
                }
            }
            finally
            {
                ArrayPool<byte>.Shared.Return(headerBuffer);
            }
        }

        private async ValueTask EnsureChunkedAsync(CancellationToken cancellationToken = default)
        {
            if (Headers != null 
                && Headers.IsWroteToStream 
                && Headers.TransferEncoding.All(x => x.ToLower() != "chunked"))
            {
                throw new InvalidOperationException("The body is not in chunked mode and headers have already sent");
            }

            if (Headers != null && !Headers.IsWroteToStream)
            {
                Headers.AddOrUpdate("transfer-encoding", "chunked");
                Headers.TryRemove("content-length");
                await Headers.WriteToStreamAsync(baseDestinationStream, HttpAction, cancellationToken);
            }
        }

        public async ValueTask ChunkedCopyToAsync(
            Stream destination, 
            int bufferSize = 2048,
            CancellationToken cancellationToken = default)
        {
            await EnsureChunkedAsync(cancellationToken);
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
            if (wroteBody)
            {
                try
                {
                    CompleteAsync().GetAwaiter().GetResult();
                }
                catch
                { }
            }
            if (temp != null)
            {
                ArrayPool<byte>.Shared.Return(temp);
            }
        }
    }
}
