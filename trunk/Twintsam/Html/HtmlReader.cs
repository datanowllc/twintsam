using System;
using System.Collections.Generic;
using System.IO;
using System.Xml;
using System.Diagnostics;
using System.Text;
using System.Collections.Specialized;

namespace Twintsam.Html
{
    public partial class HtmlReader : XmlReader, IXmlLineInfo
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

        private enum ParsingState
        {
            ProcessNextToken,
            ReprocessCurrentToken,
            Pause,
        }

        public static readonly string[] QuirksModeDoctypePublicIds = {
            "+//silmaril//dtd html pro v0r11 19970101//en",
            "-//advasoft ltd//dtd html 3.0 aswedit + extensions//en",
            "-//as//dtd html 3.0 aswedit + extensions//en",
            "-//ietf//dtd html 2.0 level 1//en",
            "-//ietf//dtd html 2.0 level 2//en",
            "-//ietf//dtd html 2.0 strict level 1//en",
            "-//ietf//dtd html 2.0 strict level 2//en",
            "-//ietf//dtd html 2.0 strict//en",
            "-//ietf//dtd html 2.0//en",
            "-//ietf//dtd html 2.1e//en",
            "-//ietf//dtd html 3.0//en",
            "-//ietf//dtd html 3.0//en//",
            "-//ietf//dtd html 3.2 final//en",
            "-//ietf//dtd html 3.2//en",
            "-//ietf//dtd html 3//en",
            "-//ietf//dtd html level 0//en",
            "-//ietf//dtd html level 0//en//2.0",
            "-//ietf//dtd html level 1//en",
            "-//ietf//dtd html level 1//en//2.0",
            "-//ietf//dtd html level 2//en",
            "-//ietf//dtd html level 2//en//2.0",
            "-//ietf//dtd html level 3//en",
            "-//ietf//dtd html level 3//en//3.0",
            "-//ietf//dtd html strict level 0//en",
            "-//ietf//dtd html strict level 0//en//2.0",
            "-//ietf//dtd html strict level 1//en",
            "-//ietf//dtd html strict level 1//en//2.0",
            "-//ietf//dtd html strict level 2//en",
            "-//ietf//dtd html strict level 2//en//2.0",
            "-//ietf//dtd html strict level 3//en",
            "-//ietf//dtd html strict level 3//en//3.0",
            "-//ietf//dtd html strict//en",
            "-//ietf//dtd html strict//en//2.0",
            "-//ietf//dtd html strict//en//3.0",
            "-//ietf//dtd html//en",
            "-//ietf//dtd html//en//2.0",
            "-//ietf//dtd html//en//3.0",
            "-//metrius//dtd metrius presentational//en",
            "-//microsoft//dtd internet explorer 2.0 html strict//en",
            "-//microsoft//dtd internet explorer 2.0 html//en",
            "-//microsoft//dtd internet explorer 2.0 tables//en",
            "-//microsoft//dtd internet explorer 3.0 html strict//en",
            "-//microsoft//dtd internet explorer 3.0 html//en",
            "-//microsoft//dtd internet explorer 3.0 tables//en",
            "-//netscape comm. corp.//dtd html//en",
            "-//netscape comm. corp.//dtd strict html//en",
            "-//o'reilly and associates//dtd html 2.0//en",
            "-//o'reilly and associates//dtd html extended 1.0//en",
            "-//spyglass//dtd html 2.0 extended//en",
            "-//sq//dtd html 2.0 hotmetal + extensions//en",
            "-//sun microsystems corp.//dtd hotjava html//en",
            "-//sun microsystems corp.//dtd hotjava strict html//en",
            "-//w3c//dtd html 3 1995-03-24//en",
            "-//w3c//dtd html 3.2 draft//en",
            "-//w3c//dtd html 3.2 final//en",
            "-//w3c//dtd html 3.2//en",
            "-//w3c//dtd html 3.2s draft//en",
            "-//w3c//dtd html 4.0 frameset//en",
            "-//w3c//dtd html 4.0 transitional//en",
            "-//w3c//dtd html experimental 19960712//en",
            "-//w3c//dtd html experimental 970421//en",
            "-//w3c//dtd w3 html//en",
            "-//w3o//dtd w3 html 3.0//en",
            "-//w3o//dtd w3 html 3.0//en//",
            "-//w3o//dtd w3 html strict 3.0//en//",
            "-//webtechs//dtd mozilla html 2.0//en",
            "-//webtechs//dtd mozilla html//en",
            "-/w3c/dtd html 4.0 transitional/en",
            "html",
        };

        public static readonly string[] QuirksModeDoctypePublicIdsWhenSystemIdIsMissing = {
            "-//w3c//dtd html 4.01 frameset//EN", "-//w3c//dtd html 4.01 transitional//EN"
        };

        public static readonly string QuirksModeDoctypeSystemId = "http://www.ibm.com/data/dtd/v11/ibmxhtml1-transitional.dtd";

