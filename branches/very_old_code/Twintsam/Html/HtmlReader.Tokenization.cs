using System;
using System.Collections.Generic;
using System.Text;
using System.Xml;
using Twintsam.Properties;
using System.Diagnostics;

namespace Twintsam.Html
{
    public partial class HtmlReader
    {
        private ContentModel _contentModel;

        public ContentModel ContentModel
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

        /// <summary>
        /// A parsing function, roughly corresponding to a token to be parsed.
        /// </summary>
        /// <returns><see langword="true"/> if a token has been produced, <see langword="false"/> if no token is ready yet.</returns>
        private delegate bool ParsingFunction();

        private ParsingFunction _currentParsingFunction;

        private XmlNodeType _tokenType;
        private string _name;
        private string _value;
        private List<Attribute> _attributes = new List<Attribute>();
        private bool _doctypeInError;

        private string _lastEmittedStartTagName;

        private struct Attribute
        {
            public string name;
            public string value;
            public char quoteChar;
        }

        /// <summary>
        /// Initialises a new token with the given type.
        /// </summary>
        /// <param name="tokenType">The type of the token, should be on of <see cref="XmlNodeType.DocumentType"/>, <see cref="XmlNodeType.Element"/>, <see cref="XmlNodeType.EndElement"/>, <see cref="XmlNodeType.Text"/>, <see cref="XmlNodeType.Whitespace"/> or <see cref="XmlNodeType.Comment"/></param>
        private void InitToken(XmlNodeType tokenType)
        {
            Debug.Assert(tokenType == XmlNodeType.DocumentType
                || tokenType == XmlNodeType.Element
                || tokenType == XmlNodeType.EndElement
                || tokenType == XmlNodeType.Text
                || tokenType == XmlNodeType.Whitespace
                || tokenType == XmlNodeType.Comment);

            if (_tokenType == XmlNodeType.Element) {
                _lastEmittedStartTagName = _name;
            } else if (tokenType != XmlNodeType.Text && tokenType == XmlNodeType.Whitespace) {
                // _lastEmittedStartTagName is for CDATA and RCDATA,
                // which can only contain text tokens, so clear it if
                // a non-text token is produced.
                _lastEmittedStartTagName = null;
            }

            _tokenType = tokenType;
            _name = null;
            _value = null;
            _attributes.Clear();
            _doctypeInError = false;
        }

        private void InitTextToken(string value)
        {
            InitToken(Constants.IsSpace(value) ? XmlNodeType.Whitespace : XmlNodeType.Text);
            _value = value;
        }

        /// <summary>
        /// Reads the next token from the stream.
        /// </summary>
        /// <returns><see langword="true"/> if the next token was read successfully; <see langword="false"/> if there are no more tokens to read.</returns>
        /// <remarks>
        /// Calls the <see cref="_currentParsingFunction"/> until it returns <see langword="true"/>.
        /// </remarks>
        private bool ParseToken()
        {
            if (EndOfStream) {
                // buffer consumed and reader is at EOF => we reached EOF
                return false;
            }
            bool newToken = false;
            while (!newToken && !EndOfStream) {
                newToken = _currentParsingFunction();
            }
            return newToken;
        }

        // http://www.whatwg.org/specs/web-apps/current-work/#data-state
        private bool ParseData()
        {
            switch (ContentModel) {
            case ContentModel.Pcdata:
                return ParsePcdata();
            case ContentModel.Rcdata:
            case ContentModel.Cdata:
                return ParseRcdataOrCdata();
            case ContentModel.PlainText:
            default:
                string s = this.EatCharsToEnd();
                if (s.Length > 0) {
                    InitTextToken(s);
                    return true;
                } else {
                    return false;
                }
            }
        }

