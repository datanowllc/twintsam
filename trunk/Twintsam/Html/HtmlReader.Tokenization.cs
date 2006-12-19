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
        /// A tokenizing method, corresponding to a state in HTML5 tokenization stage.
        /// </summary>
        /// <returns><see langword="true"/> if a token has been produced, <see langword="false"/> if no token is ready yet.</returns>
        private delegate bool TokenizationState();

        private TokenizationState _currentTokenizationState;

        private XmlNodeType _tokenType;
        private string _name;
        private string _value;
        private List<Attribute> _attributes = new List<Attribute>();

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

            this._tokenType = tokenType;
            _name = null;
            _value = null;
            _attributes.Clear();
        }

        /// <summary>
        /// Reads the next token from the stream.
        /// </summary>
        /// <returns><see langword="true"/> if the next node was read successfully; <see langword="false"/> if there are no more nodes to read.</returns>
        /// <remarks>
        /// Calls the <see cref="_currentTokenisationState"/> until it returns <see langword="true"/>.
        /// </remarks>
        private bool ParseToken()
        {
            if (_buffer.Length == 0 && _readerAtEof) {
                // buffer consumed and reader is at EOF => we reached EOF
                return false;
            }
            bool newToken = false;
            while (!newToken) {
                newToken = _currentTokenizationState();
            }
            // Paragraph just before http://www.whatwg.org/specs/web-apps/current-work/#permitted:
            // When an end tag token is emitted with attributes, that is a parse error
            if (_tokenType == XmlNodeType.EndElement
                && _attributes != null && _attributes.Count > 0) {
                OnParseError(Resources.Html_ParseError_EndTagWithAttributes);
            }
            return true;
        }

        // http://www.whatwg.org/specs/web-apps/current-work/#data-state
        private bool ParseData()
        {
            char c;
            StringBuilder sb = new StringBuilder();
            do {
                c = EatNextInputChar();
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
                    _currentTokenizationState = ParseStartTag;
                } else if (c == EOF_CHAR) {
                    break;
                } else {
                    sb.Append(c);
                }
            } while (_currentTokenizationState != ParseData);

            if (sb.Length > 0) {
                InitToken(XmlNodeType.Text);
                _value = sb.ToString();
                return true;
            } else {
                return c == EOF_CHAR;
            }
        }

        // http://www.whatwg.org/specs/web-apps/current-work/#tag-open
        private bool ParseStartTag()
        {
            throw new NotImplementedException();
        }
    }
}