        public static readonly string[] AlmostStandardsModeDoctypePublicIds = {
            "-//W3C//DTD XHTML 1.0 Frameset//EN", "-//W3C//DTD XHTML 1.0 Transitional//EN",
        };

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
                    for (int i = 0; i < _pendingToken.attributes.Count; i++){
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
                    return base.EOF;
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

        private Tokenizer _tokenizer;
        private IXmlLineInfo _lineInfo;
        private TreeConstructionPhase _phase;
        private InsertionMode _insertionMode;

        private Queue<Token> _pendingOutputTokens = new Queue<Token>();
        private int _attributeIndex = -1;

        private CompatibilityMode _compatMode = CompatibilityMode.Standards;

        #region Constructors
        // TODO: public constructors

        public HtmlReader(TextReader reader)
        {
            if (reader == null) {
                throw new ArgumentNullException("reader");
            }
            Init(HtmlTokenizer.Create(reader));
        }
        #endregion

        private void Init(HtmlTokenizer tokenizer)
        {
            _lineInfo = tokenizer as IXmlLineInfo;
            tokenizer.ParseError += new EventHandler<ParseErrorEventArgs>(tokenizer_ParseError);
            _tokenizer = new Tokenizer(tokenizer);
        }
        private void tokenizer_ParseError(object sender, ParseErrorEventArgs e)
        {
            OnParseError(e);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing) {
                // TODO: add flag to eventually not close the tokenizer
                // (particularly when it was passed to the constructor)
                _tokenizer.Close();
            }
            base.Dispose(disposing);
        }

        #region 8.2.4.3.1 The stack of open elements
        // http://www.whatwg.org/specs/web-apps/current-work/multipage/section-tree-construction.html#stack
        private Stack<Token> _openElements = new Stack<Token>();

        private bool IsInScope(string name, bool inTableScope)
        {
            Debug.Assert(String.Equals(name, name.ToLowerInvariant(), StringComparison.Ordinal));

            foreach (Token openElement in _openElements) {
                if (String.Equals(name, openElement.name, StringComparison.Ordinal)) {
                    return true;
                }
            }
            return false;
        }
        #endregion

        #region 8.2.4.3.2. The list of active formatting elements
        // http://www.whatwg.org/specs/web-apps/current-work/multipage/section-tree-construction.html#list-of4
        private Stack<LinkedList<Token>> _activeFormattingElements = new Stack<LinkedList<Token>>();

        private Token ElementInActiveFormattingElements(string name)
        {
            for (LinkedListNode<Token> element = _activeFormattingElements.Peek().Last;
                element != null; element = element.Previous) {
                Debug.Assert(element.Value != null);
                if (element.Value.name == name) {
                    return element.Value;
                }
            }
            return null;
        }

        private void ReconstructActiveFormattingElements()
        {
            if (_activeFormattingElements.Count > 0) {
                LinkedListNode<Token> element = _activeFormattingElements.Peek().Last;
                if (element != null) {
                    Debug.Assert(element.Value != null);
                    if (_openElements.Contains(element.Value)) {
                        while (element.Previous != null) {
                            element = element.Previous;
                            if (_openElements.Contains(element.Value)) {
                                element = element.Next;
                                break;
                            }
                        }
                        do {
                            _pendingOutputTokens.Enqueue(element.Value);
                            element = element.Next;
                        } while (element != null);
                    }
                }
            }
        }

        private void ClearActiveFormattingElements()
        {
            _activeFormattingElements.Pop();
        }
        #endregion

        #region 8.2.4.3.3 Creating and inserting HTML elements
        private ParsingState InsertHtmlElement()
        {
            InsertHtmlElement(_tokenizer.Token);
            return ParsingState.Pause;
        }
        private void InsertHtmlElement(Token token)
        {
            Debug.Assert(token != null && token.tokenType == XmlNodeType.Element);
            // http://www.whatwg.org/specs/web-apps/current-work/multipage/section-tree-construction.html#insert
            _pendingOutputTokens.Enqueue(token);
            _openElements.Push(token);
        }
        #endregion

        #region 8.2.4.3.4. Closing elements that have implied end tags
        // http://www.whatwg.org/specs/web-apps/current-work/multipage/section-tree-construction.html#generate
        private bool GenerateImpliedEndTags(string omitted)
        {
            Debug.Assert(omitted == null || String.Equals(omitted, omitted.ToLowerInvariant(), StringComparison.Ordinal));

            if (_openElements.Count > 0) {
                string element = _openElements.Peek().name;
                if ((omitted == null || String.Equals(element, omitted, StringComparison.Ordinal))
                    && Constants.HasOptionalEndTag(element)) {
                    _tokenizer.PushToken(Token.CreateEndTag(element));
                    return true;
                }
            }
            return false;
        }
        #endregion

        #region 8.2.4.3.6. The insertion mode
        // http://www.whatwg.org/specs/web-apps/current-work/multipage/section-tree-construction.html#reset
        private void ResetInsertionMode()
        {
            _insertionMode = InsertionMode.InBody;
            // TODO: step 3 (fragment case)
            foreach (Token element in _openElements) {
                switch (element.name) {
                case "select":
                    _insertionMode = InsertionMode.InSelect;
                    return;
                case "td":
                case "th":
                    _insertionMode = InsertionMode.InCell;
                    return;
                case "tr":
                    _insertionMode = InsertionMode.InRow;
                    return;
                case "tbody":
                case "thead":
                case "tfoot":
                    _insertionMode = InsertionMode.InTableBody;
                    return;
                case "caption":
                    _insertionMode = InsertionMode.InCaption;
                    return;
                case "colgroup":
                    _insertionMode = InsertionMode.InColumnGroup;
                    return;
                case "table":
                    _insertionMode = InsertionMode.InTable;
                    return;
                case "head":
                case "body":
                    Debug.Assert(_insertionMode == InsertionMode.InBody);
                    return;
                case "frameset":
                    _insertionMode = InsertionMode.InFrameset;
                    return;
                case "html":
                    throw new NotImplementedException();
                }
            }
        }
        #endregion