        private bool ParseRcdataOrCdata()
        {
            Predicate<char> condition;
            switch (ContentModel) {
            case ContentModel.Rcdata:
                condition = delegate(char c) { return c != '<' && c != '&'; };
                break;
            case ContentModel.Cdata:
                condition = delegate(char c) { return c != '<'; };
                break;
            default:
                throw new InvalidOperationException();
            }

            StringBuilder sb = new StringBuilder();
            while (_currentParsingFunction == ParseData && !EndOfStream) {
                // http://www.whatwg.org/specs/web-apps/current-work/#data-state
                EatChars(sb, condition);
                // http://www.whatwg.org/specs/web-apps/current-work/#tag-open
                char next = EatNextInputChar();
                if (next == '&') {
                    Debug.Assert(ContentModel == ContentModel.Rcdata);
                    // http://www.whatwg.org/specs/web-apps/current-work/#entity
                    string entityValue = EatEntity();
                    if (String.IsNullOrEmpty(entityValue)) {
                        sb.Append('&');
                    } else {
                        sb.Append(entityValue);
                    }
                } else if (next == '<') {
                    // http://www.whatwg.org/specs/web-apps/current-work/#close1
                    if (NextInputChar == '/') {
                        EatChars(1); // Eat SOLIDUS
                        char c = PeekChar(_lastEmittedStartTagName.Length);
                        if (String.Equals(PeekChars(_lastEmittedStartTagName.Length), _lastEmittedStartTagName, StringComparison.InvariantCultureIgnoreCase)
                            && (Constants.IsSpaceCharacter(c) || c == '>' || c == '/' || c == '<' || c == EOF_CHAR)) {
                            _currentParsingFunction = ParseEndTag;
                        } else {
                            OnParseError("Unescaped </ in CDATA or RCDATA");
                            sb.Append("</");
                        }
                    } else {
                        if (ContentModel == ContentModel.Rcdata) {
                            // NEW: Unescaped < in RCDATA
                            OnParseError("Unescaped < in RCDATA");
                        }
                        sb.Append('<');
                    }
                }
            }
            if (sb.Length > 0) {
                InitTextToken(sb.ToString());
                return true;
            } else {
                return false;
            }
        }

        private bool ParsePcdata()
        {
            StringBuilder sb = new StringBuilder();
            while (_currentParsingFunction == ParseData && !EndOfStream) {
                // http://www.whatwg.org/specs/web-apps/current-work/#data-state
                EatChars(sb, delegate(char c) { return c != '<' && c != '&'; });
                // http://www.whatwg.org/specs/web-apps/current-work/#tag-open
                char next = EatNextInputChar();
                if (next == '&') {
                    // http://www.whatwg.org/specs/web-apps/current-work/#entity
                    string entityValue = EatEntity();
                    if (String.IsNullOrEmpty(entityValue)) {
                        sb.Append('&');
                    } else {
                        sb.Append(entityValue);
                    }
                } else if (next == '<') {
                    switch (NextInputChar) {
                    case '!':
                        EatChars(1); // Eat EXCLAMATION MARK
                        _currentParsingFunction = ParseMarkupDeclaration;
                        break;
                    case '/':
                        EatChars(1); // Eat SOLIDUS
                        // http://www.whatwg.org/specs/web-apps/current-work/#close1
                        switch (NextInputChar) {
                        case '>':
                            EatChars(1); // Eat GREATER-THAN-SIGN
                            OnParseError("End tag without a name");
                            break;
                        case EOF_CHAR:
                            OnParseError("Unexpected end of file in PCDATA");
                            sb.Append("</");
                            break;
                        default:
                            next = NextInputChar;
                            if (('a' <= next && next <= 'z') || ('A' <= next && next <= 'Z')) {
                                _currentParsingFunction = ParseEndTag;
                            } else {
                                _currentParsingFunction = ParseBogusComment;
                            }
                            break;
                        }
                        break;
                    case '>':
                        EatChars(1); // Eat GREATER-THAN SIGN
                        OnParseError("Unescaped <> in PCDATA");
                        sb.Append("<>");
                        break;
                    case '?':
                        _currentParsingFunction = ParseBogusComment;
                        break;
                    default:
                        next = NextInputChar;
                        if (('a' <= next && next <= 'z') || ('A' <= next && next <= 'Z')) {
                            _currentParsingFunction = ParseStartTag;
                        } else {
                            OnParseError("Unescaped < in PCDATA");
                            sb.Append('<');
                        }
                        break;
                    }
                }
            }
            if (sb.Length > 0) {
                InitTextToken(sb.ToString());
                return true;
            } else {
                return false;
            }
        }

        private bool ParseStartTag()
        {
            InitToken(XmlNodeType.Element);
            ParseTag();
            return true;
        }

