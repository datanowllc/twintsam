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

        private class Attribute
        {
            public string name;
            public string value;
            public char quoteChar;
        }

        /// <summary>
        /// Initialises a new token with the given type.
        /// </summary>
        /// <param name="tokenType">The type of the token, should be on of <see cref="XmlNodeType.DocumentType"/>, <see cref="XmlNodeType.Element"/>, <see cref="XmlNodeType.EndElement"/>, <see cref="XmlNodeType.Text"/> or <see cref="XmlNodeType.Comment"/></param>
        private void InitToken(XmlNodeType tokenType)
        {
            Debug.Assert(tokenType == XmlNodeType.DocumentType
                || tokenType == XmlNodeType.Element
                || tokenType == XmlNodeType.EndElement
                || tokenType == XmlNodeType.Text
                || tokenType == XmlNodeType.Comment);

            if (_tokenType == XmlNodeType.Element) {
                _lastEmittedStartTagName = _name;
            } else if (tokenType != XmlNodeType.Text) {
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
            // Paragraph just before http://www.whatwg.org/specs/web-apps/current-work/#permitted:
            // When an end tag token is emitted with attributes, that is a parse error
            if (newToken
                && _tokenType == XmlNodeType.EndElement
                && _attributes != null && _attributes.Count > 0) {
                OnParseError(Resources.Html_ParseError_EndTagWithAttributes);
            }
            return newToken;
        }

        // http://www.whatwg.org/specs/web-apps/current-work/#data-state
        private bool ParseData()
        {
            StringBuilder sb = new StringBuilder();
            do {
                char c = EatNextInputChar();
                if (c == '&' && (ContentModel == ContentModel.Pcdata || ContentModel == ContentModel.Rcdata)) {
                    // http://www.whatwg.org/specs/web-apps/current-work/#entity:
                    // Entity data state is only switched to from the data state,
                    // so integrate the algorithm here instead of within a new method
                    string entityValue = EatEntity();
                    if (String.IsNullOrEmpty(entityValue)) {
                        sb.Append(c);
                    } else {
                        sb.Append(entityValue);
                    }
                } else if (c == '<' && ContentModel != ContentModel.PlainText) {
                    // http://www.whatwg.org/specs/web-apps/current-work/#tag-open
                    // Tag open state is only switched to from the data state,
                    // so integrate the algorithm here instead of within a new method
                    switch (ContentModel) {
                    case ContentModel.Rcdata:
                    case ContentModel.Cdata:
                        if (NextInputChar == '/') {
                            c = EatNextInputChar();
                            // http://www.whatwg.org/specs/web-apps/current-work/#close1
                            if (('A' <= NextInputChar && NextInputChar <= 'Z') || ('a' <= NextInputChar && NextInputChar <= 'z')) {
                                string tagName = PeekChars(_lastEmittedStartTagName.Length);
                                char next = PeekChar(tagName.Length);
                                if (!String.Equals(_lastEmittedStartTagName, tagName, StringComparison.OrdinalIgnoreCase)
                                    || (!Constants.IsSpaceCharacter(next) && next != '>' && next != '/' && next != '<' && next != EOF_CHAR)) {
                                    OnParseError("Found end tag with different name than the previous start tag");
                                    sb.Append("</");
                                } else {
                                    _currentParsingFunction = ParseEndTag;
                                }
                            } else if (NextInputChar == '>') {
                                OnParseError("Empty end tag or unescaped </>");
                            } else if (NextInputChar == EOF_CHAR) {
                                OnParseError("Unexpected end of file");
                                sb.Append("</");
                                // let the next loop iteration handle EOF
                            } else {
                                OnParseError("Bogus comment");
                                _currentParsingFunction = ParseBogusComment;
                            }
                        } else {
                            sb.Append(c);
                        }
                        break;
                    case ContentModel.Pcdata:
                        if (NextInputChar == '!') {
                            c = EatNextInputChar();
                            _currentParsingFunction = ParseMarkupDeclaration;
                        } else if (NextInputChar == '/') {
                            c = EatNextInputChar();
                            // http://www.whatwg.org/specs/web-apps/current-work/#close1
                            if (('A' <= NextInputChar && NextInputChar <= 'Z') || ('a' <= NextInputChar && NextInputChar <= 'z')) {
                                _currentParsingFunction = ParseEndTag;
                            } else if (NextInputChar == '>') {
                                OnParseError("Empty end tag or unescaped </>");
                            } else if (NextInputChar == EOF_CHAR) {
                                OnParseError("Unexpected end of file");
                                sb.Append("</");
                                // let the next loop iteration handle EOF
                            } else {
                                OnParseError("Bogus comment");
                                _currentParsingFunction = ParseBogusComment;
                            }
                        } else if (('A' <= NextInputChar && NextInputChar <= 'Z')
                            || ('a' <= NextInputChar && NextInputChar <= 'z')) {
                            InitToken(XmlNodeType.Element);
                            _currentParsingFunction = ParseStartTag;
                        } else if (NextInputChar == '>') {
                            OnParseError("Empty tag or unescaped <>");
                            c = EatNextInputChar();
                            sb.Append("<>");
                            _currentParsingFunction = ParseData;
                        } else if (NextInputChar == '?') {
                            OnParseError("Bogus comment");
                            _currentParsingFunction = ParseBogusComment;
                        } else {
                            OnParseError("Unescaped <");
                            sb.Append(c);
                        }
                        break;
                    }
                } else if (c == EOF_CHAR) {
                    break;
                } else {
                    sb.Append(c);
                }
            } while (_currentParsingFunction == ParseData);

            if (sb.Length > 0) {
                InitToken(XmlNodeType.Text);
                _value = sb.ToString();
                return true;
            } else {
                return false;
            }
        }

        private bool ParseStartTag()
        {
            throw new NotImplementedException();
        }

        private bool ParseEndTag()
        {
            throw new NotImplementedException();
        }

        // http://www.whatwg.org/specs/web-apps/current-work/#bogus
        private bool ParseBogusComment()
        {
            StringBuilder sb = new StringBuilder();
            for (char c = EatNextInputChar(); c != '>' && c != EOF_CHAR; c = EatNextInputChar()) {
                sb.Append(c);
            }
            InitToken(XmlNodeType.Comment);
            _value = sb.ToString();
            return true;
        }

        // http://www.whatwg.org/specs/web-apps/current-work/#markup
        private bool ParseMarkupDeclaration()
        {
            if (PeekChars(2) == "--") {
                EatChars(2);
                // http://www.whatwg.org/specs/web-apps/current-work/#comment
                StringBuilder sb = new StringBuilder();
                int dashes = 0;
                char c;
                for (c = EatNextInputChar(); c != EOF_CHAR && (dashes < 2 || c != '>'); c = EatNextInputChar()) {
                    if (c == '-') {
                        dashes++;
                    } else if (dashes >= 2 && c != '>') {
                        // at least 2 dashes not followed by '>' => reinit dashes counter
                        dashes = 0;
                    }
                    sb.Append(c);
                }
                InitToken(XmlNodeType.Comment);
                _value = sb.ToString(0, sb.Length - 2);
                if (_value.Contains("--")) {
                    OnParseError("Comment containes double-dash");
                } else if (_value.EndsWith("-")) {
                    OnParseError("Comment ends on a dash: --->");
                }
                if (c == EOF_CHAR) {
                    OnParseError("Unexpected end of file");
                }
                return true;
            } else if (String.Equals(PeekChars(7), "DOCTYPE", StringComparison.OrdinalIgnoreCase)) {
                EatChars(7);
                // http://www.whatwg.org/specs/web-apps/current-work/#doctype0
                InitToken(XmlNodeType.DocumentType);

                if (!Constants.IsSpaceCharacter(NextInputChar)) {
                    OnParseError("<!DOCTYPE should be followed by a space character, found " + NextInputChar);
                    _doctypeInError = true;
                    if (NextInputChar == '>' || NextInputChar == EOF_CHAR) {
                        return true;
                    }
                }
                StringBuilder sb = new StringBuilder();
                char c = EatNextInputChar(); ;
                while (c != EOF_CHAR && !Constants.IsSpaceCharacter(c) && c != '>') {
                    if ('a' <= c && c <= 'z') {
                        c = c + 0x0020;
                    }
                    if (c < 'A' || 'Z' < c) {
                        _doctypeInError = true;
                    }
                    sb.Append(c);

                    c = EatNextInputChar();
                }
                // TODO: after doctype name
            } else {
                _currentParsingFunction = ParseBogusComment;
                return false;
            }
        }
    }
}
