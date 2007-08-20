using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Xml;

namespace Twintsam.Html
{
    internal class HtmlTextReader : TextReader, IXmlLineInfo
    {
        private const char REPLACEMENT_CHAR = '\uFFFD';

        private TextReader _reader;
        private bool _marked;
        private StringBuilder _buffer = new StringBuilder();
        private int _bufferPos;
        private bool _lastReadCharWasCarriageReturnInLineFeedLookAhead;

        private int _lineNumber;
        private int _linePosition;
        private int _markLineNumber;
        private int _markLinePosition;

        public HtmlTextReader(TextReader reader)
        {
            if (reader == null) {
                throw new ArgumentNullException("reader");
            }
            _reader = reader;
        }

        public virtual event EventHandler<ParseErrorEventArgs> ParseError;

        protected virtual void OnParseError(string message)
        {
            ParseErrorEventArgs args = new ParseErrorEventArgs(message, this as IXmlLineInfo);

            if (ParseError != null) {
                ParseError(this, args);
            }
        }

        private void OnReplacedNul()
        {
            OnParseError("U+0000 (NUL) replaced with U+FFFD");
        }

        public bool IsMarked { get { return _marked; } }

        public void Mark()
        {
            if (_marked) {
                throw new InvalidOperationException();
            }
            _marked = true;
            _markLineNumber = _lineNumber;
            _markLinePosition = _linePosition;
            Debug.Assert(_buffer.Length == 0);
            Debug.Assert(_bufferPos == 0);
        }

        public void UnsetMark()
        {
            if (!_marked) {
                throw new InvalidOperationException();
            }
            _marked = false;
            FreeBuffer();
        }

        private void FreeBuffer()
        {
            _buffer.Length = 0;
            _bufferPos = 0;
        }

        public void ResetToMark()
        {
            if (!_marked) {
                throw new InvalidOperationException();
            }
            _marked = false;
            _bufferPos = 0;
            _lineNumber = _markLineNumber;
            _linePosition = _markLinePosition;
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing) {
                _reader.Dispose();
                _buffer = null;
            }
            base.Dispose(disposing);
        }

        public override int Peek()
        {
            if (_buffer == null) {
                throw new ObjectDisposedException(null);
            }
            if (!_marked && _buffer.Length > _bufferPos) {
                return _buffer[_bufferPos];
            } else if (_lastReadCharWasCarriageReturnInLineFeedLookAhead) {
                return '\n';
            } else {
                int c = _reader.Peek();
                if (c == 0) {
                    OnReplacedNul();
                    c = REPLACEMENT_CHAR;
                } else if (c == '\r') {
                    _reader.Read();
                    c = _reader.Peek();
                    if (c != '\n') {
                        _lastReadCharWasCarriageReturnInLineFeedLookAhead = true;
                    }
                    c = '\n';
                }
                return c;
            }
        }

        public override int Read()
        {
            int c;
            if (!_marked && _buffer.Length > _bufferPos) {
                c = _buffer[_bufferPos++];
                if (_buffer.Length == _bufferPos) {
                    FreeBuffer();
                }
            } else if (_lastReadCharWasCarriageReturnInLineFeedLookAhead) {
                _lastReadCharWasCarriageReturnInLineFeedLookAhead = false;
                c = '\n';
            } else {
                c = _reader.Read();
                if (c == 0) {
                    OnReplacedNul();
                    c = REPLACEMENT_CHAR;
                } else if (c == '\r') {
                    c = '\n';
                    if (_reader.Peek() == '\n') {
                        _reader.Read();
                    }
                }
                if (c >= 0 && _marked) {
                    _buffer.Append((char) c);
                }
            }

            if (c == '\n') {
                _lineNumber++;
                _linePosition = 0;
            } else {
                _linePosition++;
            }

            return c;
        }