        public override void Close()
        {
            Dispose(true);
        }

        public override bool EOF
        {
            get
            {
                if (_pendingOutputTokens.Count > 0 && _attributeIndex >= 0) {
                    return false;
                }
                return _tokenizer.EOF;
            }
        }

        public override XmlNodeType NodeType
        {
            get
            {
                if (_attributeIndex >= 0) {
                    return XmlNodeType.Attribute;
                } else if (_pendingOutputTokens.Count > 0) {
                    return _pendingOutputTokens.Peek().tokenType;
                }
                return _tokenizer.TokenType;
            }
        }

        public override string LocalName
        {
            get
            {
                if (_pendingOutputTokens.Count > 0) {
                    if (_attributeIndex >= 0) {
                        return _pendingOutputTokens.Peek().attributes[_attributeIndex].name;
                    }
                    return _pendingOutputTokens.Peek().name;
                }
                if (_attributeIndex >= 0) {
                    return _tokenizer.GetAttributeName(_attributeIndex);
                }
                return _tokenizer.Name;
            }
        }

        public override bool IsEmptyElement
        {
            get
            {
                if (NodeType == XmlNodeType.Element) {
                    return false;
                }
                if (_pendingOutputTokens.Count > 0) {
                    return _pendingOutputTokens.Peek().hasTrailingSolidus;
                }
                return _tokenizer.HasTrailingSolidus;
            }
        }

        public override string Value
        {
            get
            {
                if (_pendingOutputTokens.Count > 0) {
                    if (_attributeIndex >= 0) {
                        return _pendingOutputTokens.Peek().attributes[_attributeIndex].value;
                    }
                    return _pendingOutputTokens.Peek().value;
                }
                if (_attributeIndex >= 0) {
                    return _tokenizer.GetAttribute(_attributeIndex);
                }
                return _tokenizer.Value;
            }
        }

        public override ReadState ReadState
        {
            get
            {
                if (_pendingOutputTokens.Count > 0 && _attributeIndex >= 0) {
                    return ReadState.Interactive;
                }
                return _tokenizer.ReadState;
            }
        }

        public override int AttributeCount
        {
            get {
                if (_attributeIndex >= 0) {
                    return 0;
                }
                if (_pendingOutputTokens.Count > 0) {
                    return _pendingOutputTokens.Peek().attributes.Count;
                }
                return _tokenizer.AttributeCount;
            }
        }

        public override string BaseURI
        {
            get { throw new NotImplementedException(); }
        }

        public override int Depth
        {
            get
            {
                // TODO: Depth
                return 0;
            }
        }

        public override string GetAttribute(int i)
        {
            if (i < 0 || i >= AttributeCount) {
                throw new ArgumentOutOfRangeException("i");
            }
            if (_pendingOutputTokens.Count > 0) {
                return _pendingOutputTokens.Peek().attributes[i].value;
            }
            return _tokenizer.GetAttribute(i);
        }

        protected int GetAttributeIndex(string name)
        {
            if (_pendingOutputTokens.Count > 0) {
                int index = 0;
                foreach (Attribute attribute in _pendingOutputTokens.Peek().attributes) {
                    if (String.Equals(attribute.name, name, StringComparison.Ordinal)) {
                        return index;
                    }
                    index++;
                }
                return -1;
            }
            return _tokenizer.GetAttributeIndex(name);
        }

        public override string GetAttribute(string name, string namespaceURI)
        {
            if (_attributeIndex >= 0) {
                return null;
            }
            if (String.IsNullOrEmpty(namespaceURI)
                || String.Equals(namespaceURI, Constants.XhtmlNamespaceUri, StringComparison.Ordinal)) {
                if (_pendingOutputTokens.Count > 0) {
                    foreach (Attribute attribute in _pendingOutputTokens.Peek().attributes) {
                        if (String.Equals(attribute.name, name, StringComparison.Ordinal)) {
                            return attribute.value;
                        }
                    }
                    return null;
                }
                return _tokenizer.GetAttribute(name);
            }
            return null;
        }

        public override string GetAttribute(string name)
        {
            return GetAttribute(name, null);
        }

        public override bool HasValue
        {
            get { throw new NotImplementedException(); }
        }

        public override string LookupNamespace(string prefix)
        {
            if (String.IsNullOrEmpty(prefix)) {
                return Constants.XhtmlNamespaceUri;
            }
            return null;
        }

        public override void MoveToAttribute(int i)
        {
            int attributeIndex = _attributeIndex;
            _attributeIndex = -1;
            if (i < 0 || i >= AttributeCount) {
                _attributeIndex = attributeIndex;
                throw new ArgumentOutOfRangeException("i");
            }
            _attributeIndex = i;
        }