        private bool ParseEndTag()
        {
            InitToken(XmlNodeType.EndElement);
            ParseTag();
            // Paragraphs just before http://www.whatwg.org/specs/web-apps/current-work/#permitted:
            // When an end tag token is emitted, the content model flag must be switched to the PCDATA state.
            _contentModel = ContentModel.Pcdata;
            // When an end tag token is emitted with attributes, that is a parse error
            if (_attributes.Count > 0) {
                OnParseError(Resources.Html_ParseError_EndTagWithAttributes);
            }
            return true;
        }

        private void ParseTag()
        {
            // http://www.whatwg.org/specs/web-apps/current-work/#tag-name0
            _name = EatChars(delegate(char c) {
                return c != '>' && c != '<' && c != '/' && !Constants.IsSpaceCharacter(c);
            });
            _name = _name.ToLowerInvariant();

            bool seenSlash = false; // flag to notify "non permitted slashes" only once per start tag

            // the only "exit state" is ParseData
            while (_currentParsingFunction != ParseData && !EndOfStream) {
                // http://www.whatwg.org/specs/web-apps/current-work/#before
                SkipChars(Constants.IsSpaceCharacter);
                switch (NextInputChar) {
                case '/':
                    EatChars(1); // Eat SOLIDUS
                    // http://www.whatwg.org/specs/web-apps/current-work/#permitted
                    if (!seenSlash && (_tokenType != XmlNodeType.Element || NextInputChar != '>' || !Constants.IsVoidElement(_name))) {
                        OnParseError("Illformed start tag: contains /");
                        seenSlash = true;
                    }
                    break;
                case '<':
                    OnParseError("Illformed start tag: contains <");
                    _currentParsingFunction = ParseData;
                    break;
                case '>':
                    EatChars(1); // Eat GREATER-THAN SIGN
                    _currentParsingFunction = ParseData;
                    break;
                case '=':
                case '"':
                case '\'':
                case '&':
                    EatChars(1); // Eat EQUALS SIGN, QUOTATION MARK, APOSTROPHE or AMPERSAND
                    // NEW: standalone =, " or ' in start tag
                    if (!seenSlash) {
                        OnParseError("Illformed start tag: contains " + NextInputChar);
                        seenSlash = true;
                    }
                    break;
                default:
                    if (!EndOfStream) {
                        ParseAttribute();
                    }
                    break;
                }
            }
        }

        private void ParseAttribute()
        {
            Attribute attr = new Attribute();
            attr.quoteChar = ' ';

            attr.name = EatChars(delegate(char c)
            {
                return c != '=' && c != '>' && c != '<' && !Constants.IsSpaceCharacter(c)
                    // NEW: add ", & and ' to "end of attribute name" signal
                    /*&& c != '"' && c != '&' && c != '\''*/;
            });
            attr.name = attr.name.ToLowerInvariant();

            SkipChars(Constants.IsSpaceCharacter);
            if (NextInputChar == '=') {
                EatChars(1); // Eat EQUALS SIGN
                SkipChars(Constants.IsSpaceCharacter);
                if (!EndOfStream) {
                    char next = NextInputChar;
                    if (next != '<' && next != '>') {
                        Predicate<char> condition;
                        if (next == '"' || next == '\'') {
                            EatChars(1); // Eat QUOTATION MARK or APOSTROPHE
                            attr.quoteChar = next;
                            condition = delegate(char c) { return c != next && c != '&'; };
                        } else {
                            attr.quoteChar = ' ';
                            condition = delegate(char c)
                            {
                                return c != '&' && c != '<' && c != '>' && !Constants.IsSpaceCharacter(c);
                            };
                        }
                        StringBuilder sb = new StringBuilder();
                        do {
                            EatChars(sb, condition);
                            if (NextInputChar == '&') {
                                EatChars(1); // Eat AMPERSAND
                                string entityValue = EatEntity();
                                if (String.IsNullOrEmpty(entityValue)) {
                                    sb.Append('&');
                                } else {
                                    sb.Append(entityValue);
                                }
                            }
                        } while (!EndOfStream && (NextInputChar == '&' || condition(NextInputChar)));
                        attr.value = sb.ToString();
                        if (!EndOfStream && attr.quoteChar != ' ') {
                            EatChars(1); // Eat QUOTATION MARK or APOSTROPHE
                        }
                    }
                }
            }

            if (_attributes.Exists(delegate(Attribute a) { return attr.name == a.name; })) {
                OnParseError("Duplicate attribute: " + attr.name);
            } else {
                _attributes.Add(attr);
            }
        }

