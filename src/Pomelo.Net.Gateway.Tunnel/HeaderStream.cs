﻿using System;
using System.IO;

namespace Pomelo.Net.Gateway.Tunnel
{
    public class HeaderStream : Stream
    {
        private Memory<byte> buffer;

        public HeaderStream(Memory<byte> buffer)
        {
            this.buffer = buffer;
        }

        public override bool CanRead => true;

        public override bool CanSeek => false;

        public override bool CanWrite => false;

        public override long Length => buffer.Length;

        public override long Position { get; set; }

        public override void Flush()
        {
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            var memory = new Memory<byte>(buffer, offset, count);
            var length = (int)Math.Min(count, Length - (int)Position);
            this.buffer.Slice((int)Position, length).CopyTo(memory);
            Position += length;
            return length;
        }

        public override long Seek(long offset, SeekOrigin origin)
        {
            throw new InvalidOperationException();
        }

        public override void SetLength(long value)
        {
            throw new InvalidOperationException();
        }

        public override void Write(byte[] buffer, int offset, int count)
        {
            throw new InvalidOperationException();
        }
    }
}
