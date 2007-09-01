using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Xml;

namespace Twintsam.Html
{
    public partial class HtmlReader
    {
        private class Token : ICloneable
        {
            public XmlNodeType tokenType;
            public string name;
            public bool hasTrailingSolidus;
            public bool isIncorrectDoctype;
            public string value;
            public List<Attribute> attributes = new List<Attribute>();

            public static Token CreateStartTag(string name)
            {
                return CreateStartTag(name, null);
            }
            public static Token CreateStartTag(string name, IEnumerable<Attribute> attributes)
            {
                Debug.Assert(!String.IsNullOrEmpty(name));
                Token token = new Token();
                token.tokenType = XmlNodeType.Element;
                token.name = name;
                if (attributes != null) {
                    token.attributes.AddRange(attributes);
                }
                token.value = String.Empty;
                return token;
            }
            public static Token CreateEndTag(string name)
            {
                Debug.Assert(!String.IsNullOrEmpty(name));
                Debug.Assert(String.Equals(name, name.ToLowerInvariant(), StringComparison.Ordinal));

                Token token = new Token();
                token.tokenType = XmlNodeType.EndElement;
                token.name = name;
                token.value = String.Empty;
                return token;
            }
            public static Token CreateText(string text)
            {
                Debug.Assert(!Constants.IsSpaceCharacter(text[0]));
                Token token = new Token();
                token.tokenType = XmlNodeType.Text;
                token.name = String.Empty;
                token.value = text;
                return token;
            }
            public static Token CreateWhitespace(string whitespace)
            {
                Debug.Assert(Constants.IsSpace(whitespace));
                Token token = new Token();
                token.tokenType = XmlNodeType.Whitespace;
                token.name = String.Empty;
                token.value = whitespace;
                return token;
            }

            #region ICloneable Membres

            public object Clone()
            {
                Token newToken = (Token)this.MemberwiseClone();
                newToken.attributes = new List<Attribute>(newToken.attributes);
                return newToken;
            }

            #endregion
        }

        private enum TreeConstructionPhase
        {
            Initial,
            Root,
            CdataOrRcdata, // XXX: special phase for the "generic (R)CDATA parsing algorithm
            Main,
            TrailingEnd,
        }

        private enum InsertionMode
        {
            BeforeHead,
            InHead,
            InHeadNoscript,
            AfterHead,
            InBody,
            InTable,
            InCaption,
            InColumnGroup,
            InTableBody,
            InRow,
            InCell,
            InSelect,
            AfterBody,
            InFrameset,
            AfterFrameset,
        }

        private enum CurrentTokenizerTokenState
        {
            Unprocessed,
            Ignored,
            Emitted,
        }

        private class Tokenizer : HtmlWrappingTokenizer
        {
            private Stack<Token> _pendingTokens = new Stack<Token>();
            private Token _pendingToken;

            public Tokenizer(HtmlTokenizer tokenizer) : base(tokenizer) { }