        public override int Read(char[] buffer, int index, int count)
        {
            if (_buffer.Length <= _bufferPos + count) {
                if (_lastReadCharWasCarriageReturnInLineFeedLookAhead && count > 0) {
                    _lastReadCharWasCarriageReturnInLineFeedLookAhead = false;
                    _buffer.Append('\n');
                }
                if (_buffer.Length <= _bufferPos + count) {
                    char[] chars = new char[count - (_buffer.Length - _bufferPos + 1)];
                    while (_buffer.Length <= _bufferPos + count) {
                        int charsToRead = Math.Min(count, count - (_buffer.Length - _bufferPos + 1));

                        int charsRead = _reader.Read(chars, 0, charsToRead);
                        if (charsRead > 0) {

                            if (chars[charsRead - 1] == '\r' && _reader.Peek() == '\n') {
                                chars[charsRead - 1] = '\n';
                                _reader.Read();
                            }

                            _buffer.Append(chars, 0, charsRead);
                            _buffer.Replace("\r\n", "\n", _buffer.Length - charsRead, charsRead);
                        }

                        if (charsRead < charsToRead) {
                            // At EOF
                            break;
                        }
                    }
                }
            }

            _buffer.Replace('\r', '\n', _bufferPos, _buffer.Length - _bufferPos + 1);

            int read = Math.Min(count, _buffer.Length - _bufferPos + 1);
            _buffer.CopyTo(_bufferPos, buffer, index, read);

            for (int i = 0; i < read; i++)
            {
                char c = buffer[index + i];
                if (c == '\n') {
                    _lineNumber++;
                    _linePosition = 0;
                } else {
                    _linePosition++;
                    if (c == '\0'){
                        OnReplacedNul();
                        buffer[index + i] = REPLACEMENT_CHAR;
                    }
                }
            }

            _buffer.Replace('\0', REPLACEMENT_CHAR, _bufferPos, _buffer.Length - _bufferPos + 1);

            _bufferPos += read;
            if (!_marked && _bufferPos >= _buffer.Length) {
                FreeBuffer();
            }

            return read;
        }

        public override int ReadBlock(char[] buffer, int index, int count)
        {
            if (_buffer.Length <= _bufferPos + count) {
                if (_lastReadCharWasCarriageReturnInLineFeedLookAhead && count > 0) {
                    _lastReadCharWasCarriageReturnInLineFeedLookAhead = false;
                    _buffer.Append('\n');
                }
                if (_buffer.Length <= _bufferPos + count) {
                    char[] chars = new char[count - (_buffer.Length - _bufferPos + 1)];
                    while (_buffer.Length <= _bufferPos + count) {
                        int charsToRead = Math.Min(count, count - (_buffer.Length - _bufferPos + 1));

                        int charsRead = _reader.ReadBlock(chars, 0, charsToRead);
                        if (charsRead > 0) {

                            if (chars[charsRead - 1] == '\r' && _reader.Peek() == '\n') {
                                chars[charsRead - 1] = '\n';
                                _reader.Read();
                            }

                            _buffer.Append(chars, 0, charsRead);
                            _buffer.Replace("\r\n", "\n", _buffer.Length - charsRead, charsRead);
                        }

                        if (charsRead < charsToRead) {
                            // At EOF
                            break;
                        }
                    }
                }
            }

            _buffer.Replace('\r', '\n', _bufferPos, _buffer.Length - _bufferPos + 1);

            int read = Math.Min(count, _buffer.Length - _bufferPos + 1);
            _buffer.CopyTo(_bufferPos, buffer, index, read);

            for (int i = 0; i < read; i++) {
                char c = buffer[index + i];
                if (c == '\n') {
                    _lineNumber++;
                    _linePosition = 0;
                } else {
                    _linePosition++;
                    if (c == '\0') {
                        OnReplacedNul();
                        buffer[index + i] = REPLACEMENT_CHAR;
                    }
                }
            }

            _buffer.Replace('\0', REPLACEMENT_CHAR, _bufferPos, _buffer.Length - _bufferPos + 1);

            _bufferPos += read;
            if (!_marked && _bufferPos >= _buffer.Length) {
                FreeBuffer();
            }

            return read;
        }

        public override string ReadLine()
        {
            throw new NotImplementedException();
        }

        public override string ReadToEnd()
        {
            string toEnd = _reader.ReadToEnd();
            toEnd = toEnd.Replace("\r\n", "\n");
            toEnd = toEnd.Replace('\r', '\n');

            foreach (char c in toEnd) {
                if (c == '\n') {
                    _lineNumber++;
                    _linePosition = 0;
                } else {
                    _linePosition++;
                    if (c == '\0') {
                        OnReplacedNul();
                    }
                }
            }

            toEnd = toEnd.Replace('\0', REPLACEMENT_CHAR);

            if (_marked) {
                _buffer.Append(toEnd);
            } else if (_buffer.Length > _bufferPos) {
                toEnd = _buffer.ToString(_bufferPos, _buffer.Length - _bufferPos) + toEnd;
                FreeBuffer();
            }

            return toEnd;
        }

        #region IXmlLineInfo Membres

        public bool HasLineInfo() { return true; }

        public int LineNumber { get { return _lineNumber; } }

        public int LinePosition { get { return _linePosition; } }

        #endregion
    }
}
