using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Xml;
using System.Text;

namespace Twintsam.Html
{
    public sealed class HtmlTextTokenizer : HtmlTokenizer, IXmlLineInfo
    {
        internal class Attribute : Twintsam.Html.Attribute
        {
            public bool isDuplicate;
            public Attribute(string name, IXmlLineInfo lineInfo)
                : this(name, false, lineInfo) { }

            public Attribute(string name, bool isDuplicate, IXmlLineInfo lineInfo)
                : base(name, lineInfo)
            {
                this.isDuplicate = isDuplicate;
            }
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
            TagName,
            BeforeAttributeName,
            AttributeName,
            AfterAttributeName,
            BeforeAttributeValue,
            AttributeValueDoubleQuoted,
            AttributeValueSingleQuoted,
            AttributeValueUnquoted,
            // EntityInAttributeValue: handled "inline" in the ParseAttributeXXX methods
            BogusComment,
            MarkupDeclarationOpen,
            CommentStart,
            CommentStartDash,
            Comment,
            CommentEndDash,
            CommentEnd,
            Doctype,
            BeforeDoctypeName,
            DoctypeName,
            AfterDoctypeName,
            BeforeDoctypePublicId,
            DoctypePublicIdDoubleQuoted,
            DoctypePublicIdSingleQuoted,
            AfterDoctypePublicId,
            BeforeDoctypeSystemId,
            DoctypeSystemIdDoubleQuoted,
            DoctypeSystemIdSingleQuoted,
            AfterDoctypeSystemId,
            BogusDoctype,
            Eof,
            ReaderClosed,
        }

        private enum TokenState
        {
            Uninitialized,
            Initialized,
            Complete,
        }

#if DEBUG
        private bool _isFragmentParser;
#endif
        private HtmlTextReader _input;
        private XmlNameTable _nameTable;

        private ContentModel _contentModel;

        private TokenState _tokenState;
        private XmlNodeType _tokenType;
        private string _name;
        private string _value;
        internal List<Attribute> _attributes = new List<Attribute>();
        private bool _trailingSolidus;
        private bool _incorrectDoctype;

        private StringBuilder _textToken = new StringBuilder();
        private bool _textTokenIsWhitespace;

        private string _lastEmittedStartTagName;
        private bool _escapeFlag;
        private ParsingFunction _currentParsingFunction = ParsingFunction.Initial;

