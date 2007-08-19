using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Xml;

namespace Twintsam.Html
{
    internal class HtmlTextReader : TextReader, IXmlLineInfo
    {
        private const char EOF_CHAR = '\0';
        private const char REPLACEMENT_CHAR = '\uFFFD';

        private TextReader _reader;
        private bool _marked;
        private StringBuilder _buffer = new StringBuilder();
        private int _bufferPos;
        private bool _lastReadCharWasCarriageReturnInLineFeedLookAhead;

        private int _lineNumber;
        private int _linePosition;

        public HtmlTextReader(TextReader reader)
        {
            if (reader == null) {
                throw new ArgumentNullException("reader");
            }
            _reader = reader;
        }

        public bool IsMarked { get { return _marked; } }

        public void Mark()
        {
            if (_marked) {
                throw new InvalidOperationException();
            }
            _marked = true;
            Debug.Assert(_buffer.Length == 0);
            Debug.Assert(_bufferPos == 0);
        }

        public void UnsetMark()
        {
            if (!_marked) {
                throw new InvalidOperationException();
            }
            _marked = false;
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
                    UnsetMark();
                }
            } else if (_lastReadCharWasCarriageReturnInLineFeedLookAhead) {
                _lastReadCharWasCarriageReturnInLineFeedLookAhead = false;
                c = '\n';
            } else {
                c = _reader.Read();
                if (c == 0) {
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
            int read = _buffer.Length - _bufferPos + 1;

            if (read < count) {
                if (_lastReadCharWasCarriageReturnInLineFeedLookAhead && count > 0) {
                    _lastReadCharWasCarriageReturnInLineFeedLookAhead = false;
                    _buffer.Append('\n');
                    read++;
                }
                if (read < count) {
                    char[] chars = new char[count - read];
                    while (_buffer.Length <= _bufferPos + count) {
                        int charsToRead = Math.Min(count, count - (_buffer.Length - _bufferPos + 1));

                        int charsRead = _reader.Read(chars, 0, charsToRead);
                        read += charsRead;

                        _buffer.Append(chars, 0, charsRead);

                        _buffer.Replace('\0', REPLACEMENT_CHAR);
                        _buffer.Replace("\r\n", "\n");
                        _buffer.Replace('\r', '\n');

                        if (charsRead < charsToRead) {
                            // At EOF
                            break;
                        }
                    }
                }
            }

            _buffer.CopyTo(_bufferPos, buffer, index, read);

            Array.ForEach<char>(buffer, delegate(char c) {
                if (c == '\n') {
                    _lineNumber++;
                    _linePosition = 0;
                } else {
                    _linePosition++;
                }
            });

            if (read < count) {
                _bufferPos += read;
            } else {
                UnsetMark();
            }

            return read;
        }

        public override int ReadBlock(char[] buffer, int index, int count)
        {
            int read = _buffer.Length - _bufferPos + 1;

            if (read < count) {
                if (_lastReadCharWasCarriageReturnInLineFeedLookAhead && count > 0) {
                    _lastReadCharWasCarriageReturnInLineFeedLookAhead = false;
                    _buffer.Append('\n');
                    read++;
                }
                if (read < count) {
                    char[] chars = new char[count - read];
                    while (_buffer.Length <= _bufferPos + count) {
                        int charsToRead = Math.Min(count, count - (_buffer.Length - _bufferPos + 1));

                        int charsRead = _reader.ReadBlock(chars, 0, charsToRead);
                        read += charsRead;

                        _buffer.Append(chars, 0, charsRead);

                        _buffer.Replace('\0', REPLACEMENT_CHAR);
                        _buffer.Replace("\r\n", "\n");
                        _buffer.Replace('\r', '\n');

                        if (charsRead < charsToRead) {
                            // At EOF
                            break;
                        }
                    }
                }
            }

            _buffer.CopyTo(_bufferPos, buffer, index, read);
            
            Array.ForEach<char>(buffer, delegate(char c) {
                if (c == '\n') {
                    _lineNumber++;
                    _linePosition = 0;
                } else {
                    _linePosition++;
                }
            });

            if (read < count) {
                _bufferPos += read;
            } else {
                UnsetMark();
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
            toEnd = toEnd.Replace('\0', REPLACEMENT_CHAR);
            toEnd = toEnd.Replace("\r\n", "\n");
            toEnd = toEnd.Replace('\r', '\n');

            foreach (char c in toEnd) {
                if (c == '\n') {
                    _lineNumber++;
                    _linePosition = 0;
                } else {
                    _linePosition++;
                }
            }

            if (_marked) {
                _buffer.Append(toEnd);
            } else if (_buffer.Length > _bufferPos) {
                toEnd = _buffer.ToString(_bufferPos, _buffer.Length - _bufferPos) + toEnd;
                UnsetMark();
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
