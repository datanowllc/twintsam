using System;
using System.IO;
using System.Text;

namespace Twintsam.IO
{
    // http://www.whatwg.org/specs/web-apps/current-work/multipage/section-parsing.html#the-input0
    public abstract class DetectEncodingStreamReader : TextReader
    {
        private const int _DefaultBufferSize = 1000;

        private Stream _stream;
        private Encoding _encoding;
        private bool _confident;

        private byte[] buffer;
        private int _position;
        private int bufferLength;
        private int _bufferSize;

        private static Stream EnsureSeekable(Stream stream, int bufferSize)
        {
            if (stream == null) {
                throw new ArgumentNullException("stream");
            }
            if (bufferSize  <= 0) {
                throw new ArgumentOutOfRangeException("bufferSize");
            }
            if (stream.CanSeek) {
                return stream;
            } else {
                return new SeekableReadOnlyStream(stream, bufferSize);
            }
        }

        #region Constructors
        public DetectEncodingStreamReader(Stream stream)
            : this(stream, _DefaultBufferSize) { }
        public DetectEncodingStreamReader(Stream stream, int bufferSize)
        {
            _stream = EnsureSeekable(stream, bufferSize);
            _bufferSize = bufferSize;
        }
        public DetectEncodingStreamReader(string path)
            : this(File.OpenRead(path)) { }
        public DetectEncodingStreamReader(string path, int bufferSize)
            : this(File.OpenRead(path), bufferSize) { }
        #endregion

        public Encoding Encoding { get { return _encoding; } }

        public bool IsEncodingConfident { get { return _confident; } }

        public TextReader GetEncodingConfidentReader()
        {
            if (_encoding == null) {
                throw new InvalidOperationException("No encoding detected.");
            }
            Stream stream = _stream;
            if (stream is SeekableReadOnlyStream) {
                stream = ((SeekableReadOnlyStream)stream).GetReadOnlyStream();
            }
            return new StreamReader(stream, _encoding, false, _bufferSize);
        }

        #region TextReader implementation
        public override int Peek()
        {
            return _stream.Peek();
        }

        public override int Read()
        {
            return _stream.Read();
        }

        public override int Read(char[] buffer, int index, int count)
        {
            return _stream.Read(buffer, index, count);
        }

        public override string ReadLine()
        {
            return _stream.ReadLine();
        }

        public override string ReadToEnd()
        {
            return _stream.ReadToEnd();
        }
        #endregion
    }
}