        private StringBuilder _buffer = new StringBuilder();

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
#if DEBUG
            _isFragmentParser = true;
#endif
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
                    return (_textToken.Length > 0) ? ReadState.Interactive : ReadState.EndOfFile;
                case ParsingFunction.ReaderClosed:
                    return ReadState.Closed;
                default:
                    return ReadState.Interactive;
                }
            }
        }

        public override bool EOF
        {
            get { return (_textToken.Length == 0) && (_currentParsingFunction == ParsingFunction.Eof); }
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
                _contentModel = value;
            }
        }

        public override XmlNodeType TokenType
        {
            get
            {
                if (_textToken.Length > 0) {
                    return _textTokenIsWhitespace ? XmlNodeType.Whitespace : XmlNodeType.Text;
                }
                return _tokenType;
            }
        }

        public override string Name
        {
            get
            {
                if (_textToken.Length > 0) {
                    return String.Empty;
                }
                return _name ?? String.Empty;
            }
        }

        public override bool HasTrailingSolidus
        {
            get
            {
                if (_textToken.Length > 0) {
                    return false;
                }
                return _trailingSolidus;
            }
        }

        public override bool IsIncorrectDoctype
        {
            get
            {
                if (_textToken.Length > 0) {
                    return false;
                }
                return _incorrectDoctype;
            }
        }

        public override string Value
        {
            get
            {
                if (_textToken.Length > 0) {
                    return _textToken.ToString();
                }
                return _value ?? String.Empty;
            }
        }

        public override int AttributeCount
        {
            get
            {
                if (_textToken.Length > 0) {
                    return 0;
                }
                return _attributes.Count;
            }
        }

        public override string GetAttributeName(int index)
        {
            if (_textToken.Length > 0) {
                throw new InvalidOperationException();
            }
            Debug.Assert(_attributes[index].name != null);
            return _attributes[index].name;
        }

        public override char GetAttributeQuoteChar(int index)
        {
            if (_textToken.Length > 0) {
                throw new InvalidOperationException();
            }
#if DEBUG
            char c = _attributes[index].quoteChar;
            Debug.Assert(c == '"' || c == '\'' || c == ' ');
#endif
            return _attributes[index].quoteChar;
        }

        public override string GetAttribute(int index)
        {
            if (_textToken.Length > 0) {
                throw new InvalidOperationException();
            }
            return _attributes[index].value;
        }

        public override int GetAttributeIndex(string name)
        {
            if (_textToken.Length > 0) {
                return -1;
            }
            int index = 0;
            foreach (Attribute attribute in _attributes) {
                if (String.Equals(attribute.name, name, StringComparison.OrdinalIgnoreCase)) {
                    Debug.Assert(attribute.value != null);
                    return index;
                }
            }
            return -1;
        }

        public override string GetAttribute(string name)
        {
            if (_textToken.Length > 0) {
                return null;
            }
            foreach (Attribute attribute in _attributes) {
                if (String.Equals(attribute.name, name, StringComparison.OrdinalIgnoreCase)) {
                    Debug.Assert(attribute.value != null);
                    return attribute.value;
                }
            }
            return null;
        }

        public override bool Read()
        {
            if (_tokenState == TokenState.Complete) {
                if (_textToken.Length > 0) {
                    _textToken.Length = 0;
                    _textTokenIsWhitespace = true;
                    return true;
                }
                _tokenState = TokenState.Uninitialized;
            }

            _textToken.Length = 0;
            _textTokenIsWhitespace = true;

            do {
                switch (_currentParsingFunction) {
                case ParsingFunction.Initial:
                    // http://www.whatwg.org/specs/web-apps/current-work/multipage/section-tokenisation.htmlmultipage/section-tokenisation.html#tokenization:
                    // The state machine must start in the data state.
                    _currentParsingFunction = ParsingFunction.Data;
                    break;
                case ParsingFunction.Eof:
                    if (_textToken.Length == 0) {
                        return false;
                    }
                    _tokenState = TokenState.Initialized; // HACK: to exit the while loop
                    break;
                case ParsingFunction.ReaderClosed:
                    return false;
                case ParsingFunction.Data:
                    ParseData();
                    break;
                case ParsingFunction.EntityData:
                    ParseEntityData();
                    break;
                case ParsingFunction.TagOpen:
                    ParseTagOpen();
                    break;
                case ParsingFunction.CloseTagOpen:
                    ParseCloseTagOpen();
                    break;
                case ParsingFunction.TagName:
                    ParseTagName();
                    break;
                case ParsingFunction.BeforeAttributeName:
                    ParseBeforeAttributeName();
                    break;
                case ParsingFunction.AttributeName:
                    ParseAttributeName();
                    break;
                case ParsingFunction.AfterAttributeName:
                    ParseAfterAttributeName();
                    break;
                case ParsingFunction.BeforeAttributeValue:
                    ParseBeforeAttributeValue();
                    break;
                case ParsingFunction.AttributeValueDoubleQuoted:
                    ParseAttributeValueDoubleQuoted();
                    break;
                case ParsingFunction.AttributeValueSingleQuoted:
                    ParseAttributeValueSingleQuoted();
                    break;
                case ParsingFunction.AttributeValueUnquoted:
                    ParseAttributeValueUnquoted();
                    break;
                // case ParsingFunction.EntityInAttributeValue: handled "inline" in the ParseAttributeXXX methods
                case ParsingFunction.BogusComment:
                    ParseBogusComment();
                    break;
                case ParsingFunction.MarkupDeclarationOpen:
                    ParseMarkupDeclarationOpen();
                    break;
                case ParsingFunction.CommentStart:
                    ParseCommentStart();
                    break;
                case ParsingFunction.CommentStartDash:
                    ParseCommentStartDash();
                    break;
                case ParsingFunction.Comment:
                    ParseComment();
                    break;
                case ParsingFunction.CommentEndDash:
                    ParseCommentEndDash();
                    break;
                case ParsingFunction.CommentEnd:
                    ParseCommentEnd();
                    break;
                case ParsingFunction.Doctype:
                    ParseDoctype();
                    break;
                case ParsingFunction.BeforeDoctypeName:
                    ParseBeforeDoctypeName();
                    break;
                case ParsingFunction.DoctypeName:
                    ParseDoctypeName();
                    break;
                case ParsingFunction.AfterDoctypeName:
                    ParseAfterDoctypeName();
                    break;
                case ParsingFunction.BeforeDoctypePublicId:
                    ParseBeforeDoctypePublicId();
                    break;
                case ParsingFunction.DoctypePublicIdDoubleQuoted:
                    ParseDoctypePublicIdDoubleQuoted();
                    break;
                case ParsingFunction.DoctypePublicIdSingleQuoted:
                    ParseDoctypePublicIdSingleQuoted();
                    break;
                case ParsingFunction.AfterDoctypePublicId:
                    ParseAfterDoctypePublicId();
                    break;
                case ParsingFunction.BeforeDoctypeSystemId:
                    ParseBeforeDoctypeSystemId();
                    break;
                case ParsingFunction.DoctypeSystemIdDoubleQuoted:
                    ParseDoctypeSystemIdDoubleQuoted();
                    break;
                case ParsingFunction.DoctypeSystemIdSingleQuoted:
                    ParseDoctypeSystemIdSingleQuoted();
                    break;
                case ParsingFunction.AfterDoctypeSystemId:
                    ParseAfterDoctypeSystemId();
                    break;
                case ParsingFunction.BogusDoctype:
                    ParseBogusDoctype();
                    break;
                default:
                    throw new InvalidOperationException();
                }
            } while (_tokenState == TokenState.Uninitialized
                || (_tokenState == TokenState.Initialized && _textToken.Length == 0));

            if (_tokenState == TokenState.Complete){
                switch (_tokenType) {
                case XmlNodeType.Element:
                case XmlNodeType.EndElement:
                    // Check duplicate attributes
                    _attributes.RemoveAll(
                        delegate(Attribute attr)
                        {
                            if (attr.isDuplicate) {
                                OnParseError(new ParseErrorEventArgs(String.Concat("Duplicate attribute: ", attr.name), attr));
                                return true;
                            }
                            return false;
                        }
                    );
                    if (_tokenType == XmlNodeType.EndElement) {
                        _contentModel = ContentModel.Pcdata;
                        if (_attributes.Count > 0) {
                            OnParseError("End tag with attributes");
                        }
                    }
                    break;
                }
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
        private void InitToken(XmlNodeType newTokenType)
        {
            Debug.Assert(newTokenType == XmlNodeType.DocumentType
                || newTokenType == XmlNodeType.Element
                || newTokenType == XmlNodeType.EndElement
                || newTokenType == XmlNodeType.Comment);
            Debug.Assert(_tokenState != TokenState.Initialized);

            _tokenState = TokenState.Initialized;

            if (_tokenType == XmlNodeType.Element) {
                _lastEmittedStartTagName = _name;
            }

            _tokenType = newTokenType;
            _name = null;
            _value = null;
            _attributes.Clear();
            _trailingSolidus = false;
            _incorrectDoctype = false;
        }

        private void EmitToken()
        {
            Debug.Assert(_tokenState == TokenState.Initialized);
            _tokenState = TokenState.Complete;
        }

        private void PrepareTextToken(string value)
        {
            _tokenState = TokenState.Uninitialized;
            if (_textTokenIsWhitespace && !Constants.IsSpace(value)) {
                _textTokenIsWhitespace = false;
            }
            _textToken.Append(value);
        }
        private void PrepareTextToken(char value)
        {
            _tokenState = TokenState.Uninitialized;
            if (_textTokenIsWhitespace && !Constants.IsSpaceCharacter(value)) {
                _textTokenIsWhitespace = false;
            }
            _textToken.Append(value);
        }

        #region Parsing
        private void CheckPermittedSlash()
        {
            if (_input.Peek() != '>'){
                OnParseError("Not a permitted slash: slash not at end of tag");
            } else if (_tokenType != XmlNodeType.Element) {
                OnParseError("Not a permitted slash: slash at end of tag but not a start tag");
            } else if (!Constants.IsVoidElement(_buffer.ToString())) {
                OnParseError("Not a permitted slash: slash at end of start tag but not a void element");
            }
        }

        private void ParseData()
        {
            if (ContentModel == ContentModel.PlainText) {
                // XXX: Optimization over the spec
                PrepareTextToken(_input.ReadToEnd());
                _currentParsingFunction = ParsingFunction.Eof;
            } else {
                int next;
                // http://www.whatwg.org/specs/web-apps/current-work/multipage/section-tokenisation.html#data-state
                while (true) {
                    next = _input.Read();
                    if (next < 0) {
                        _currentParsingFunction = ParsingFunction.Eof;
                        return;
                    }
                    switch (next) {
                    case '&':
                        if (ContentModel == ContentModel.Pcdata || ContentModel == ContentModel.Rcdata) {
                            _currentParsingFunction = ParsingFunction.EntityData;
                            return;
                        } else {
                            PrepareTextToken('&');
                        }
                        break;
                    case '-':
                        PrepareTextToken('-');
                        if (!_escapeFlag && (ContentModel == ContentModel.Rcdata || ContentModel == ContentModel.Cdata)) {
                            if (_textToken.Length >= 4 && _textToken.ToString(_textToken.Length - 4, 4).Equals("<!--", StringComparison.Ordinal)) {
                                _escapeFlag = true;
                            }
                        }
                        break;
                    case '<':
                        if ((ContentModel == ContentModel.Pcdata)
                            || (!_escapeFlag && (ContentModel == ContentModel.Rcdata || ContentModel == ContentModel.Cdata))) {
                            _currentParsingFunction = ParsingFunction.TagOpen;
                            return;
                        } else {
                            PrepareTextToken((char)next);
                        }
                        break;
                    case '>':
                        PrepareTextToken('>');
                        if (_escapeFlag && (ContentModel == ContentModel.Rcdata || ContentModel == ContentModel.Cdata)) {
                            if (_textToken.Length >= 4 && _textToken.ToString(_textToken.Length - 3, 3).Equals("-->", StringComparison.Ordinal)) {
                                _escapeFlag = false;
                            }
                        }
                        break;
                    default:
                        PrepareTextToken((char)next);
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
                PrepareTextToken('&');
            } else {
                PrepareTextToken(character);
            }
            _currentParsingFunction = ParsingFunction.Data;
        }

        private void ParseTagOpen()
        {
            // http://www.whatwg.org/specs/web-apps/current-work/multipage/section-tokenisation.html#tag-open
            if (ContentModel == ContentModel.Rcdata || ContentModel == ContentModel.Cdata) {
                if (_input.Peek() == '/') {
                    _input.Read();
                    _currentParsingFunction = ParsingFunction.CloseTagOpen;
                } else {
                    PrepareTextToken("<");
                    _currentParsingFunction = ParsingFunction.Data;
                }
            } else {
                Debug.Assert(ContentModel == ContentModel.Pcdata);
                int c = _input.Peek();
                if (c == '!') {
                    _input.Read();
                    _currentParsingFunction = ParsingFunction.MarkupDeclarationOpen;
                } else if (c == '/') {
                    _input.Read();
                    _currentParsingFunction = ParsingFunction.CloseTagOpen;
                } else if (('A' <= c && c <= 'Z') || ('a' <= c && c <= 'z')) {
                    InitToken(XmlNodeType.Element);
                    // XXX: draft says to consume the character and initialize the token name with it; we instead let ParseTagName consume the whole tag name
                    _currentParsingFunction = ParsingFunction.TagName;
                } else if (c == '>') {
                    _input.Read();
                    OnParseError("Unescaped <>");
                    PrepareTextToken("<>");
                    _currentParsingFunction = ParsingFunction.Data;
                } else if (c == '?') {
                    OnParseError("Bogus comment starting with <?");
                    _currentParsingFunction = ParsingFunction.BogusComment;
                } else {
                    OnParseError("Unescaped <");
                    PrepareTextToken("<");
                    _currentParsingFunction = ParsingFunction.Data;
                }
            }
        }

        private void ParseCloseTagOpen()
        {
            // http://www.whatwg.org/specs/web-apps/current-work/multipage/section-tokenisation.html#close1
            if (ContentModel == ContentModel.Rcdata || ContentModel == ContentModel.Cdata) {
                if (_lastEmittedStartTagName == null) {
                    Debug.Assert(_isFragmentParser);
                    PrepareTextToken("</");
                    _currentParsingFunction = ParsingFunction.Data;
                    return;
                }
                _input.Mark();
                foreach (char c1 in _lastEmittedStartTagName) {
                    int c2 = _input.Read();
                    if ('A' <= c2 && c2 <= 'Z') {
                        c2 += 0x0020;
                    }
                    if (c2 < 0 || c2 != c1) {
                        _input.ResetToMark();
                        PrepareTextToken("</");
                        _currentParsingFunction = ParsingFunction.Data;
                        return;
                    }
                }
                switch (_input.Peek()) {
                case '\t':
                case '\n':
                case '\v':
                case '\f':
                case ' ':
                case '>':
                case '/':
                case -1:
                    Debug.Assert(('A' <= _lastEmittedStartTagName[0] && _lastEmittedStartTagName[0] <= 'Z')
                        || ('a' <= _lastEmittedStartTagName[0] && _lastEmittedStartTagName[0] <= 'z'));
                    // XXX: We cannot be in RCDATA or CDATA content model flag without the last emitted start tag name starting with a letter.
                    // We've already read characters matching that tag name, so the end of the Close Tag Open State algorithm is predictable.
                    // So why bother forgetting those read characters and re-read them in the Tag Name State? Just jump to the Tag Name State.
                    _input.UnsetMark();
                    Debug.Assert(_buffer.Length == 0);
                    _buffer.Append(_lastEmittedStartTagName);
                    InitToken(XmlNodeType.EndElement);
                    _currentParsingFunction = ParsingFunction.TagName;
                    break;
                default:
                    _input.ResetToMark();
                    PrepareTextToken("</");
                    _currentParsingFunction = ParsingFunction.Data;
                    break;
                }
            } else {
                Debug.Assert(ContentModel == ContentModel.Pcdata);
                int c = _input.Peek();
                if (('A' <= c && c <= 'Z') || ('a' <= c && c <= 'z')) {
                    // XXX: draft says to consume the character and initialize the token name with it; we instead let ParseTagName consume the whole tag name
                    InitToken(XmlNodeType.EndElement);
                    _currentParsingFunction = ParsingFunction.TagName;
                } else if (c == '>') {
                    _input.Read();
                    OnParseError("Unescaped </>, all three characters ignored.");
                    _currentParsingFunction = ParsingFunction.Data;
                } else if (c < 0) {
                    OnParseError("Unexpected end of stream in end tag.");
                    PrepareTextToken("</");
                    _currentParsingFunction = ParsingFunction.Data;
                } else {
                    OnParseError("End tag name not beginning with a letter, treat as a bogus comment.");
                    _currentParsingFunction = ParsingFunction.BogusComment;
                }
            }
        }

        private void ParseTagName()
        {
            Debug.Assert(_tokenType == XmlNodeType.Element || _tokenType == XmlNodeType.EndElement);
            // http://www.whatwg.org/specs/web-apps/current-work/multipage/section-tokenisation.html#tag-name0
            while (_currentParsingFunction == ParsingFunction.TagName) {
                switch (_input.Peek()) {
                case '\t':
                case '\n':
                case '\v':
                case '\f':
                case ' ':
                    _input.Read();
                    _currentParsingFunction = ParsingFunction.BeforeAttributeName;
                    break;
                case '>':
                    _input.Read();
                    EmitToken();
                    _currentParsingFunction = ParsingFunction.Data;
                    break;
                case -1:
                    OnParseError("Unexpected end of stream in tag name");
                    EmitToken();
                    _currentParsingFunction = ParsingFunction.Data;
                    break;
                case '/':
                    _input.Read();
                    CheckPermittedSlash();
                    _currentParsingFunction = ParsingFunction.BeforeAttributeName;
                    break;
                default:
                    char c = (char)_input.Read();
                    if ('A' <= c && c <= 'Z') {
                        c += (char)0x0020;
                    }
                    _buffer.Append(c);
                    break;
                }
            }
            _name = _buffer.ToString();
            _buffer.Length = 0;
        }

        private void ParseBeforeAttributeName()
        {
            // http://www.whatwg.org/specs/web-apps/current-work/multipage/section-tokenisation.html#before
            while (_currentParsingFunction == ParsingFunction.BeforeAttributeName) {
                switch (_input.Peek()) {
                case '\t':
                case '\n':
                case '\v':
                case '\f':
                case ' ':
                    _input.Read();
                    break;
                case '>':
                    _input.Read();
                    EmitToken();
                    _currentParsingFunction = ParsingFunction.Data;
                    break;
                case '/':
                    _input.Read();
                    CheckPermittedSlash();
                    break;
                case -1:
                    OnParseError("Unexpected end of stream before attribute name");
                    EmitToken();
                    _currentParsingFunction = ParsingFunction.Data;
                    break;
                default:
                    // XXX: draft says to consume the character and initialize a new attribute's name with it; we instead let ParseAttributeName consume the whole attribute name
                    _currentParsingFunction = ParsingFunction.AttributeName;
                    break;
                }
            }
        }

        private void ParseAttributeName()
        {
            Debug.Assert(_tokenType == XmlNodeType.Element || _tokenType == XmlNodeType.EndElement);
            // http://www.whatwg.org/specs/web-apps/current-work/multipage/section-tokenisation.html#attribute1
            Debug.Assert(_buffer.Length == 0);
            while (_currentParsingFunction == ParsingFunction.AttributeName) {
                switch (_input.Peek()) {
                case '\t':
                case '\n':
                case '\v':
                case '\f':
                case ' ':
                    _input.Read();
                    _currentParsingFunction = ParsingFunction.AfterAttributeName;
                    break;
                case '=':
                    _input.Read();
                    _currentParsingFunction = ParsingFunction.BeforeAttributeValue;
                    break;
                case '>':
                    _input.Read();
                    EmitToken();
                    _currentParsingFunction = ParsingFunction.Data;
                    break;
                case '/':
                    _input.Read();
                    CheckPermittedSlash();
                    _currentParsingFunction = ParsingFunction.BeforeAttributeName;
                    break;
                case -1:
                    OnParseError("Unexpected end of stream in attribute name");
                    EmitToken();
                    _currentParsingFunction = ParsingFunction.Data;
                    break;
                default:
                    char c = (char)_input.Read();
                    if ('A' <= c && c <= 'Z') {
                        c += (char)0x0020;
                    }
                    _buffer.Append(c);
                    break;
                }
            }

            string attrName = _buffer.ToString();
            _attributes.Add(
                new Attribute(
                attrName,
                _attributes.Exists(
                    delegate(Attribute attr)
                    {
                        return String.Equals(attr.name, attrName, StringComparison.Ordinal);
                    }
                ),
                this));
            _buffer.Length = 0;
        }

        private void ParseAfterAttributeName()
        {
            // http://www.whatwg.org/specs/web-apps/current-work/multipage/section-tokenisation.html#after
            while (_currentParsingFunction == ParsingFunction.AfterAttributeName) {
                switch (_input.Peek()) {
                case '\t':
                case '\n':
                case '\v':
                case '\f':
                case ' ':
                    _input.Read();
                    break;
                case '=':
                    _input.Read();
                    _currentParsingFunction = ParsingFunction.BeforeAttributeValue;
                    break;
                case '>':
                    _input.Read();
                    EmitToken();
                    _currentParsingFunction = ParsingFunction.Data;
                    break;
                case '/':
                    _input.Read();
                    CheckPermittedSlash();
                    _currentParsingFunction = ParsingFunction.BeforeAttributeName;
                    break;
                case -1:
                    OnParseError("Unexpected end of stream after attribute name");
                    EmitToken();
                    _currentParsingFunction = ParsingFunction.Data;
                    break;
                default:
                    // XXX: draft says to consume the character and initialize a new attribute's name with it; we instead let ParseAttributeName consume the whole attribute name
                    _currentParsingFunction = ParsingFunction.AttributeName;
                    break;
                }
            }
        }

        private void ParseBeforeAttributeValue()
        {
            // http://www.whatwg.org/specs/web-apps/current-work/multipage/section-tokenisation.html#before0
            while (_currentParsingFunction == ParsingFunction.BeforeAttributeValue) {
                switch (_input.Peek()) {
                case '\t':
                case '\n':
                case '\v':
                case '\f':
                case ' ':
                    _input.Read();
                    break;
                case '"':
                    _attributes[_attributes.Count - 1].quoteChar = (char)_input.Read();
                    _currentParsingFunction = ParsingFunction.AttributeValueDoubleQuoted;
                    break;
                case '&':
                    Debug.Assert(_attributes[_attributes.Count - 1].quoteChar == ' ');
                    _currentParsingFunction = ParsingFunction.AttributeValueUnquoted;
                    break;
                case '\'':
                    _attributes[_attributes.Count - 1].quoteChar = (char)_input.Read();
                    _currentParsingFunction = ParsingFunction.AttributeValueSingleQuoted;
                    break;
                case '>':
                    _input.Read();
                    EmitToken();
                    _currentParsingFunction = ParsingFunction.Data;
                    break;
                case -1:
                    OnParseError("Unexpected end of stream before attribute value");
                    EmitToken();
                    _currentParsingFunction = ParsingFunction.Data;
                    break;
                default:
                    // TODO: move the following assertion at the beginning of each three ParseAttributeValueXXX method
                    Debug.Assert(String.IsNullOrEmpty(_attributes[_attributes.Count - 1].value));
                    // XXX: draft says to consume the character and appdn it to the (still empty) current attribute's value; we instead let ParseAttributeValueUnquoted consume the whole attribute value
                    Debug.Assert(_attributes[_attributes.Count - 1].quoteChar == ' ');
                    _currentParsingFunction = ParsingFunction.AttributeValueUnquoted;
                    break;
                }
            }
        }

        private void ParseAttributeValueDoubleQuoted()
        {
            Debug.Assert(String.IsNullOrEmpty(_attributes[_attributes.Count - 1].value));
            Debug.Assert(_attributes[_attributes.Count - 1].quoteChar == '"');

            // http://www.whatwg.org/specs/web-apps/current-work/multipage/section-tokenisation.html#attribute2
            Debug.Assert(_buffer.Length == 0);
            while (_currentParsingFunction == ParsingFunction.AttributeValueDoubleQuoted) {
                switch (_input.Peek()) {
                case '"':
                    _input.Read();
                    _attributes[_attributes.Count - 1].value = _buffer.ToString();
                    _currentParsingFunction = ParsingFunction.BeforeAttributeName;
                    break;
                case '&':
                    _input.Read();
                    string character = ConsumeEntity(true);
                    if (String.IsNullOrEmpty(character)) {
                        _buffer.Append('&');
                    } else {
                        _buffer.Append(character);
                    }
                    break;
                case -1:
                    OnParseError("Unexpected end of stream in attribute value");
                    _attributes[_attributes.Count - 1].value = _buffer.ToString();
                    EmitToken();
                    _currentParsingFunction = ParsingFunction.Data;
                    break;
                default:
                    _buffer.Append((char)_input.Read());
                    break;
                }
            }
            _buffer.Length = 0;
        }

        private void ParseAttributeValueSingleQuoted()
        {
            Debug.Assert(String.IsNullOrEmpty(_attributes[_attributes.Count - 1].value));
            Debug.Assert(_attributes[_attributes.Count - 1].quoteChar == '\'');

            // http://www.whatwg.org/specs/web-apps/current-work/multipage/section-tokenisation.html#attribute3
            Debug.Assert(_buffer.Length == 0);
            while (_currentParsingFunction == ParsingFunction.AttributeValueSingleQuoted) {
                switch (_input.Peek()) {
                case '\'':
                    _input.Read();
                    _attributes[_attributes.Count - 1].value = _buffer.ToString();
                    _currentParsingFunction = ParsingFunction.BeforeAttributeName;
                    break;
                case '&':
                    _input.Read();
                    string character = ConsumeEntity(true);
                    if (String.IsNullOrEmpty(character)) {
                        _buffer.Append('&');
                    } else {
                        _buffer.Append(character);
                    }
                    break;
                case -1:
                    OnParseError("Unexpected end of stream in attribute value");
                    _attributes[_attributes.Count - 1].value = _buffer.ToString();
                    EmitToken();
                    _currentParsingFunction = ParsingFunction.Data;
                    break;
                default:
                    _buffer.Append((char)_input.Read());
                    break;
                }
            }
            _buffer.Length = 0;
        }

        private void ParseAttributeValueUnquoted()
        {
            Debug.Assert(String.IsNullOrEmpty(_attributes[_attributes.Count - 1].value));
            Debug.Assert(_attributes[_attributes.Count - 1].quoteChar == ' ');

            // http://www.whatwg.org/specs/web-apps/current-work/multipage/section-tokenisation.html#attribute4
            Debug.Assert(_buffer.Length == 0);
            while (_currentParsingFunction == ParsingFunction.AttributeValueUnquoted) {
                switch (_input.Peek()) {
                case '\t':
                case '\n':
                case '\v':
                case '\f':
                case ' ':
                    _input.Read();
                    _attributes[_attributes.Count - 1].value = _buffer.ToString();
                    _currentParsingFunction = ParsingFunction.BeforeAttributeName;
                    break;
                case '&':
                    _input.Read();
                    string character = ConsumeEntity(true);
                    if (String.IsNullOrEmpty(character)) {
                        _buffer.Append('&');
                    } else {
                        _buffer.Append(character);
                    }
                    break;
                case '>':
                    _input.Read();
                    _attributes[_attributes.Count - 1].value = _buffer.ToString();
                    EmitToken();
                    _currentParsingFunction = ParsingFunction.Data;
                    break;
                case -1:
                    OnParseError("Unexpected end of stream in attribute value");
                    _attributes[_attributes.Count - 1].value = _buffer.ToString();
                    EmitToken();
                    _currentParsingFunction = ParsingFunction.Data;
                    break;
                default:
                    _buffer.Append((char)_input.Read());
                    break;
                }
            }
            _buffer.Length = 0;
        }

        private void ParseBogusComment()
        {
            // http://www.whatwg.org/specs/web-apps/current-work/multipage/section-tokenisation.html#bogus
            Debug.Assert(ContentModel == ContentModel.Pcdata);

            InitToken(XmlNodeType.Comment);
            Debug.Assert(_buffer.Length == 0);
            for (int c = _input.Peek(); c >= 0 && c != '>'; c = _input.Peek()) {
                _buffer.Append((char)_input.Read());
            }
            _value = _buffer.ToString();
            _buffer.Length = 0;
            if (_input.Peek() == '>') {
                _input.Read();
            }
            EmitToken();
            _currentParsingFunction = ParsingFunction.Data;
        }

        private void ParseMarkupDeclarationOpen()
        {
            // http://www.whatwg.org/specs/web-apps/current-work/multipage/section-tokenisation.html#markup
            Debug.Assert(ContentModel == ContentModel.Pcdata);

            _input.Mark();
            switch (_input.Peek()) {
            case '-':
                _input.Read();
                if (_input.Read() == '-') {
                    _input.UnsetMark();
                    InitToken(XmlNodeType.Comment);
                    _currentParsingFunction = ParsingFunction.CommentStart;
                    return;
                } else {
                    goto default;
                }
            case 'D':
            case 'd':
                _input.Read();
                foreach (char c1 in "octype") {
                    int c2 = _input.Read();
                    if ('A' <= c2 && c2 <= 'Z') {
                        c2 += 0x0020;
                    }
                    if (c2 < 0 || c1 != c2) {
                        goto default;
                    }
                }
                _input.UnsetMark();
                _currentParsingFunction = ParsingFunction.Doctype;
                break;
            default:
                _input.ResetToMark();
                OnParseError("Bogus comment");
                _currentParsingFunction = ParsingFunction.BogusComment;
                break;
            }
        }

        private void ParseCommentStart()
        {
            // http://www.whatwg.org/specs/web-apps/current-work/multipage/section-tokenisation.html#comment0
            Debug.Assert(_buffer.Length == 0);
            switch (_input.Peek()) {
            case '-':
                _input.Read();
                _currentParsingFunction = ParsingFunction.CommentStartDash;
                break;
            case '>':
                _input.Read();
                OnParseError("Invalid comment <!-->");
                Debug.Assert(_buffer.Length == 0);
                _value = _buffer.ToString();
                _buffer.Length = 0;
                EmitToken();
                _currentParsingFunction = ParsingFunction.Data;
                break;
            case -1:
                OnParseError("Unexpected end of stream at start of comment");
                Debug.Assert(_buffer.Length == 0);
                _value = _buffer.ToString();
                _buffer.Length = 0;
                EmitToken();
                _currentParsingFunction = ParsingFunction.Data;
                break;
            default:
                _buffer.Append((char)_input.Read());
                _currentParsingFunction = ParsingFunction.Comment;
                break;
            }
        }

        private void ParseCommentStartDash()
        {
            // http://www.whatwg.org/specs/web-apps/current-work/multipage/section-tokenisation.html#comment1
            Debug.Assert(_buffer.Length == 0);
            switch (_input.Peek()) {
            case '-':
                _input.Read();
                _currentParsingFunction = ParsingFunction.CommentEnd;
                break;
            case '>':
                _input.Read();
                OnParseError("Invalid comment <!--->");
                Debug.Assert(_buffer.Length == 0);
                _value = _buffer.ToString();
                _buffer.Length = 0;
                EmitToken();
                _currentParsingFunction = ParsingFunction.Data;
                break;
            case -1:
                OnParseError("Unexpected end of stream at start of comment");
                Debug.Assert(_buffer.Length == 0);
                _value = _buffer.ToString();
                _buffer.Length = 0;
                EmitToken();
                _currentParsingFunction = ParsingFunction.Data;
                break;
            default:
                _buffer.Append('-');
                _buffer.Append((char)_input.Read());
                _currentParsingFunction = ParsingFunction.Comment;
                break;
            }
        }

        private void ParseComment()
        {
            // http://www.whatwg.org/specs/web-apps/current-work/multipage/section-tokenisation.html#comment1
            while (_currentParsingFunction == ParsingFunction.Comment) {
                switch (_input.Peek()) {
                case '-':
                    _input.Read();
                    _currentParsingFunction = ParsingFunction.CommentEndDash;
                    break;
                case -1:
                    OnParseError("Unexpected end of stream in comment");
                    _value = _buffer.ToString();
                    _buffer.Length = 0;
                    EmitToken();
                    _currentParsingFunction = ParsingFunction.Data;
                    break;
                default:
                    _buffer.Append((char)_input.Read());
                    break;
                }
            }
        }

        private void ParseCommentEndDash()
        {
            // http://www.whatwg.org/specs/web-apps/current-work/multipage/section-tokenisation.html#comment2
            switch (_input.Peek()) {
            case '-':
                _input.Read();
                _currentParsingFunction = ParsingFunction.CommentEnd;
                break;
            case -1:
                OnParseError("Unexpected end of stream in comment");
                _value = _buffer.ToString();
                _buffer.Length = 0;
                EmitToken();
                _currentParsingFunction = ParsingFunction.Data;
                break;
            default:
                _buffer.Append('-');
                _buffer.Append((char)_input.Read());
                _currentParsingFunction = ParsingFunction.Comment;
                break;
            }
        }

        private void ParseCommentEnd()
        {
            // http://www.whatwg.org/specs/web-apps/current-work/multipage/section-tokenisation.html#comment3
            while (_currentParsingFunction == ParsingFunction.CommentEnd) {
                switch (_input.Peek()) {
                case '>':
                    _input.Read();
                    _value = _buffer.ToString();
                    _buffer.Length = 0;
                    EmitToken();
                    _currentParsingFunction = ParsingFunction.Data;
                    break;
                case '-':
                    _input.Read();
                    OnParseError("Double-dash (or more) in comment");
                    _buffer.Append('-');
                    break;
                case -1:
                    OnParseError("Unexpected end of stream at end of comment");
                    _value = _buffer.ToString();
                    _buffer.Length = 0;
                    EmitToken();
                    _currentParsingFunction = ParsingFunction.Data;
                    break;
                default:
                    OnParseError("Double-dash (or more) in comment");
                    _buffer.Append("--");
                    _buffer.Append((char)_input.Read());
                    _currentParsingFunction = ParsingFunction.Comment;
                    break;
                }
            }
        }

        private void ParseDoctype()
        {
            // http://www.whatwg.org/specs/web-apps/current-work/multipage/section-tokenisation.html#doctype0
            switch (_input.Peek()) {
            case '\t':
            case '\n':
            case '\v':
            case '\f':
            case ' ':
                _input.Read();
                break;
            default:
                OnParseError("Unexpected character in DOCTYPE");
                break;
            }
            _currentParsingFunction = ParsingFunction.BeforeDoctypeName;
        }

        private void ParseBeforeDoctypeName()
        {
            Debug.Assert(_buffer.Length == 0);
            // http://www.whatwg.org/specs/web-apps/current-work/multipage/section-tokenisation.html#before1
            while (_currentParsingFunction == ParsingFunction.BeforeDoctypeName) {
                switch (_input.Peek()) {
                case '\t':
                case '\n':
                case '\v':
                case '\f':
                case ' ':
                    _input.Read();
                    break;
                case '>':
                    _input.Read();
                    OnParseError("Unexpected end of DOCTYPE before DOCTYPE name");
                    InitToken(XmlNodeType.DocumentType);
                    _incorrectDoctype = true;
                    EmitToken();
                    _currentParsingFunction = ParsingFunction.Data;
                    break;
                case -1:
                    OnParseError("Unexpected end of stream before DOCTYPE name");
                    InitToken(XmlNodeType.DocumentType);
                    _incorrectDoctype = true;
                    EmitToken();
                    _currentParsingFunction = ParsingFunction.Data;
                    break;
                default:
                    InitToken(XmlNodeType.DocumentType);
                    Debug.Assert(_buffer.Length == 0);
                    _buffer.Append((char)_input.Read());
                    _currentParsingFunction = ParsingFunction.DoctypeName;
                    break;
                }
            }
        }

        private void ParseDoctypeName()
        {
            Debug.Assert(_buffer.Length == 1);
            // http://www.whatwg.org/specs/web-apps/current-work/multipage/section-tokenisation.html#doctype1
            while (_currentParsingFunction == ParsingFunction.DoctypeName) {
                switch (_input.Peek()) {
                case '\t':
                case '\n':
                case '\v':
                case '\f':
                case ' ':
                    _input.Read();
                    _currentParsingFunction = ParsingFunction.AfterDoctypeName;
                    break;
                case '>':
                    _input.Read();
                    EmitToken();
                    _currentParsingFunction = ParsingFunction.Data;
                    break;
                case -1:
                    OnParseError("Unexpected end of stream in DOCTYPE name");
                    _incorrectDoctype = true;
                    EmitToken();
                    _currentParsingFunction = ParsingFunction.Data;
                    break;
                default:
                    _buffer.Append((char)_input.Read());
                    break;
                }
            }
            _name = _buffer.ToString();
            _buffer.Length = 0;
        }

        private void ParseAfterDoctypeName()
        {
            // http://www.whatwg.org/specs/web-apps/current-work/multipage/section-tokenisation.html#after0
            while (_currentParsingFunction == ParsingFunction.AfterDoctypeName) {
                switch (_input.Peek()) {
                case '\t':
                case '\n':
                case '\v':
                case '\f':
                case ' ':
                    _input.Read();
                    break;
                case '>':
                    _input.Read();
                    EmitToken();
                    _currentParsingFunction = ParsingFunction.Data;
                    break;
                case -1:
                    OnParseError("Unexpected end of stream in DOCTYPE name");
                    _incorrectDoctype = true;
                    EmitToken();
                    _currentParsingFunction = ParsingFunction.Data;
                    break;
                default:
                    _input.Mark();
                    switch (_input.Read()) {
                    case 'P':
                    case 'p':
                        foreach (char c1 in "ublic") {
                            int c2 = _input.Read();
                            if ('A' <= c2 && c2 <= 'Z') {
                                c2 += 0x0020;
                            }
                            if (c2 < 0 || c1 != c2) {
                                goto default;
                            }
                        }
                        _input.UnsetMark();
                        _currentParsingFunction = ParsingFunction.BeforeDoctypePublicId;
                        break;
                    case 'S':
                    case 's':
                        foreach (char c1 in "ystem") {
                            int c2 = _input.Read();
                            if ('A' <= c2 && c2 <= 'Z') {
                                c2 += 0x0020;
                            }
                            if (c2 < 0 || c1 != c2) {
                                goto default;
                            }
                        }
                        _input.UnsetMark();
                        _currentParsingFunction = ParsingFunction.BeforeDoctypeSystemId;
                        break;
                    default:
                        _input.ResetToMark();
                        _input.Read();
                        OnParseError("Unexpected character after DOCTYPE name");
                        _currentParsingFunction = ParsingFunction.BogusDoctype;
                        break;
                    }
                    break;
                }
            }
        }

        private void ParseBeforeDoctypePublicId()
        {
            // http://www.whatwg.org/specs/web-apps/current-work/multipage/section-tokenisation.html#before2
            while (_currentParsingFunction == ParsingFunction.BeforeDoctypePublicId) {
                switch (_input.Peek()) {
                case '\t':
                case '\n':
                case '\v':
                case '\f':
                case ' ':
                    _input.Read();
                    break;
                case '"':
                    _input.Read();
                    _currentParsingFunction = ParsingFunction.DoctypePublicIdDoubleQuoted;
                    break;
                case '\'':
                    _input.Read();
                    _currentParsingFunction = ParsingFunction.DoctypePublicIdSingleQuoted;
                    break;
                case '>':
                    _input.Read();
                    OnParseError("Unexpected end of DOCTYPE before public identifier");
                    _incorrectDoctype = true;
                    EmitToken();
                    _currentParsingFunction = ParsingFunction.Data;
                    break;
                case -1:
                    OnParseError("Unexpected end of stream before DOCTYPE public identifier");
                    _incorrectDoctype = true;
                    EmitToken();
                    _currentParsingFunction = ParsingFunction.Data;
                    break;
                default:
                    _input.Read();
                    OnParseError("Unexpected character before DOCTYPE public identifier");
                    _currentParsingFunction = ParsingFunction.BogusDoctype;
                    break;
                }
            }
        }

        private void ParseDoctypePublicIdDoubleQuoted()
        {
            // http://www.whatwg.org/specs/web-apps/current-work/multipage/section-tokenisation.html#doctype2
            Debug.Assert(_buffer.Length == 0);
            while (_currentParsingFunction == ParsingFunction.DoctypePublicIdDoubleQuoted) {
                switch (_input.Peek()) {
                case '"':
                    _input.Read();
                    _currentParsingFunction = ParsingFunction.AfterDoctypePublicId;
                    break;
                case -1:
                    OnParseError("Unexpected end of stream in DOCTYPE public identifier");
                    _incorrectDoctype = true;
                    EmitToken();
                    _currentParsingFunction = ParsingFunction.Data;
                    break;
                default:
                    _buffer.Append((char)_input.Read());
                    break;
                }
            }
            Attribute attr = new Attribute("PUBLIC", this);
            attr.value = _buffer.ToString();
            _buffer.Length = 0;
            attr.quoteChar = '"';
            Debug.Assert(_attributes.Count == 0);
            _attributes.Add(attr);
        }

        private void ParseDoctypePublicIdSingleQuoted()
        {
            // http://www.whatwg.org/specs/web-apps/current-work/multipage/section-tokenisation.html#doctype3
            Debug.Assert(_buffer.Length == 0);
            while (_currentParsingFunction == ParsingFunction.DoctypePublicIdSingleQuoted) {
                switch (_input.Peek()) {
                case '\'':
                    _input.Read();
                    _currentParsingFunction = ParsingFunction.AfterDoctypePublicId;
                    break;
                case -1:
                    OnParseError("Unexpected end of stream in DOCTYPE public identifier");
                    _incorrectDoctype = true;
                    EmitToken();
                    _currentParsingFunction = ParsingFunction.Data;
                    break;
                default:
                    _buffer.Append((char)_input.Read());
                    break;
                }
            }
            Attribute attr = new Attribute("PUBLIC", this);
            attr.value = _buffer.ToString();
            _buffer.Length = 0;
            attr.quoteChar = '\'';
            Debug.Assert(_attributes.Count == 0);
            _attributes.Add(attr);
        }

        private void ParseAfterDoctypePublicId()
        {
            // http://www.whatwg.org/specs/web-apps/current-work/multipage/section-tokenisation.html#after1
            while (_currentParsingFunction == ParsingFunction.AfterDoctypePublicId) {
                switch (_input.Peek()) {
                case '\t':
                case '\n':
                case '\v':
                case '\f':
                case ' ':
                    _input.Read();
                    break;
                case '"':
                    _input.Read();
                    _currentParsingFunction = ParsingFunction.DoctypeSystemIdDoubleQuoted;
                    break;
                case '\'':
                    _input.Read();
                    _currentParsingFunction = ParsingFunction.DoctypeSystemIdSingleQuoted;
                    break;
                case '>':
                    _input.Read();
                    EmitToken();
                    _currentParsingFunction = ParsingFunction.Data;
                    break;
                case -1:
                    OnParseError("Unexpected end of stream after DOCTYPE public identifier");
                    _incorrectDoctype = true;
                    EmitToken();
                    _currentParsingFunction = ParsingFunction.Data;
                    break;
                default:
                    _input.Read();
                    OnParseError("Unexpected character after DOCTYPE public identifier");
                    _currentParsingFunction = ParsingFunction.BogusDoctype;
                    break;
                }
            }
        }

        private void ParseBeforeDoctypeSystemId()
        {
            // http://www.whatwg.org/specs/web-apps/current-work/multipage/section-tokenisation.html#before3
            while (_currentParsingFunction == ParsingFunction.BeforeDoctypeSystemId) {
                switch (_input.Peek()) {
                case '\t':
                case '\n':
                case '\v':
                case '\f':
                case ' ':
                    _input.Read();
                    break;
                case '"':
                    _input.Read();
                    _currentParsingFunction = ParsingFunction.DoctypeSystemIdDoubleQuoted;
                    break;
                case '\'':
                    _input.Read();
                    _currentParsingFunction = ParsingFunction.DoctypeSystemIdSingleQuoted;
                    break;
                case '>':
                    _input.Read();
                    OnParseError("Unexpected end of DOCTYPE before system identifier");
                    _incorrectDoctype = true;
                    EmitToken();
                    _currentParsingFunction = ParsingFunction.Data;
                    break;
                case -1:
                    OnParseError("Unexpected end of stream before fter DOCTYPE system identifier");
                    _incorrectDoctype = true;
                    EmitToken();
                    _currentParsingFunction = ParsingFunction.Data;
                    break;
                default:
                    _input.Read();
                    OnParseError("Unexpected character before DOCTYPE system identifier");
                    _currentParsingFunction = ParsingFunction.BogusDoctype;
                    break;
                }
            }
        }

        private void ParseDoctypeSystemIdDoubleQuoted()
        {
            // http://www.whatwg.org/specs/web-apps/current-work/multipage/section-tokenisation.html#doctype4
            Debug.Assert(_buffer.Length == 0);
            while (_currentParsingFunction == ParsingFunction.DoctypeSystemIdDoubleQuoted) {
                switch (_input.Peek()) {
                case '"':
                    _input.Read();
                    _currentParsingFunction = ParsingFunction.AfterDoctypeSystemId;
                    break;
                case -1:
                    OnParseError("Unexpected end of stream in DOCTYPE system identifier");
                    _incorrectDoctype = true;
                    EmitToken();
                    _currentParsingFunction = ParsingFunction.Data;
                    break;
                default:
                    _buffer.Append((char)_input.Read());
                    break;
                }
            }
            Attribute attr = new Attribute("SYSTEM", this);
            attr.value = _buffer.ToString();
            _buffer.Length = 0;
            attr.quoteChar = '"';
            Debug.Assert(_attributes.Count == 0 || (_attributes.Count == 1 && _attributes[0].name == "PUBLIC"));
            _attributes.Add(attr);
        }

        private void ParseDoctypeSystemIdSingleQuoted()
        {
            // http://www.whatwg.org/specs/web-apps/current-work/multipage/section-tokenisation.html#doctype5
            Debug.Assert(_buffer.Length == 0);
            while (_currentParsingFunction == ParsingFunction.DoctypeSystemIdSingleQuoted) {
                switch (_input.Peek()) {
                case '\'':
                    _input.Read();
                    _currentParsingFunction = ParsingFunction.AfterDoctypeSystemId;
                    break;
                case -1:
                    OnParseError("Unexpected end of stream in DOCTYPE system identifier");
                    _incorrectDoctype = true;
                    EmitToken();
                    _currentParsingFunction = ParsingFunction.Data;
                    break;
                default:
                    _buffer.Append((char)_input.Read());
                    break;
                }
            }
            Attribute attr = new Attribute("SYSTEM", this);
            attr.value = _buffer.ToString();
            _buffer.Length = 0;
            attr.quoteChar = '\'';
            Debug.Assert(_attributes.Count == 0 || (_attributes.Count == 1 && _attributes[0].name == "PUBLIC"));
            _attributes.Add(attr);
        }

        private void ParseAfterDoctypeSystemId()
        {
            // http://www.whatwg.org/specs/web-apps/current-work/multipage/section-tokenisation.html#after2
            while (_currentParsingFunction == ParsingFunction.AfterDoctypeSystemId) {
                switch (_input.Peek()) {
                case '\t':
                case '\n':
                case '\v':
                case '\f':
                case ' ':
                    _input.Read();
                    break;
                case '>':
                    _input.Read();
                    EmitToken();
                    _currentParsingFunction = ParsingFunction.Data;
                    break;
                case -1:
                    OnParseError("Unexpected end of stream after DOCTYPE system identifier");
                    _incorrectDoctype = true;
                    EmitToken();
                    _currentParsingFunction = ParsingFunction.Data;
                    break;
                default:
                    _input.Read();
                    OnParseError("Unexpected character after DOCTYPE system identifier");
                    _currentParsingFunction = ParsingFunction.BogusDoctype;
                    break;
                }
            }
        }

        private void ParseBogusDoctype()
        {
            // http://www.whatwg.org/specs/web-apps/current-work/multipage/section-tokenisation.html#bogus0
            while (_currentParsingFunction == ParsingFunction.BogusDoctype) {
                switch (_input.Peek()) {
                case '>':
                    _input.Read();
                    _incorrectDoctype = true;
                    EmitToken();
                    _currentParsingFunction = ParsingFunction.Data;
                    break;
                case -1:
                    OnParseError("Unexpected end of stream in bogus DOCTYPE");
                    _incorrectDoctype = true;
                    EmitToken();
                    _currentParsingFunction = ParsingFunction.Data;
                    break;
                default:
                    _input.Read();
                    break;
                }
            }
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
            EntityNameCharacter foundChar = null;
            // Just for the ParseError below:
            StringBuilder entityName = new StringBuilder(HtmlEntities.LonguestEntityNameLength);
            EntityNameCharacter entityNameCharacter = EntityNameCharacter.Root;
            do {
                int c = _input.Read();
                if (c < 0) {
                    OnParseError("Unexpected end of file in character entity");
                    _input.ResetToMark();
                    break;
                } else if (c == ';') {
                    if (foundChar == null) {
                        OnParseError(String.Concat("Unknown named entity: ", entityName.ToString()));
                    }
                    _input.ResetToMark();
                    break;
                } else {
                    EntityNameCharacter tmp;
                    if (!entityNameCharacter.TryGetEntityNameCharacter((char) c, out tmp)) {
                        if (foundChar != null) {
                            OnParseError("Entity does not end with a semi-colon");
                        } else {
                            OnParseError(String.Concat("Unescaped & or unknown named entity: ", entityName.ToString()));
                        }
                        _input.ResetToMark();
                        break;
                    }
                    entityNameCharacter = tmp;
                    entityName.Append((char) c);
                    c = _input.Peek();
                    if (entityNameCharacter.HasCodepoint
                        && (c == ';' || entityNameCharacter.IsMissingSemiColonRecoverable)) {
                        foundChar = entityNameCharacter;
                        // If in attribute value and next char is not [0-9A-Za-z],
                        // we want to be able to reset to just after the '&'.
                        // Otherwise, we consume the entity and mark the input again for lookup.
                        if (!inAttributeValue
                            || !(('0' <= c && c <= '9') || ('A' <= c && c <= 'Z') || ('a' <= c && c <= 'z'))) {
                            _input.UnsetMark();
                            _input.Mark();
                        }
                    }
                }
            } while (true);

            if (foundChar == null) {
                return null;
            }

            int nextChar = _input.Peek();
            if (nextChar == ';') {
                _input.Read();
            } else if (inAttributeValue) {
                if ((('0' <= nextChar && nextChar <= '9')
                        || ('A' <= nextChar && nextChar <= 'Z')
                        || ('a' <= nextChar && nextChar <= 'z'))) {
                    return null;
                }
            }

            return Char.ConvertFromUtf32(foundChar.Codepoint);
        }


        private class EntityNameCharacter
        {
            private int? _codepoint;
            private bool _missingSemiColonRecoverable;
            private Dictionary<char, EntityNameCharacter> _nextChars = new Dictionary<char, EntityNameCharacter>();

            private EntityNameCharacter() { }

            public int Codepoint { get { return _codepoint.Value; } }
            public bool HasCodepoint { get { return _codepoint.HasValue; } }
            public bool IsMissingSemiColonRecoverable { get { return _missingSemiColonRecoverable; } }
            public EntityNameCharacter this[char nextChar]
            {
                get
                {
                    return _nextChars[nextChar];
                }
            }
            public bool TryGetEntityNameCharacter(char nextChar, out EntityNameCharacter entityNameCharacter)
            {
                return _nextChars.TryGetValue(nextChar, out entityNameCharacter);
            }

            private static EntityNameCharacter _root;
            internal static EntityNameCharacter Root
            {
                get
                {
                    if (_root == null) {
                        _root = new EntityNameCharacter();
                        foreach (string entityName in HtmlEntities.EntityNames) {
                            EntityNameCharacter entityNameCharacter = null;
                            Dictionary<char, EntityNameCharacter> nextChars = _root._nextChars;
                            foreach (char c in entityName) {
                                if (!nextChars.TryGetValue(c, out entityNameCharacter)) {
                                    entityNameCharacter = new EntityNameCharacter();
                                    nextChars.Add(c, entityNameCharacter);
                                }
                                nextChars = entityNameCharacter._nextChars;
                            }
                            entityNameCharacter._codepoint = HtmlEntities.GetChar(entityName);
                            entityNameCharacter._missingSemiColonRecoverable = HtmlEntities.IsMissingSemiColonRecoverable(entityName);
                        }
                    }
                    return _root;
                }
            }
        }
        #endregion

        #endregion
    }
}
