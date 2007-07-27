using System;
using System.IO;
using System.Collections.Generic;

namespace Twintsam.IO
{
    public class StreamChain : Stream
    {
        private Queue<Stream> _streams;
        private Stream _currentStream;

        private bool _canRead;
        private bool _canTimeout;

        public StreamChain(Stream s1, Stream s2, params Stream[] streams)
        {
            if (s1 == null) {
                throw new ArgumentNullException("s1");
            }
            if (s2 == null) {
                throw new ArgumentNullException("s2");
            }
            if (streams == null) {
                throw new ArgumentNullException("streams");
            }
            _currentStream = s1;
            _streams = new Queue<Stream>(streams.Length + 1);
            _streams.Enqueue(s2);
            foreach (Stream stream in streams) {
                _streams.Enqueue(stream);
            }
            Init();
        }
        public StreamChain(IEnumerable<Stream> streams)
        {
            if (streams == null) {
                throw new ArgumentNullException("streams");
            }
            _currentStream = Stream.Null;
            _streams = new Queue<Stream>(streams);
            Init();
        }

        private void Init()
        {
            _canRead = _currentStream.CanRead;
            _canTimeout = _currentStream.CanTimeout;

            foreach (Stream stream in _streams) {
                if (stream == null) {
                    throw new ArgumentNullException("streams");
                }
                _canRead &= stream.CanRead;
                _canTimeout |= stream.CanTimeout;
            }
        }

        private bool NextStream()
        {
            _currentStream.Dispose();
            try {
                _currentStream = _streams.Dequeue();
                return true;
            } catch (InvalidOperationException) {
                _currentStream = Stream.Null;
                return false;
            }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing) {
                _currentStream.Dispose();
                foreach (Stream stream in _streams) {
                    stream.Dispose();
                }
            }
            base.Dispose(disposing);
        }

        public override bool CanRead { get { return _canRead; } }

        public override bool CanSeek { get { return false; } }

        public override bool CanWrite { get { return false; } }

        public override bool CanTimeout { get { return _canTimeout; } }

        public override void Flush()
        {
            _currentStream.Flush();
        }

        public override long Length
        {
            get { throw new NotSupportedException(); }
        }

        public override long Position
        {
            get { throw new NotSupportedException(); }
            set { throw new NotSupportedException(); }
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            int totalReadBytes = 0;
            while (totalReadBytes < count) {
                int readBytes = _currentStream.Read(buffer,
                    totalReadBytes + offset, totalReadBytes - count);
                if (readBytes == 0) {
                    if (!NextStream()) {
                        break;
                    }
                } else {
                    totalReadBytes += readBytes;
                }
            }
            return totalReadBytes;
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
    }
}
