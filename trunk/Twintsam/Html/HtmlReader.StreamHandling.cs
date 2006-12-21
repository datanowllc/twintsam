using System;
using System.Collections.Generic;
using System.Text;
using System.Xml;
using System.IO;
using System.Diagnostics;
using System.Globalization;

namespace Twintsam.Html
{
    public partial class HtmlReader : IXmlLineInfo
    {
        private const char EOF_CHAR = '\0';
        private const char REPLACEMENT_CHAR = '\uFFFD';

        private TextReader _reader;
        private bool _readerAtEof;
        private StringBuilder _buffer = new StringBuilder();

        private int _lineNumber;
        private int _linePosition;

        private bool EndOfStream { get { return _buffer.Length == 0 && _readerAtEof; } }

        private char NextInputChar { get { return PeekChar(0); } }

        private char EatNextInputChar()
        {
            char c = NextInputChar;
            EatChars(1);
            return c;
        }

        private char PeekChar(int offset)
        {
            if (_buffer.Length <= offset && !_readerAtEof) {
                char[] chars = new char[offset - _buffer.Length + 1];

                while (_buffer.Length <= offset && !_readerAtEof) {
                    int charsToRead = Math.Min(chars.Length, offset - _buffer.Length + 1);

                    int charsRead = _reader.ReadBlock(chars, 0, charsToRead);
                    if (charsRead < charsToRead) {
                        _readerAtEof = true;
                    }

                    _buffer.Append(chars, 0, charsRead);

                    _buffer.Replace('\0', REPLACEMENT_CHAR);
                    _buffer.Replace("\r\n", "\n");
                    _buffer.Replace('\r', '\n');
                }
            }
            if (_buffer.Length <= offset) {
                return EOF_CHAR;
            }
            return _buffer[offset];
        }

        private string PeekChars(int count)
        {
            return PeekChars(0, count);
        }

        private string PeekChars(int offset, int count)
        {
            // ensure we have enough chars in the buffer
            char c = PeekChar(offset + count - 1);
            // if we reached EOF, we probably won't be able to peek 'count' chars
            if (c == EOF_CHAR) {
                count = _buffer.Length - offset;
            }
            return _buffer.ToString(offset, count);
        }

        private string PeekChars(Predicate<char> condition)
        {
            StringBuilder sb = new StringBuilder();
            PeekChars(sb, condition);
            return sb.ToString();
        }
        private string PeekChars(Predicate<char> condition, int estimatedLength)
        {
            StringBuilder sb = new StringBuilder(estimatedLength);
            PeekChars(sb, condition);
            return sb.ToString();
        }
        private int PeekChars(StringBuilder sb, Predicate<char> condition)
        {
            int offset = 0;
            char c = PeekChar(offset);
            while (c != EOF_CHAR && condition(c)) {
                sb.Append(c);
                c = PeekChar(++offset);
            }
            return offset;
        }

        private void EatChars(int count)
        {
            Debug.Assert(count > 0, String.Format(CultureInfo.InvariantCulture, "HtmlReader.EatChars called with non-positive argument: {0}.", count));

            char[] chars = new char[count];
            _buffer.CopyTo(0, chars, 0, count);
            _buffer.Remove(0, count);

            Array.ForEach<char>(chars, delegate(char c)
            {
                if (c == '\n') {
                    _lineNumber++;
                    _linePosition = 0;
                } else {
                    _linePosition++;
                }
            });
        }

        private int SkipChars(Predicate<char> condition)
        {
            int offset = 0;
            char c = NextInputChar;
            while (c != EOF_CHAR && condition(c)) {
                c = PeekChar(++offset);
            }
            if (offset > 0) {
                EatChars(offset);
            }
            return offset;
        }

        #region IXmlLineInfo Members

        public bool HasLineInfo() { return true; }

        public int LineNumber { get { return _lineNumber; } }

        public int LinePosition { get { return _linePosition; } }

        #endregion
    }
}