            public override int AttributeCount
            {
                get
                {
                    if (_pendingToken != null) {
                        return _pendingToken.attributes.Count;
                    }
                    return base.AttributeCount;
                }
            }
            protected override void Dispose(bool disposing)
            {
                if (disposing) {
                    _pendingToken = null;
                }
                base.Dispose(disposing);
            }
            public override bool EOF
            {
                get
                {
                    if (_pendingToken != null) {
                        return false;
                    }
                    return base.EOF;
                }
            }
            public override string GetAttribute(int index)
            {
                if (_pendingToken != null) {
                    if (index < 0 || index >= _pendingToken.attributes.Count) {
                        throw new ArgumentOutOfRangeException("index");
                    }
                    return _pendingToken.attributes[index].value;
                }
                return base.GetAttribute(index);
            }
            public override string GetAttribute(string name)
            {
                if (_pendingToken != null) {
                    name = name.ToLowerInvariant();
                    for (int i = 0; i < _pendingToken.attributes.Count; i++) {
                        Attribute attribute = _pendingToken.attributes[i];
                        if (String.Equals(attribute.name, name, StringComparison.Ordinal)) {
                            return attribute.value;
                        }
                    }
                    return null;
                }
                return base.GetAttribute(name);
            }
            public override int GetAttributeIndex(string name)
            {
                if (_pendingToken != null) {
                    name = name.ToLowerInvariant();
                    for (int i = 0; i < _pendingToken.attributes.Count; i++) {
                        if (String.Equals(_pendingToken.attributes[i].name, name, StringComparison.Ordinal)) {
                            return i;
                        }
                    }
                    return -1;
                }
                return base.GetAttributeIndex(name);
            }
            public override string GetAttributeName(int index)
            {
                if (_pendingToken != null) {
                    if (index < 0 || index >= _pendingToken.attributes.Count) {
                        throw new ArgumentOutOfRangeException("index");
                    }
                    return _pendingToken.attributes[index].name;
                }
                return base.GetAttributeName(index);
            }
            public override char GetAttributeQuoteChar(int index)
            {
                if (_pendingToken != null) {
                    if (index < 0 || index >= _pendingToken.attributes.Count) {
                        throw new ArgumentOutOfRangeException("index");
                    }
                    return _pendingToken.attributes[index].quoteChar;
                }
                return base.GetAttributeQuoteChar(index);
            }
            public override bool HasAttributes
            {
                get
                {
                    if (_pendingToken != null) {
                        return _pendingToken.attributes.Count > 0;
                    }
                    return base.HasAttributes;
                }
            }
            public override bool HasTrailingSolidus
            {
                get
                {
                    if (_pendingToken != null) {
                        return _pendingToken.hasTrailingSolidus;
                    }
                    return base.HasTrailingSolidus;
                }
            }
            public override bool IsIncorrectDoctype
            {
                get
                {
                    if (_pendingToken != null) {
                        return _pendingToken.isIncorrectDoctype;
                    }
                    return base.IsIncorrectDoctype;
                }
            }
            public override string Name
            {
                get
                {
                    if (_pendingToken != null) {
                        return _pendingToken.name;
                    }
                    return base.Name;
                }
            }
            public override bool Read()
            {
                if (_pendingTokens.Count > 0) {
                    _pendingToken = _pendingTokens.Pop();
                    return true;
                }
                if (_pendingToken != null) {
                    _pendingToken = null;
                    return !base.EOF;
                }
                return base.Read();
            }
            public override ReadState ReadState
            {
                get
                {
                    if (_pendingToken != null) {
                        return ReadState.Interactive;
                    }
                    return base.ReadState;
                }
            }
            public override string this[int index]
            {
                get
                {
                    return GetAttribute(index);
                }
            }
            public override string this[string name]
            {
                get
                {
                    return GetAttribute(name);
                }
            }
            public override XmlNodeType TokenType
            {
                get
                {
                    if (_pendingToken != null) {
                        return _pendingToken.tokenType;
                    }
                    return base.TokenType;
                }
            }
            public new string Value
            {
                get
                {
                    if (_pendingToken != null) {
                        return _pendingToken.value;
                    }
                    return base.Value;
                }
            }

            internal Token Token
            {
                get
                {
                    if (_pendingToken != null) {
                        return _pendingToken;
                    }
                    Token token = new Token();
                    token.tokenType = Tokenizer.TokenType;
                    token.name = Tokenizer.Name;
                    token.value = Tokenizer.Value;
                    token.isIncorrectDoctype = Tokenizer.IsIncorrectDoctype;
                    token.hasTrailingSolidus = Tokenizer.HasTrailingSolidus;
                    HtmlTextTokenizer textTokenizer = Tokenizer as HtmlTextTokenizer;
                    if (textTokenizer != null) {
                        token.attributes.AddRange(textTokenizer._attributes.ConvertAll<Attribute>(
                            delegate(HtmlTextTokenizer.Attribute attribute)
                            {
                                return attribute;
                            }));
                    } else {
                        for (int i = 0; i < Tokenizer.AttributeCount; i++) {
                            Attribute attribute = new Attribute(Tokenizer.GetAttributeName(i), Tokenizer as IXmlLineInfo);
                            attribute.value = Tokenizer.GetAttribute(i);
                            attribute.quoteChar = Tokenizer.GetAttributeQuoteChar(i);
                            token.attributes.Add(attribute);
                        }
                    }
                    ReplaceToken(token);
                    return token;
                }
            }
            internal void PushToken(Token token)
            {
                Debug.Assert(token != null);
                if (_pendingToken != null) {
                    _pendingTokens.Push(_pendingToken);
                }
                _pendingToken = token;
            }
            internal void ReplaceToken(Token token)
            {
                Debug.Assert(token != null);
                if (_pendingToken == null && _pendingTokens.Count == 0) {
                    base.Read();
                }
                _pendingToken = token;
            }
        }
    }
}
