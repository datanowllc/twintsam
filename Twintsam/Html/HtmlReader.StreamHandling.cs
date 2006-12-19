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

        private TextReader _reader;
        private bool _readerAtEof;
        private StringBuilder _buffer = new StringBuilder();

        private int _lineNumber;
        private int _linePosition;

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

                    _buffer.Replace('\0', '\uFFFE');
                    _buffer.Replace("\r\n", "\n");
                    _buffer.Replace('\r', '\n');
                }
            }
            if (_buffer.Length <= offset) {
                return EOF_CHAR;
            }
            return _buffer[offset];
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

        #region IXmlLineInfo Members

        public bool HasLineInfo() { return true; }

        public int LineNumber { get { return _lineNumber; } }

        public int LinePosition { get { return _linePosition; } }

        #endregion
    }
}
