using System;
using System.Buffers;
using System.IO;

namespace Pomelo.Net.Gateway.Http
{
    public enum HttpBodyType
    { 
        FixedLength,
        Chunked
    }

    public class HttpBodyReadonlyStream : Stream, IDisposable
    {
        private const int MaxChunkBodySize = 2048;
        private const int TempBufferSize = MaxChunkBodySize + 8;

        private long length;
        private long position = 0;
        private Stream baseStream;
        private HttpBodyType type;
        private byte[] temp;
        private int tempReadPos = 0;
        private int tempStoredBytes = 0;
        private bool streamEnd = false;

        public HttpBodyReadonlyStream(Stream baseStream, long length)
        {
            this.baseStream = baseStream;
            this.length = length;
            this.type = HttpBodyType.FixedLength;
            if (length <= 0)
            {
                this.length = 0;
                this.temp = ArrayPool<byte>.Shared.Rent(TempBufferSize);
            }
        }
        public HttpBodyReadonlyStream(Stream baseStream)
        {
            this.baseStream = baseStream;
            this.length = -1;
            this.type = HttpBodyType.Chunked;
            this.temp = ArrayPool<byte>.Shared.Rent(TempBufferSize);
        }

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
            tempReadPos += bytesToRead;
            tempStoredBytes -= bytesToRead;
            return (tempReadPos, bytesToRead);
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
            else
            {
                if (!streamEnd && tempStoredBytes == 0) 
                {
                    // Read a chunk
                    tempReadPos = 0;
                    tempStoredBytes = 0;
                    baseStream.ReadEx(temp, 0, 4);
                    tempStoredBytes += 4;
                    position += 4;
                    var chunkLength = BitConverter.ToUInt16(temp, 0);
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
                        baseStream.ReadEx(temp, 4, chunkLength);
                        tempStoredBytes += chunkLength;
                        position += chunkLength;
                    }
                }

                // Send chunk data to destination buffer
                if (tempStoredBytes > 0)
                {
                    var pos = ReadFromTempbuffer(count);
                    Buffer.BlockCopy(temp, pos.StartIndex, buffer, offset, pos.Count);
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