        public override bool MoveToAttribute(string name, string ns)
        {
            int attributeIndex = _attributeIndex;
            _attributeIndex = -1;
            if (NodeType == XmlNodeType.Element || NodeType == XmlNodeType.DocumentType) {
                if (String.IsNullOrEmpty(ns)
                    || String.Equals(ns, Constants.XhtmlNamespaceUri, StringComparison.Ordinal)) {
                    _attributeIndex = GetAttributeIndex(name);
                    return true;
                }
            }
            _attributeIndex = attributeIndex;
            return false;
        }

        public override bool MoveToAttribute(string name)
        {
            return MoveToAttribute(name, null);
        }

        public override bool MoveToElement()
        {
            if (_attributeIndex >= 0) {
                _attributeIndex = -1;
                return true;
            }
            return false;
        }

        public override bool MoveToFirstAttribute()
        {
            int attributeIndex = _attributeIndex;
            _attributeIndex = -1;
            if (AttributeCount <= 0) {
                _attributeIndex = attributeIndex;
                return false;
            }
            _attributeIndex = 0;
            return true;
        }

        public override bool MoveToNextAttribute()
        {
            if (_attributeIndex < 0) {
                return MoveToFirstAttribute();
            }
            _attributeIndex++;
            if (_attributeIndex < 0 || _attributeIndex >= AttributeCount) {
                _attributeIndex--;
                return false;
            }
            return true;
        }

        public override XmlNameTable NameTable
        {
            get { throw new NotImplementedException(); }
        }

        public override string NamespaceURI
        {
            get
            {
                if (NodeType == XmlNodeType.Element || NodeType == XmlNodeType.EndElement) {
                    return Constants.XhtmlNamespaceUri;
                }
                return String.Empty;
            }
        }

        public override string Prefix
        {
            get { return String.Empty; }
        }

        public override bool Read()
        {
            if (_pendingOutputTokens.Count > 0) {
                _pendingOutputTokens.Dequeue();
                if (_pendingOutputTokens.Count > 0) {
                    return true;
                }
            }
            if (_tokenizer.EOF) {
                return false;
            }
            if (!_tokenizer.Read()) {
                return ProcessEndOfFile();
            }
            ParsingState parsingState;
            do {
                switch (_phase) {
                case TreeConstructionPhase.Initial:
                    parsingState = ParseInitial();
                    break;
                case TreeConstructionPhase.Root:
                    parsingState = ParseRoot();
                    break;
                case TreeConstructionPhase.Main:
                    parsingState = ParseMain();
                    break;
                case TreeConstructionPhase.TrailingEnd:
                    throw new NotImplementedException();
                default:
                    throw new InvalidOperationException();
                }
                if (parsingState == ParsingState.ProcessNextToken) {
                    if (!_tokenizer.Read()) {
                        return ProcessEndOfFile();
                    }
                }
            } while (parsingState != ParsingState.Pause);

            return true;
        }

        private string ExtractLeadingWhitespace()
        {
            Debug.Assert(_tokenizer.TokenType == XmlNodeType.Text);
            string value = _tokenizer.Value;
            if (Constants.IsSpaceCharacter(value[0])) {
                string nonWhitespace = _tokenizer.Value.TrimStart(Constants.SpaceCharacters.ToCharArray());
                string whitespace = _tokenizer.Value.Substring(0, _tokenizer.Value.Length - nonWhitespace.Length);
                _tokenizer.ReplaceToken(Token.CreateText(nonWhitespace));
                return whitespace;
            } else {
                return String.Empty;
            }
        }
        private ParsingState ActAsIfTokenHadBeenSeenThenReprocessCurrentToken(Token token)
        {
            _tokenizer.PushToken(token);
            return ParsingState.ReprocessCurrentToken;
        }

        private bool ProcessEndOfFile()
        {
            switch (_phase) {
            case TreeConstructionPhase.Initial:
                // http://www.whatwg.org/specs/web-apps/current-work/multipage/section-tree-construction.html#the-initial0
                OnParseError("Unexpected end of stream. Expected DOCTYPE.");
                _compatMode = CompatibilityMode.QuirksMode;
                goto case TreeConstructionPhase.Root;
            case TreeConstructionPhase.Root:
                // http://www.whatwg.org/specs/web-apps/current-work/multipage/section-tree-construction.html#the-root1
                // XXX: that's not exactly what the spec says, but it has an equivalent result.
                InsertHtmlElement(Token.CreateStartTag("html"));
                _phase = TreeConstructionPhase.Main;
                goto case TreeConstructionPhase.Main;
            case TreeConstructionPhase.Main:
                // http://www.whatwg.org/specs/web-apps/current-work/multipage/section-tree-construction.html#the-main0
                bool generated = GenerateImpliedEndTags(null);
                if (_openElements.Count > 2){
                    OnParseError("Unexpected end of stream. Missing closing tags.");
                } else if (_openElements.Count == 2
                    && String.Equals(_openElements.Peek().name, "body", StringComparison.Ordinal)) {
                    OnParseError(
                        String.Concat("Unexpected end of stream. Expected end tag (",
                            _openElements.Peek().name, ") first."));
                }
                // TODO: fragment case
                return generated;
            case TreeConstructionPhase.TrailingEnd:
                return false; // Nothing to do
            default:
                throw new InvalidOperationException();
            }
        }

