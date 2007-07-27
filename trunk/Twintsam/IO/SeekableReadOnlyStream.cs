using System;
using System.IO;
using System.Diagnostics;
using System.ComponentModel;

namespace Twintsam.IO
{
    /// <summary>
    /// Adds a buffered memory to any readable <see cref="Stream"/> to allow
    /// seeking backwards (eventually rewinding back to the stream origin).
    /// </summary>
    public class SeekableReadOnlyStream : Stream
    {
        private const int _DefaultBufferSize = 1000;

        private static readonly byte[] EMPTY_BUFFER = new byte[0];

        private Stream _stream;

        private byte[] _buffer = EMPTY_BUFFER;
        private int _bufferLength;
        private int _position;
        private int _bufferSize;

        public SeekableReadOnlyStream(Stream stream) : this(stream, _DefaultBufferSize) { }

        public SeekableReadOnlyStream(Stream stream, int bufferSize)
        {
            if (stream == null) {
                throw new ArgumentNullException("stream");
            }
            if (bufferSize < 1) {
                throw new ArgumentOutOfRangeException("bufferSize");
            }
            _stream = stream;
            _bufferSize = bufferSize;
        }

        private void CheckDisposed()
        {
            if (_buffer == null) {
                throw new ObjectDisposedException(null);
            }
        }
        protected override void Dispose(bool disposing)
        {
            if (disposing && _buffer != null) {
                _buffer = null;
                _stream.Dispose();
            }
            base.Dispose(disposing);
        }

        public Stream GetReadOnlyStream()
        {
            CheckDisposed();
            Stream stream = new StreamChain(
                new MemoryStream(_buffer, _position, _bufferLength - _position, false),
                _stream);
            this.Close();
            return stream;
        }

        private void ReadBuffer(int count)
        {
            int availableBufferSpace = Buffer.ByteLength(_buffer) - _bufferLength;
            if (count > availableBufferSpace) {
                count -= availableBufferSpace;
            }
            count = ((count / _bufferSize) * _bufferSize) + _bufferSize;
            byte[] newBuffer = new byte[_bufferLength + count];
            Buffer.BlockCopy(_buffer, 0, newBuffer, 0, _bufferLength);
            _buffer = newBuffer;
            _bufferLength += _stream.Read(_buffer, _bufferLength, count);
        }

        public override bool CanRead { get { return true; } }

        public override bool CanSeek { get { return true; } }

        public override bool CanWrite { get { return false; } }

        public override void Flush() { /* no-op */ }

        public override long Length
        {
            get { throw new NotSupportedException(); }
        }

        public override long Position
        {
            get { return _position; }
            set
            {
                CheckDisposed();
                if (value < 0) {
                    throw new ArgumentOutOfRangeException();
                }
                if (value > _position) {
                    throw new InvalidOperationException("Seeking forwards is not supported");
                }
                _position = (int)value;
                Debug.Assert(_position <= _bufferLength);
            }
        }

        public override int ReadByte()
        {
            CheckDisposed();
            Debug.Assert(_position <= _bufferLength);
            if (_position >= _bufferLength) {
                ReadBuffer(1);
                Debug.Assert(_position <= _bufferLength);
                if (_position >= _bufferLength) {
                    return -1;
                }
            }
            return _buffer[_position++];
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            CheckDisposed();
            Debug.Assert(_position <= _bufferLength);
            if (_position + count > _bufferLength) {
                ReadBuffer(count);
                Debug.Assert(_position <= _bufferLength);
                if (_position + count > _bufferLength) {
                    count = _bufferLength - _position;
                }
            }
            Buffer.BlockCopy(_buffer, _position, buffer, offset, count);
            _position += count;
            return count;
        }

        public override long Seek(long offset, SeekOrigin origin)
        {
            CheckDisposed();
            if (!Enum.IsDefined(typeof(SeekOrigin), origin)) {
                throw new InvalidEnumArgumentException("origin", (int) origin, typeof(SeekOrigin));
            }
            if (origin == SeekOrigin.Current) {
                origin = SeekOrigin.Current;
                offset += _position;
            }
            if (origin == SeekOrigin.End || offset > _position) {
                throw new InvalidOperationException("Seeking forwards is not supported");
            }
            // we can safely cast to and int as the previous test ensures offset <= _position
            _position = (int)offset;
            Debug.Assert(_position <= _bufferLength);
            return _position;
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
