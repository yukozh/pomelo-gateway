using System.Collections.Generic;

namespace System.IO
{
    public class ConcatStream : Stream
    {
        private Queue<Stream> readStreams = new Queue<Stream>();
        private Queue<Stream> writeStreams = new Queue<Stream>();
        private long position = 0;

        public override bool CanRead => readStreams.Peek().CanRead;

        public override bool CanSeek => false;

        public override bool CanWrite => writeStreams.Peek().CanWrite;

        public override long Length => readStreams.Peek().Length;

        public override long Position 
        { 
            get => position; 
            set => throw new NotSupportedException();
        }

        public override void Flush()
        {
            writeStreams.Peek().Flush();
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            while (readStreams.Count > 0 && !readStreams.Peek().CanRead)
            {
                readStreams.Dequeue();
            }

            if (readStreams.Count == 0)
            {
                return 0;
            }
            var _count = readStreams.Peek().Read(buffer, offset, count);
            if (_count == 0)
            {
                readStreams.Dequeue();
                return Read(buffer, offset, count);
            }
            position += _count;
            return _count;
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
            while (writeStreams.Count > 0 && !writeStreams.Peek().CanWrite)
            {
                writeStreams.Dequeue();
            }
            writeStreams.Peek().Write(buffer, offset, count);
        }

        public void Join(Stream stream)
        {
            readStreams.Enqueue(stream);
            writeStreams.Enqueue(stream);
        }

        public void Join(params Stream[] streams)
        {
            foreach (var stream in streams)
            {
                Join(stream);
            }
        }
    }
}