        private ParsingState ParseInitial()
        {
            // http://www.whatwg.org/specs/web-apps/current-work/multipage/section-tree-construction.html#the-initial0
            switch (_tokenizer.TokenType) {
            case XmlNodeType.Whitespace:
                return ParsingState.ProcessNextToken;
            case XmlNodeType.Comment:
                _pendingOutputTokens.Enqueue(_tokenizer.Token);
                return ParsingState.Pause;
            case XmlNodeType.DocumentType:
                if (!String.Equals(_tokenizer.Name, "HTML", StringComparison.OrdinalIgnoreCase)) {
                    OnParseError("DOCTYPE name is not HTML (case-insitive)");
                    _compatMode = CompatibilityMode.QuirksMode;
                } else if (_tokenizer.AttributeCount != 0) {
                    OnParseError("DOCTYPE has public and/or system identifier");
                    if (_tokenizer.IsIncorrectDoctype) {
                        _compatMode = CompatibilityMode.QuirksMode;
                    }
                    string systemId = _tokenizer.GetAttribute("SYSTEM");
                    if (systemId != null && String.Equals(systemId, QuirksModeDoctypeSystemId, StringComparison.OrdinalIgnoreCase)) {
                        _compatMode = CompatibilityMode.QuirksMode;
                    } else {
                        string publicId = _tokenizer.GetAttribute("PUBLIC");
                        if (publicId != null) {
                            publicId = publicId.ToLowerInvariant();
                            if (Constants.Is(QuirksModeDoctypePublicIds, publicId)) {
                                _compatMode = CompatibilityMode.QuirksMode;
                            } else if (Constants.Is(QuirksModeDoctypePublicIdsWhenSystemIdIsMissing, publicId)) {
                                _compatMode = (systemId == null) ? CompatibilityMode.QuirksMode : CompatibilityMode.AlmostStandards;
                            }
                        }
                    }
                } else if (_tokenizer.IsIncorrectDoctype) {
                    _compatMode = CompatibilityMode.QuirksMode;
                }
                _phase = TreeConstructionPhase.Root;
                _pendingOutputTokens.Enqueue(_tokenizer.Token);
                return ParsingState.Pause;
            case XmlNodeType.Element:
            case XmlNodeType.EndElement:
            case XmlNodeType.Text:
                OnParseError("Unexpected non-space characters. Expected DOCTYPE.");
                _compatMode = CompatibilityMode.QuirksMode;
                _phase = TreeConstructionPhase.Root;
                return ParsingState.ReprocessCurrentToken;
            default:
                throw new InvalidOperationException();
            }
        }

        private ParsingState ParseRoot()
        {
            // http://www.whatwg.org/specs/web-apps/current-work/multipage/section-tree-construction.html#the-root1
            switch (_tokenizer.TokenType) {
            case XmlNodeType.DocumentType:
                OnParseError("Misplaced or duplicate DOCTYPE. Ignored.");
                return ParsingState.ProcessNextToken;
            case XmlNodeType.Comment:
                _pendingOutputTokens.Enqueue(_tokenizer.Token);
                return ParsingState.Pause;
            case XmlNodeType.Whitespace:
                return ParsingState.ProcessNextToken;
            case XmlNodeType.Text:
                _phase = TreeConstructionPhase.Main;
                ExtractLeadingWhitespace(); // ignore returned whitespace, because whitespace is ignore in this phase (see above)
                goto case XmlNodeType.EndElement;
            case XmlNodeType.Element:
                if (String.Equals(_tokenizer.Name, "html", StringComparison.Ordinal)) {
                    _phase = TreeConstructionPhase.Main;
                    // XXX: instead of creating an "html" node and then copying the attributes in the main phase, we just use directly the current "html" start tag token
                    return InsertHtmlElement();
                } else {
                    goto case XmlNodeType.EndElement;
                }
            case XmlNodeType.EndElement:
                InsertHtmlElement(Token.CreateStartTag("html"));
                _phase = TreeConstructionPhase.Main;
                return ParsingState.ReprocessCurrentToken;
            default:
                throw new InvalidOperationException();
            }
        }

        private ParsingState ParseMain()
        {
            // http://www.whatwg.org/specs/web-apps/current-work/multipage/section-tree-construction.html#how-to0
            switch (_tokenizer.TokenType) {
            case XmlNodeType.DocumentType:
                OnParseError("Unexpected DOCTYPE. Ignored");
                return ParsingState.ProcessNextToken;
            case XmlNodeType.Element:
            case XmlNodeType.EndElement:
            case XmlNodeType.Comment:
            case XmlNodeType.Text:
            case XmlNodeType.Whitespace:
                switch (_insertionMode) {
                case InsertionMode.BeforeHead:
                    return ParseMainBeforeHead();
                case InsertionMode.InHead:
                    return ParseMainInHead();
                case InsertionMode.InHeadNoscript:
                    return ParseMainInHeadNoscript();
                case InsertionMode.AfterHead:
                    return ParseMainAfterHead();
                case InsertionMode.InBody:
                    return ParseMainInBody();
                case InsertionMode.InTable:
                case InsertionMode.InCaption:
                case InsertionMode.InColumnGroup:
                case InsertionMode.InTableBody:
                case InsertionMode.InRow:
                case InsertionMode.InCell:
                case InsertionMode.InSelect:
                case InsertionMode.AfterBody:
                case InsertionMode.InFrameset:
                case InsertionMode.AfterFrameset:
                    throw new NotImplementedException();
                default:
                    throw new InvalidOperationException();
                }
            default:
                throw new InvalidOperationException();
            }
        }

