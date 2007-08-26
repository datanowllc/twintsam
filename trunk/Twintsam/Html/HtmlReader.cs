using System;
using System.Collections.Generic;
using System.IO;
using System.Xml;
using System.Diagnostics;

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
            public static Token CreateFromTokenizer(HtmlTokenizer tokenizer)
            {
                Token token = new Token();
                token.tokenType = tokenizer.TokenType;
                token.name = tokenizer.Name;
                token.value = tokenizer.Value;
                token.isIncorrectDoctype = tokenizer.IsIncorrectDoctype;
                token.hasTrailingSolidus = tokenizer.HasTrailingSolidus;
                HtmlTextTokenizer textTokenizer = tokenizer as HtmlTextTokenizer;
                if (textTokenizer != null) {
                    token.attributes.AddRange(textTokenizer._attributes.ConvertAll<Attribute>(
                        delegate(HtmlTextTokenizer.Attribute attribute) {
                            return attribute;
                        }));
                } else {
                    for (int i = 0; i < tokenizer.AttributeCount; i++) {
                        token.attributes.Add(CreateAttributeFromTokenizer(tokenizer, i));
                    }
                }
                return token;
            }
            private static Attribute CreateAttributeFromTokenizer(HtmlTokenizer tokenizer, int i)
            {
                Attribute attribute = new Attribute(tokenizer.GetAttributeName(i), tokenizer as IXmlLineInfo);
                attribute.value = tokenizer.GetAttribute(i);
                attribute.quoteChar = tokenizer.GetAttributeQuoteChar(i);
                return attribute;
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
            InHeadNoScript,
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
            private Token _pendingToken;
            private bool _pendingTokenActive;

            public Tokenizer(HtmlTokenizer tokenizer) : base(tokenizer) { }

            public override int AttributeCount
            {
                get
                {
                    if (_pendingTokenActive && _pendingToken != null) {
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
                    if (_pendingTokenActive && _pendingToken != null) {
                        return false;
                    }
                    return base.EOF;
                }
            }
            public override string GetAttribute(int index)
            {
                if (_pendingTokenActive && _pendingToken != null) {
                    if (index < 0 || index >= _pendingToken.attributes.Count) {
                        throw new ArgumentOutOfRangeException("index");
                    }
                    return _pendingToken.attributes[index].value;
                }
                return base.GetAttribute(index);
            }
            public override string GetAttribute(string name)
            {
                if (_pendingTokenActive && _pendingToken != null) {
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
                if (_pendingTokenActive && _pendingToken != null) {
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
                if (_pendingTokenActive && _pendingToken != null) {
                    if (index < 0 || index >= _pendingToken.attributes.Count) {
                        throw new ArgumentOutOfRangeException("index");
                    }
                    return _pendingToken.attributes[index].name;
                }
                return base.GetAttributeName(index);
            }
            public override char GetAttributeQuoteChar(int index)
            {
                if (_pendingTokenActive && _pendingToken != null) {
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
                    if (_pendingTokenActive && _pendingToken != null) {
                        return _pendingToken.attributes.Count > 0;
                    }
                    return base.HasAttributes;
                }
            }
            public override bool HasTrailingSolidus
            {
                get
                {
                    if (_pendingTokenActive && _pendingToken != null) {
                        return _pendingToken.hasTrailingSolidus;
                    }
                    return base.HasTrailingSolidus;
                }
            }
            public override bool IsIncorrectDoctype
            {
                get
                {
                    if (_pendingTokenActive && _pendingToken != null) {
                        return _pendingToken.isIncorrectDoctype;
                    }
                    return base.IsIncorrectDoctype;
                }
            }
            public override string Name
            {
                get
                {
                    if (_pendingTokenActive && _pendingToken != null) {
                        return _pendingToken.name;
                    }
                    return base.Name;
                }
            }
            public override bool Read()
            {
                if (_pendingToken != null) {
                    if (_pendingTokenActive) {
                        _pendingToken = null;
                        return base.EOF;
                    } else {
                        _pendingTokenActive = true;
                        return true;
                    }
                }
                return base.Read();
            }
            public override ReadState ReadState
            {
                get
                {
                    if (_pendingTokenActive && _pendingToken != null) {
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
                    if (_pendingTokenActive && _pendingToken != null) {
                        return _pendingToken.tokenType;
                    }
                    return base.TokenType;
                }
            }
            public override string Value
            {
                get
                {
                    if (_pendingTokenActive && _pendingToken != null) {
                        return _pendingToken.value;
                    }
                    return base.Value;
                }
            }

            internal void SetToken(Token token)
            {
                if (_pendingToken != null) {
                    throw new InvalidOperationException();
                }
                _pendingToken = token;
                _pendingTokenActive = false;
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

        private void ReconstructActiveFormattingElements()
        {
            LinkedListNode<Token> element = _activeFormattingElements.Peek().Last;
            if (element != null){
                if (element.Value != null && _openElements.Contains(element.Value)) {
                    while (element.Previous != null) {
                        element = element.Previous;
                        if (element.Value == null || _openElements.Contains(element.Value)) {
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

        private void ClearActiveFormattingElements()
        {
            _activeFormattingElements.Pop();
        }
        #endregion

        #region 8.2.4.3.4. Closing elements that have implied end tags
        // http://www.whatwg.org/specs/web-apps/current-work/multipage/section-tree-construction.html#generate
        private bool GenerateImpliedEndTags(string omitted)
        {
            Debug.Assert(omitted == null || String.Equals(omitted, omitted.ToLowerInvariant(), StringComparison.Ordinal));

            string element = _openElements.Peek().name;
            if ((omitted == null || String.Equals(element, omitted, StringComparison.Ordinal))
                && Constants.HasOptionalEndTag(element)) {
                _tokenizer.SetToken(Token.CreateEndTag(element));
                return true;
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
            get { throw new NotImplementedException(); }
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
            if (AttributeCount > 0) {
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
            bool newToken = false;
            while (!newToken && _tokenizer.Read()) {
                switch (_phase) {
                case TreeConstructionPhase.Initial:
                    newToken = ParseInitial();
                    break;
                case TreeConstructionPhase.Root:
                    newToken = ParseRoot();
                    break;
                case TreeConstructionPhase.Main:
                    throw new NotImplementedException();
                case TreeConstructionPhase.TrailingEnd:
                    throw new NotImplementedException();
                default:
                    throw new InvalidOperationException();
                }
            }
            if (EOF) {
                return ProcessEndOfFile();
            }
            return true;
        }

        private bool ProcessEndOfFile()
        {
            switch (_phase) {
            case TreeConstructionPhase.Initial:
                OnParseError("Unexpected end of stream. Expected DOCTYPE.");
                _compatMode = CompatibilityMode.QuirksMode;
                goto case TreeConstructionPhase.Root;
            case TreeConstructionPhase.Root:
                Token token = Token.CreateStartTag("html");
                _pendingOutputTokens.Enqueue(token);
                _openElements.Push(token);
                _phase = TreeConstructionPhase.Main;
                goto case TreeConstructionPhase.Main;
            case TreeConstructionPhase.Main:
                bool generated = GenerateImpliedEndTags(null);
                if (_openElements.Count > 2
                    || (_openElements.Count == 2
                        && String.Equals(_openElements.Peek().name, "body", StringComparison.Ordinal))) {
                    OnParseError("???");
                }
                return generated;
            case TreeConstructionPhase.TrailingEnd:
                return false; // Nothing to do
            default:
                throw new InvalidOperationException();
            }
        }

        private bool ParseInitial()
        {
            switch (_tokenizer.TokenType) {
            case XmlNodeType.Whitespace:
                return false;
            case XmlNodeType.Comment:
                return true;
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
                return true;
            case XmlNodeType.Element:
            case XmlNodeType.EndElement:
            case XmlNodeType.Text:
                OnParseError("Unexpected end of stream in initial phase");
                _compatMode = CompatibilityMode.QuirksMode;
                _phase = TreeConstructionPhase.Root;
                return false;
            default:
                throw new InvalidOperationException();
            }
        }

        private bool ParseRoot()
        {
            switch (_tokenizer.TokenType) {
            case XmlNodeType.DocumentType:
                OnParseError("Misplaced or duplicate DOCTYPE. Ignored.");
                return false;
            case XmlNodeType.Comment:
                return true;
            case XmlNodeType.Whitespace:
                return false; // Ignore the token
            case XmlNodeType.Text:
            case XmlNodeType.Element:
            case XmlNodeType.EndElement:
                // XXX: attributes?
                _pendingOutputTokens.Enqueue(Token.CreateStartTag("html"));
                _phase = TreeConstructionPhase.Main;
                return true;
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