        // http://www.whatwg.org/specs/web-apps/current-work/#bogus
        private bool ParseBogusComment()
        {
            OnParseError("Bogus comment");

            StringBuilder sb = new StringBuilder();
            for (char c = EatNextInputChar(); c != '>' && c != EOF_CHAR; c = EatNextInputChar()) {
                sb.Append(c);
            }
            InitToken(XmlNodeType.Comment);
            _value = sb.ToString();
            _currentParsingFunction = ParseData;
            return true;
        }

        // http://www.whatwg.org/specs/web-apps/current-work/#markup
        private bool ParseMarkupDeclaration()
        {
            if (PeekChars(2) == "--") {
                EatChars(2);
                // http://www.whatwg.org/specs/web-apps/current-work/#comment
                InitToken(XmlNodeType.Comment);
                int dashes = 0;
                _value = PeekChars(delegate(char c)
                {
                    if (c == '>' && dashes >= 2) {
                        return false;
                    } else {
                        if (c == '-') {
                            dashes++;
                        } else {
                            dashes = 0;
                        }
                        return true;
                    }
                });
                int length = _value.Length;
                bool endOnEOF = (PeekChar(length) == EOF_CHAR);
                if (!endOnEOF) {
                    _value = _value.Substring(0, _value.Length - 2);
                    length++; // also eat the following '>'
                }
                EatChars(length);

                if (_value.Contains("--")) {
                    OnParseError("Comment containes double-dash");
                } else if (_value.EndsWith("-")) {
                    OnParseError("Comment ends on a dash: --->");
                }

                if (endOnEOF) {
                    OnParseError("Unexpected end of file in comment");
                }

                _currentParsingFunction = ParseData;
                return true;
            } else if (String.Equals(PeekChars(7), "DOCTYPE", StringComparison.OrdinalIgnoreCase)) {
                EatChars(7);
                InitToken(XmlNodeType.DocumentType);

                // http://www.whatwg.org/specs/web-apps/current-work/#doctype0
                if (!SkipChars(Constants.IsSpaceCharacter)) {
                    // http://www.whatwg.org/specs/web-apps/current-work/#before1
                    OnParseError("<!DOCTYPE should be followed by a space character, found " + NextInputChar);
                    _doctypeInError = true;
                }

                // http://www.whatwg.org/specs/web-apps/current-work/#before1
                if (NextInputChar == '>') {
                    EatChars(1);
                    OnParseError("DOCTYPE with no name");
                    _doctypeInError = true;
                    // FIXME: shouldn't we (and unit-tests) rather consider 'null' equivalent to String.Empty?
                    _name = String.Empty;
                } else {
                    // http://www.whatwg.org/specs/web-apps/current-work/#doctype1
                    _name = PeekChars(delegate(char c) {
                        return !Constants.IsSpaceCharacter(c) && c != '>';
                    });
                    EatChars(_name.Length);

                    _name = _name.ToUpperInvariant();
                    _doctypeInError = (_name != "HTML");

                    // http://www.whatwg.org/specs/web-apps/current-work/#after0
                    SkipChars(Constants.IsSpaceCharacter);

                    // http://www.whatwg.org/specs/web-apps/current-work/#bogus0
                    if (SkipChars(delegate(char c) { return c != '>'; })) {
                        OnParseError("Bogus DOCTYPE");
                        _doctypeInError = true;
                    }

                    if (NextInputChar != EOF_CHAR) {
                        Debug.Assert(NextInputChar == '>');
                        EatChars(1);
                    } else {
                        OnParseError("Unexpected end of file in DOCTYPE");
                        _doctypeInError = true;
                    }
                }
                _currentParsingFunction = ParseData;
                return true;
            } else {
                // "The next character that is consumed, if any, is the first character that will be in the comment."
                _currentParsingFunction = ParseBogusComment;
                return false;
            }
        }
    }
}