        private ParsingState ParseMainBeforeHead()
        {
            // http://www.whatwg.org/specs/web-apps/current-work/multipage/section-tree-construction.html#before4
            switch (_tokenizer.TokenType) {
            case XmlNodeType.Whitespace:
            case XmlNodeType.Comment:
                _pendingOutputTokens.Enqueue(_tokenizer.Token);
                return ParsingState.Pause;
            case XmlNodeType.Text:
                string whitespace = ExtractLeadingWhitespace();
                _tokenizer.PushToken(Token.CreateStartTag("head"));
                if (whitespace.Length > 0) {
                    _tokenizer.PushToken(Token.CreateWhitespace(whitespace));
                    goto case XmlNodeType.Whitespace;
                } else {
                    return ParsingState.ReprocessCurrentToken;
                }
            case XmlNodeType.Element:
                if (_tokenizer.Name == "head") {
                    _insertionMode = InsertionMode.InHead;
                    return InsertHtmlElement();
                } else {
                    return ActAsIfTokenHadBeenSeenThenReprocessCurrentToken(Token.CreateStartTag("head"));
                }
            case XmlNodeType.EndElement:
                switch (_tokenizer.Name) {
                case "head":
                case "html":
                case "body":
                case "p":
                case "br":
                    return ActAsIfTokenHadBeenSeenThenReprocessCurrentToken(Token.CreateStartTag("head"));
                default:
                    OnParseError(String.Concat("Unexpected end tag (", _tokenizer.Name, "). Ignored."));
                    return ParsingState.ProcessNextToken;
                }
            default:
                throw new InvalidOperationException();
            }
        }

        private ParsingState ParseMainInHead()
        {
            // http://www.whatwg.org/specs/web-apps/current-work/multipage/section-tree-construction.html#parsing-main-inhead
            switch (_tokenizer.TokenType) {
            case XmlNodeType.Whitespace:
            case XmlNodeType.Comment:
                _pendingOutputTokens.Enqueue(_tokenizer.Token);
                return ParsingState.Pause;
            case XmlNodeType.Text:
                string whitespace = ExtractLeadingWhitespace();
                if (whitespace.Length > 0) {
                    _tokenizer.PushToken(Token.CreateWhitespace(whitespace));
                    goto case XmlNodeType.Whitespace;
                } else {
                    return ActAsIfTokenHadBeenSeenThenReprocessCurrentToken(Token.CreateEndTag("head"));
                }
            case XmlNodeType.Element:
                switch (_tokenizer.Name) {
                case "base":
                case "link":
                    return InsertHtmlElement();
                case "meta":
                    // TODO: change charset if needed
                    return InsertHtmlElement();
                case "title":
                    // FIXME: that's not what the spec says
                    _tokenizer.ContentModel = ContentModel.Rcdata;
                    return InsertHtmlElement();
                case "noscript":
                    // TODO: case when scripting is enabled:
                    //if (_scriptingElabled)
                    //{
                    //    goto case "style";
                    //}
                    // FIXME: that's not what the spec says
                    _insertionMode = InsertionMode.InHeadNoscript;
                    return InsertHtmlElement();
                case "style":
                    // FIXME: that's not what the spec says
                    _tokenizer.ContentModel = ContentModel.Cdata;
                    return InsertHtmlElement();
                case "script":
                    // FIXME: that's not what the spec says
                    _tokenizer.ContentModel = ContentModel.Cdata;
                    // TODO: script execution
                    return InsertHtmlElement();
                case "head":
                    OnParseError("Unexpected HEAD start tag. Ignored.");
                    return ParsingState.ProcessNextToken;
                default:
                    return ActAsIfTokenHadBeenSeenThenReprocessCurrentToken(Token.CreateEndTag("head"));
                }
            case XmlNodeType.EndElement:
                switch (_tokenizer.Name) {
                case "head":
#if DEBUG
                    Debug.Assert(_openElements.Pop().name == "head");
#else
                    _openElements.Pop();
#endif
                    // XXX: we don't emit the head end tag, we'll emit it later before switching to the "in body" insertion mode
                    _insertionMode = InsertionMode.AfterHead;
                    return ParsingState.ProcessNextToken;
                case "body":
                case "html":
                case "p":
                case "br":
                    return ActAsIfTokenHadBeenSeenThenReprocessCurrentToken(Token.CreateEndTag("head"));
                default:
                    OnParseError(String.Concat("Unexpected end tag (", _tokenizer.Name, "). Ignored."));
                    return ParsingState.ProcessNextToken;
                }
            default:
                throw new InvalidOperationException();
            }
        }

        private ParsingState ParseMainInHeadNoscript()
        {
            // http://www.whatwg.org/specs/web-apps/current-work/multipage/section-tree-construction.html#in-head0
            throw new NotImplementedException();
        }

