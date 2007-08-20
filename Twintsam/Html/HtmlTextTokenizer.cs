using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
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

            _input.ParseError += new EventHandler<ParseErrorEventArgs>(input_ParseError);
        }

        public HtmlTextTokenizer(TextReader input, string lastEmittedStartTagName) : this(input)
        {
            if (String.IsNullOrEmpty(lastEmittedStartTagName)) {
                throw new ArgumentNullException("lastEmittedStartTagName");
            }
            // TODO: check all chars in lastEmittedStartTagName are valid (first simple check: no space character)
            _lastEmittedStartTagName = lastEmittedStartTagName.ToLowerInvariant();
        }
        #endregion

        private void input_ParseError(object sender, ParseErrorEventArgs e)
        {
            OnParseError(e);
        }

        public override ReadState ReadState
        {
            get
            {
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
            return _input.HasLineInfo();
        }

        public int LineNumber
        {
            get { return _input.LineNumber; }
        }

        public int LinePosition
        {
            get { return _input.LinePosition; }
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
                            || (!_escapeFlag && (ContentModel == ContentModel.Rcdata || ContentModel == ContentModel.Cdata))) {
                            _currentParsingFunction = ParsingFunction.TagOpen;
                            return PrepareTextToken(sb.ToString());
                        } else {
                            sb.Append((char)next);
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
                        sb.Append((char)next);
                        break;
                    }
                }
            }
        }

        private void ParseEntityData()
        {
            // http://www.whatwg.org/specs/web-apps/current-work/multipage/section-tokenisation.html#entity
            Debug.Assert(ContentModel != ContentModel.Cdata);
            string character = ConsumeEntity(false);
            if (String.IsNullOrEmpty(character)) {
                PrepareTextToken("&");
            } else {
                PrepareTextToken(character);
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

        #region 8.2.3.1 Tokenizing entities
        private readonly Encoding Windows1252Encoding = Encoding.GetEncoding(1252);

        private const string REPLACEMENT_CHAR = "\uFFFD";

        private string ConsumeEntity(bool inAttributeValue)
        {
            // http://www.whatwg.org/specs/web-apps/current-work/multipage/section-tokenisation.html#consume
            switch (_input.Peek()) {
            case '\t':
            case '\n':
            case '\v':
            case '\f':
            case ' ':
            case '<':
            case '&':
            case -1:
                return null;
            case '#':
                return ConsumeNumericEntity();
            default:
                return ConsumeNamedEntity(inAttributeValue);
            }
        }

        private string ConsumeNumericEntity()
        {
            _input.Mark();
            _input.Read();
            int c = _input.Peek();

            bool isHex = false;
            if (c == 'x' || c == 'X') {
                _input.Read();
                isHex = true;
                c = _input.Peek();
            }

            // Without leading zeros, the longest representation for U+10FFFF is 1114111 (decimal), which is 9 chars long;
            // so we initialize the StringBuilder with this capacity (instead of the 1024-chars default)
            StringBuilder digits = new StringBuilder(9);
            while (('0' <= c && c <= '9') || (isHex && ('A' <= c && c <= 'F') || ('a' <= c && c <= 'f'))) {
                digits.Append((char)c);
                _input.Read();
                c = _input.Peek();
            }

            if (digits.Length == 0) {
                _input.ResetToMark();
                OnParseError("Unescaped &#" + (isHex ? "x" : ""));
                return null;
            }
            _input.UnsetMark();

            if (c == ';') {
                _input.Read();
            } else {
                OnParseError("Entity does not end with a semi-colon");
            }

            int codepoint;
            try {
                codepoint = Int32.Parse(digits.ToString(), isHex ? NumberStyles.AllowHexSpecifier : NumberStyles.None, CultureInfo.InvariantCulture);
            } catch (OverflowException) {
                OnParseError("Number too large, cannot be a Unicode code point.");
                return REPLACEMENT_CHAR;
            }

            if (codepoint == 13) {
                OnParseError("Incorrect CR newline entity. Replaced with LF.");
                codepoint = 10;
            } else if (128 <= codepoint && codepoint <= 159) {
                OnParseError("Entity used with illegal number (windows-1252 reference): " + codepoint.ToString());
                string windows1252encoded = Windows1252Encoding.GetString(new byte[] { (byte)codepoint });
                int newCodepoint = Char.ConvertToUtf32(windows1252encoded, 0);
                if (newCodepoint == codepoint) {
                    // Char.ConvertToUtf32 does not produce U+FFFD or throw an exception for an invalid Unicode code point, it just passes it unchanged
                    return REPLACEMENT_CHAR;
                }
                codepoint = newCodepoint;
            } else if (codepoint == 0) {
                OnParseError("Incorrect NUL entity. Replaced with U+FFFD");
                return REPLACEMENT_CHAR;
            }

            try {
                return Char.ConvertFromUtf32(codepoint);
            } catch (ArgumentOutOfRangeException) {
                OnParseError("Entity used with illegal number: " + codepoint.ToString());
                return REPLACEMENT_CHAR;
            }
        }

        private string ConsumeNamedEntity(bool inAttributeValue)
        {
            _input.Mark();
            StringBuilder entityName = new StringBuilder(HtmlEntities.LonguestEntityNameLength);
            while (entityName.Length <= HtmlEntities.LonguestEntityNameLength) {
                int c = _input.Peek();
                if (c < 0 || c == ';') {
                    break;
                }
                entityName.Append((char)_input.Read());
            }

            if (entityName.Length == 0) {
                if (_input.Peek() < 0) {
                    OnParseError("Unexpected end of file in character entity");
                } else {
                    OnParseError("Empty entity name &;");
                }
                _input.ResetToMark();
                return null;
            }

            int foundChar = -1;

            // Just for the ParseError below:
            string name = entityName.ToString();

            int nextChar = _input.Peek();
            if (nextChar != ';' || !HtmlEntities.TryGetChar(entityName.ToString(), out foundChar)){
                while (entityName.Length >= HtmlEntities.ShortestEntityNameLength){
                    if (inAttributeValue){
                        while ((('0' <= nextChar && nextChar <= '9')
                                || ('A' <= nextChar && nextChar <= 'Z')
                                || ('a' <= nextChar && nextChar <= 'z'))
                               && entityName.Length > 0){
                            nextChar = entityName[entityName.Length - 1];
                            entityName.Length--;
                        }
                    }

                    if (HtmlEntities.TryGetChar(entityName.ToString(), out foundChar)
                        && HtmlEntities.IsMissingSemiColonRecoverable(entityName.ToString())){
                        OnParseError("Entity does not end with a semi-colon");
                        break;
                    }

                    nextChar = entityName[entityName.Length - 1];
                    entityName.Length--;
                }
            }

            if (entityName.Length < HtmlEntities.ShortestEntityNameLength) {
                OnParseError("Named entity not found: " + name);
                _input.ResetToMark();
                return null;
            }

            _input.UnsetMark();

            return Char.ConvertFromUtf32(foundChar);
        }
        #endregion

        #endregion
    }
}
