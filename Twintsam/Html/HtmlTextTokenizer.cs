using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Xml;
using System.Text;

namespace Twintsam.Html
{
    public class HtmlTextTokenizer : HtmlTokenizer, IXmlLineInfo
    {
        private struct Attribute
        {
            public string name;
            public string value;
            public char quoteChar;
        }

        /// <summary>
        /// A parsing function, roughly corresponding to a token to be parsed.
        /// </summary>
        /// <returns><see langword="true"/> if a token has been produced, <see langword="false"/> if no token is ready yet.</returns>
        private enum ParsingFunction
        {
            Initial,
            Data,
            EntityData,
            TagOpen,
            CloseTagOpen,
            // To be continued...
            Eof,
            ReaderClosed,
        }

        private HtmlTextReader _input;
        private XmlNameTable _nameTable;
        private ContentModel _contentModel;
        private XmlNodeType _tokenType;
        private string _name;
        private string _value;
        private List<Attribute> _attributes = new List<Attribute>();
        private bool _trailingSolidus;
        private bool _incorrectDoctype;

        private string _lastEmittedStartTagName;
        private bool _escapeFlag;
        private ParsingFunction _currentParsingFunction = ParsingFunction.Initial;

        #region Constructors
        // TODO: add overloads with 'string inputUri' and Stream arguments
        public HtmlTextTokenizer(TextReader input)
        {
            if (input == null) {
                throw new ArgumentNullException("input");
            }
            _input = input as HtmlTextReader;
            if (_input == null) {
                _input = new HtmlTextReader(input);
            }
        }
        #endregion

        public override ReadState ReadState
        {
            get {
                switch (_currentParsingFunction) {
                case ParsingFunction.Initial:
                    return ReadState.Initial;
                case ParsingFunction.Eof:
                    return ReadState.EndOfFile;
                case ParsingFunction.ReaderClosed:
                    return ReadState.Closed;
                default:
                    return ReadState.Interactive;
                }
            }
        }

        public override bool EOF
        {
            get { return _currentParsingFunction == ParsingFunction.Eof; }
        }

        public override XmlNameTable NameTable
        {
            get { return _nameTable; }
        }

        public override ContentModel ContentModel
        {
            get { return _contentModel; }
            set
            {
                if (ReadState != ReadState.Initial) {
                    throw new InvalidOperationException();
                }
                _contentModel = value;
            }
        }

        public override XmlNodeType TokenType
        {
            get { return _tokenType; }
        }

        public override string Name
        {
            get { return _name; }
        }

        public override bool HasTrailingSolidus
        {
            get { return _trailingSolidus; }
        }

        public override bool IsIncorrectDoctype
        {
            get { return _incorrectDoctype; }
        }

        public override string Value
        {
            get { return _value; }
        }

        public override int AttributeCount
        {
            get { return _attributes.Count; }
        }

        public override string GetAttributeName(int index)
        {
            return _attributes[index].name;
        }

        public override char GetAttributeQuoteChar(int index)
        {
            return _attributes[index].quoteChar;
        }

        public override string GetAttribute(int index)
        {
            return _attributes[index].value;
        }

        public override string GetAttribute(string name)
        {
            foreach (Attribute attribute in _attributes) {
                if (String.Equals(attribute.name, name, StringComparison.OrdinalIgnoreCase)) {
                    return attribute.value;
                }
            }
            return null;
        }

        public override bool Read()
        {
            bool newToken = false;
            do {
                switch (_currentParsingFunction) {
                case ParsingFunction.Initial:
                    // http://www.whatwg.org/specs/web-apps/current-work/multipage/section-tokenisation.htmlmultipage/section-tokenisation.html#tokenization:
                    // The state machine must start in the data state.
                    _currentParsingFunction = ParsingFunction.Data;
                    break;
                case ParsingFunction.Eof:
                case ParsingFunction.ReaderClosed:
                    return false;
                case ParsingFunction.Data:
                    newToken = ParseData();
                    break;
                case ParsingFunction.EntityData:
                    ParseEntityData();
                    break;
                case ParsingFunction.TagOpen:
                    newToken = ParseTagOpen();
                    break;
                case ParsingFunction.CloseTagOpen:
                    newToken = ParseCloseTagOpen();
                    break;
                default:
                    throw new NotImplementedException();
                }
            } while (!newToken);

            Debug.Assert(newToken);
            if (TokenType == XmlNodeType.EndElement && HasAttributes) {
                _contentModel = ContentModel.Pcdata;
                OnParseError("End tag with attributes");
            }
            return true;
        }

        public override void Close()
        {
            _currentParsingFunction = ParsingFunction.ReaderClosed;
        }

        #region IXmlLineInfo Membres

        public bool HasLineInfo()
        {
            throw new NotImplementedException();
        }

        public int LineNumber
        {
            get { throw new NotImplementedException(); }
        }

        public int LinePosition
        {
            get { throw new NotImplementedException(); }
        }

        #endregion