        private ParsingState ParseMainAfterHead()
        {
            // http://www.whatwg.org/specs/web-apps/current-work/multipage/section-tree-construction.html#after3
            switch (_tokenizer.TokenType) {
            case XmlNodeType.Whitespace:
            case XmlNodeType.Comment:
                _pendingOutputTokens.Enqueue(_tokenizer.Token);
                return ParsingState.Pause;
            case XmlNodeType.Text:
                string whitespace = ExtractLeadingWhitespace();
                if (whitespace.Length > 0) {
                    _tokenizer.PushToken(Token.CreateWhitespace(whitespace));
                    goto case XmlNodeType.Whitespace;
                } else {
                    return ActAsIfTokenHadBeenSeenThenReprocessCurrentToken(Token.CreateStartTag("body"));
                }
            case XmlNodeType.Element:
                switch (_tokenizer.Name) {
                case "body":
                    // XXX: that's where we emit the head end tag
                    _pendingOutputTokens.Enqueue(Token.CreateEndTag("head"));
                    _insertionMode = InsertionMode.InBody;
                    return InsertHtmlElement();
                case "frameset":
                    // XXX: that's where we emit the head end tag
                    _pendingOutputTokens.Enqueue(Token.CreateEndTag("head"));
                    _insertionMode = InsertionMode.InFrameset;
                    return InsertHtmlElement();
                case "base":
                case "link":
                case "meta":
                case "script":
                case "style":
                case "title":
                    OnParseError(String.Concat("Unexpected start tag (", _tokenizer.Name, "). Should be in head."));
                    // TODO: reprocess as if "in head"
                    return ParseMainInHead();
                default:
                    return ActAsIfTokenHadBeenSeenThenReprocessCurrentToken(Token.CreateStartTag("body"));
                }
            case XmlNodeType.EndElement:
                return ActAsIfTokenHadBeenSeenThenReprocessCurrentToken(Token.CreateStartTag("body"));
            default:
                throw new InvalidOperationException();
            }
        }

        private ParsingState ParseMainInBody()
        {
            // http://www.whatwg.org/specs/web-apps/current-work/multipage/section-tree-construction.html#in-body
            switch (_tokenizer.TokenType) {
            case XmlNodeType.Whitespace:
            case XmlNodeType.Text:
                ReconstructActiveFormattingElements();
                _pendingOutputTokens.Enqueue(_tokenizer.Token);
                return ParsingState.Pause;
            case XmlNodeType.Comment:
                _pendingOutputTokens.Enqueue(_tokenizer.Token);
                return ParsingState.Pause;
            case XmlNodeType.Element:
                switch (_tokenizer.Name) {
                case "base":
                case "link":
                case "meta":
                case "script":
                case "style":
                    // TODO: reprocess as if "in head"
                    return ParseMainInHead();
                case "title":
                    OnParseError("Unexpected title start tag in body.");
                    // TODO: reprocess as if "in head"
                    return ParseMainInHead();
                case "body":
                    // TODO: fragment case
                    if (_tokenizer.HasAttributes) {
                        OnParseError("Unexpected body start tag in body. NOT ignored because of attributes.");
                        return InsertHtmlElement();
                    } else {
                        OnParseError("Unexpected body start tag in body. Ignored (no attribute).");
                        return ParsingState.ProcessNextToken;
                    }
                case "address":
                case "blockquote":
                case "center":
                case "dir":
                case "div":
                case "dl":
                case "fieldset":
                case "listing":
                case "menu":
                case "ol":
                case "p":
                case "ul":
                    if (IsInScope("p", false)) {
                        // XXX: that's not what the spec says but it has the same result
                        return ActAsIfTokenHadBeenSeenThenReprocessCurrentToken(Token.CreateEndTag("p"));
                    } else {
                        return InsertHtmlElement();
                    }
                case "pre":
                    // FIXME: don't micromanage "<pre>\n", treat as a above for now (don't now it this actually needs fixing or if it'll be handle at the "real tree construction stage")
                    goto case "p";
                case "form":
                    // XXX: no "form element pointer", so no ParseError (will be handle at the "real tree construction stage")
                    goto case "p";
                case "li":
                    if (IsInScope("p", false)) {
                        // XXX: that's not what the spec says but it has the same result
                        return ActAsIfTokenHadBeenSeenThenReprocessCurrentToken(Token.CreateEndTag("p"));
                    } else {
                        throw new NotImplementedException();
                    }
                case "dd":
                case "dt":
                    if (IsInScope("p", false)) {
                        // XXX: that's not what the spec says but it has the same result
                        return ActAsIfTokenHadBeenSeenThenReprocessCurrentToken(Token.CreateEndTag("p"));
                    } else {
                        throw new NotImplementedException();
                    }
                case "plaintext":
                    if (IsInScope("p", false)) {
                        // XXX: that's not what the spec says but it has the same result
                        return ActAsIfTokenHadBeenSeenThenReprocessCurrentToken(Token.CreateEndTag("p"));
                    } else {
                        _tokenizer.ContentModel = ContentModel.PlainText;
                        return InsertHtmlElement();
                    }
                case "h1":
                case "h2":
                case "h3":
                case "h4":
                case "h5":
                case "h6":
                    goto case "p";
                case "a":
                    Token a = ElementInActiveFormattingElements("a");
                    if (a != null) {
                        OnParseError("Unexpected start tag (a) implies end tag (a)");
                        // TODO: act as if an "a" end tag had been seen then remove 'a' from _openElements and _activeFormattingElements.Peek().
                        throw new NotImplementedException();
                    }
                    ReconstructActiveFormattingElements();
                    _activeFormattingElements.Peek().AddLast(_tokenizer.Token);
                    return InsertHtmlElement();
                case "b":
                case "big":
                case "em":
                case "font":
                case "i":
                case "s":
                case "small":
                case "strike":
                case "strong":
                case "tt":
                case "u":
                    ReconstructActiveFormattingElements();
                    _activeFormattingElements.Peek().AddLast(_tokenizer.Token);
                    return InsertHtmlElement();
                case "nobr":
                    ReconstructActiveFormattingElements();
                    if (ElementInActiveFormattingElements("nobr") != null) {
                        OnParseError("Unexpected start tag (nobr) implies end tag (nobr)");
                        return ActAsIfTokenHadBeenSeenThenReprocessCurrentToken(Token.CreateEndTag("nobr"));
                    }
                    _activeFormattingElements.Peek().AddLast(_tokenizer.Token);
                    return InsertHtmlElement();
                case "button":
                    if (IsInScope("button", false)) {
                        OnParseError("Unexpected end tag (button) implies end tag (button)");
                        return ActAsIfTokenHadBeenSeenThenReprocessCurrentToken(Token.CreateEndTag("button"));
                    }
                    ReconstructActiveFormattingElements();
                    _activeFormattingElements.Push(new LinkedList<Token>());
                    return InsertHtmlElement();
                case "marquee":
                case "object":
                    ReconstructActiveFormattingElements();
                    _activeFormattingElements.Push(new LinkedList<Token>());
                    return InsertHtmlElement();
                case "xmp":
                    ReconstructActiveFormattingElements();
                    // FIXME: that's not what the spec says
                    _tokenizer.ContentModel = ContentModel.Cdata;
                    return InsertHtmlElement();
                case "table":
                    if (IsInScope("p", false)) {
                        // XXX: that's not what the spec says but it has the same result
                        return ActAsIfTokenHadBeenSeenThenReprocessCurrentToken(Token.CreateEndTag("p"));
                    } else {
                        _insertionMode = InsertionMode.InTable;
                        return InsertHtmlElement();
                    }
                case "area":
                case "basefont":
                case "bgsound":
                case "br":
                case "embed":
                case "img":
                case "param":
                case "spacer":
                case "wbr":
                    ReconstructActiveFormattingElements();
#if DEBUG
                    Debug.Assert(_openElements.Pop().name == _tokenizer.Name);
#else
                    _openElements.Pop();
#endif
                    return InsertHtmlElement();
                case "hr":
                    if (IsInScope("p", false)) {
                        // XXX: that's not what the spec says but it has the same result
                        return ActAsIfTokenHadBeenSeenThenReprocessCurrentToken(Token.CreateEndTag("p"));
                    } else {
#if DEBUG
                        Debug.Assert(_openElements.Pop().name == _tokenizer.Name);
#else
                    _openElements.Pop();
#endif
                        return InsertHtmlElement();
                    }
                case "image":
                    OnParseError("Unexpected start tag (image). Treated as img.");
                    Token token = _tokenizer.Token;
                    token.name = "img";
                    _tokenizer.ReplaceToken(token);
                    return ParsingState.ReprocessCurrentToken;
                case "input":
                    throw new NotImplementedException();
                case "isindex":
                    throw new NotImplementedException();
                case "textarea":
                    throw new NotImplementedException();
                case "iframe":
                case "noembed":
                case "noframe":
                    throw new NotImplementedException();
                case "noscript":
                    // TODO: case when scripting is enabled
                    throw new NotImplementedException();
                case "select":
                    throw new NotImplementedException();
                case "caption":
                case "col":
                case "colgroup":
                case "frame":
                case "frameset":
                case "head":
                case "option":
                case "optgroup":
                case "tbody":
                case "td":
                case "tfoot":
                case "th":
                case "thead":
                case "tr":
                    throw new NotImplementedException();
                case "event-source":
                case "section":
                case "nav":
                case "article":
                case "aside":
                case "header":
                case "footer":
                case "datagrid":
                case "command":
                    throw new NotImplementedException();
                default:
                    ReconstructActiveFormattingElements();
                    return InsertHtmlElement();
                }
            case XmlNodeType.EndElement:
                throw new NotImplementedException();
            default:
                throw new InvalidOperationException();
            }
        }

        public override bool ReadAttributeValue()
        {
            throw new NotImplementedException();
        }

        public override void ResolveEntity()
        {
            throw new InvalidOperationException();
        }

        #region IXmlLineInfo Membres

        public bool HasLineInfo()
        {
            if (_lineInfo == null) {
                return false;
            }
            return _lineInfo.HasLineInfo();
        }

        public int LineNumber
        {
            get
            {
                if (_lineInfo == null) {
                    return 0;
                }
                return _lineInfo.LineNumber;
            }
        }

        public int LinePosition
        {
            get
            {
                if (_lineInfo == null) {
                    return 0;
                }
                return _lineInfo.LinePosition;
            }
        }

        #endregion
    }
}