        /// <summary>
        /// Initialises a new token with the given type.
        /// </summary>
        /// <param name="tokenType">The type of the token, should be on of <see cref="XmlNodeType.DocumentType"/>, <see cref="XmlNodeType.Element"/>, <see cref="XmlNodeType.EndElement"/>, <see cref="XmlNodeType.Text"/>, <see cref="XmlNodeType.Whitespace"/> or <see cref="XmlNodeType.Comment"/></param>
        protected void InitToken(XmlNodeType newTokenType)
        {
            Debug.Assert(newTokenType == XmlNodeType.DocumentType
                || newTokenType == XmlNodeType.Element
                || newTokenType == XmlNodeType.EndElement
                || newTokenType == XmlNodeType.Text
                || newTokenType == XmlNodeType.Whitespace
                || newTokenType == XmlNodeType.Comment);

            if (_tokenType == XmlNodeType.Element) {
                _lastEmittedStartTagName = _name;
            } else if (newTokenType != XmlNodeType.Text && newTokenType == XmlNodeType.Whitespace) {
                // _lastEmittedStartTagName is for CDATA and RCDATA,
                // which can only contain text tokens, so clear it if
                // a non-text token is produced.
                _lastEmittedStartTagName = null;
            }

            _tokenType = newTokenType;
            _name = null;
            _value = null;
            _attributes.Clear();
            _trailingSolidus = false;
            _incorrectDoctype = false;
        }

        protected bool PrepareTextToken(string value)
        {
            if (_tokenType == XmlNodeType.Text || _tokenType == XmlNodeType.Whitespace) {
                if (_tokenType == XmlNodeType.Whitespace && !Constants.IsSpace(value)) {
                    _tokenType = XmlNodeType.Text;
                }
                _value += value;
            } else {
                InitToken(Constants.IsSpace(value) ? XmlNodeType.Whitespace : XmlNodeType.Text);
                _value = value;
            }
            return _value.Length > 0;
        }

        #region Parsing
        // http://www.whatwg.org/specs/web-apps/current-work/multipage/section-tokenisation.htmlmultipage/section-tokenisation.html#data-state
        private bool ParseData()
        {
            if (ContentModel == ContentModel.PlainText) {
                PrepareTextToken(_input.ReadToEnd());
                _currentParsingFunction = ParsingFunction.Eof;
                return true;
            } else {
                StringBuilder sb = new StringBuilder();
                int next;
                // http://www.whatwg.org/specs/web-apps/current-work/multipage/section-tokenisation.html#data-state
                while (true) {
                    next = _input.Read();
                    if (next < 0) {
                        _currentParsingFunction = ParsingFunction.Eof;
                        return PrepareTextToken(sb.ToString());
                    }
                    switch (next) {
                    case '&':
                        if (ContentModel == ContentModel.Pcdata || ContentModel == ContentModel.Rcdata) {
                            _currentParsingFunction = ParsingFunction.EntityData;
                            PrepareTextToken(sb.ToString());
                            return false;
                        } else {
                            sb.Append('&');
                        }
                        break;
                    case '-':
                        sb.Append('-');
                        if (!_escapeFlag && (ContentModel == ContentModel.Rcdata || ContentModel == ContentModel.Cdata)) {
                            PrepareTextToken(sb.ToString());
                            sb = new StringBuilder();
                            if (_value.EndsWith("<!--", StringComparison.Ordinal)) {
                                _escapeFlag = true;
                            }
                        }
                        break;
                    case '<':
                        if ((ContentModel == ContentModel.Pcdata)
                            || (!_escapeFlag && (ContentModel == ContentModel.Rcdata || ContentModel == ContentModel.Pcdata))) {
                            _currentParsingFunction = ParsingFunction.TagOpen;
                            return PrepareTextToken(sb.ToString());
                        } else {
                            sb.Append((char) next);
                        }
                        break;
                    case '>':
                        sb.Append('>');
                        if (_escapeFlag && (ContentModel == ContentModel.Rcdata || ContentModel == ContentModel.Cdata)) {
                            PrepareTextToken(sb.ToString());
                            sb = new StringBuilder();
                            if (_value.EndsWith("-->", StringComparison.Ordinal)) {
                                _escapeFlag = false;
                            }
                        }
                        break;
                    default:
                        sb.Append((char) next);
                        break;
                    }
                }
            }
        }

        private void ParseEntityData()
        {
            Debug.Assert(ContentModel != ContentModel.Cdata);
            // http://www.whatwg.org/specs/web-apps/current-work/multipage/section-tokenisation.html#entity
            switch (_input.Peek()) {
            case '\t':
            case '\n':
            case '\v':
            case '\f':
            case ' ':
            case '<':
            case '&':
            case -1:
                // Not an entity. No characters are consumed, and nothing is returned. (This is not an error, either.)
                break;
            case '#': // Numeric (decimal or hexadecimal) entity
                throw new NotImplementedException();
            default: // Named entity
                throw new NotImplementedException();
            }
            _currentParsingFunction = ParsingFunction.Data;
        }

        private bool ParseTagOpen()
        {
            if (ContentModel == ContentModel.Rcdata || ContentModel == ContentModel.Cdata) {
                if (_input.Peek() == '/') {
                    _currentParsingFunction = ParsingFunction.CloseTagOpen;
                } else {
                    PrepareTextToken("<");
                    _currentParsingFunction = ParsingFunction.Data;
                }
                return false;
            } else {
                throw new NotImplementedException();
            }
        }

        private bool ParseCloseTagOpen()
        {
            throw new NotImplementedException();
        }
        #endregion
    }
}
