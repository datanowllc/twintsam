using System;
using System.Collections.Generic;
using System.IO;
using System.Xml;

namespace Twintsam.Html
{
    public partial class HtmlReader : XmlReader, IXmlLineInfo
    {
        private static readonly string[] QuirksModeDoctypePublicIds = {
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
            "-//o'reilly and associates//dtd html extended relaxed 1.0//en",
            "-//softquad software//dtd hotmetal pro 6.0::19990601::extensions to html 4.0//en",
            "-//softquad//dtd hotmetal pro 4.0::19971010::extensions to html 4.0//en",
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

        private static readonly string[] QuirksModeDoctypePublicIdsWhenSystemIdIsMissing = {
            "-//w3c//dtd html 4.01 frameset//EN", "-//w3c//dtd html 4.01 transitional//EN"
        };

        private static readonly string QuirksModeDoctypeSystemId = "http://www.ibm.com/data/dtd/v11/ibmxhtml1-transitional.dtd";

        private static readonly string[] AlmostStandardsModeDoctypePublicIds = {
            "-//W3C//DTD XHTML 1.0 Frameset//EN", "-//W3C//DTD XHTML 1.0 Transitional//EN",
        };

        private /*readonly*/ Tokenizer _tokenizer;
        private /*readonly*/ IXmlLineInfo _lineInfo;
        private InsertionMode _insertionMode;
        private bool _inCdataOrRcdata;
        private bool _headParsed;
        private bool _inForm;

        private readonly Queue<Token> _pendingOutputTokens = new Queue<Token>();
        private int _attributeIndex = -1;
        private bool _inAttributeValue;
        private int _depth;

        private CompatibilityMode _compatMode = CompatibilityMode.NoQuirks;

        #region Constructors
        // TODO: public constructors

        public HtmlReader(TextReader reader)
        {
            if (reader == null) {
                throw new ArgumentNullException("reader");
            }
            Init(HtmlTokenizer.Create(reader));
        }

        public HtmlReader(TextReader reader, string fragmentContainer)
        {
            if (reader == null) {
                throw new ArgumentNullException("reader");
            }
            Init(HtmlTokenizer.Create(reader, fragmentContainer));
        }

        public HtmlReader(HtmlTokenizer tokenizer)
        {
            if (tokenizer == null) {
                throw new ArgumentNullException("tokenizer");
            }
            Init(tokenizer);
        }
        #endregion

        private bool FragmentCase
        {
            get
            {
                return _tokenizer.IsFragmentTokenizer;
            }
        }
        private void Init(HtmlTokenizer tokenizer)
        {
            _lineInfo = tokenizer as IXmlLineInfo;
            tokenizer.ParseError += new EventHandler<ParseErrorEventArgs>(tokenizer_ParseError);
            _tokenizer = new Tokenizer(tokenizer);
            
            if (FragmentCase) {
                _openElements.AddFirst(Token.CreateStartTag("html"));
                ResetInsertionMode();
            }
        }
        private void tokenizer_ParseError(object sender, ParseErrorEventArgs e)
        {
            OnParseError(e);
        }

        public override void Close()
        {
            // TODO: add flag to eventually not close the tokenizer
            // (particularly when it was passed to the constructor)
            _tokenizer.Close();
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
                    return _inAttributeValue ? XmlNodeType.Text : XmlNodeType.Attribute;
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
                if (_attributeIndex >= 0 && _inAttributeValue) {
                    return String.Empty;
                }
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
                if (NodeType != XmlNodeType.Element) {
                    return false;
                }
                if (Constants.IsVoidElement(Name)) {
                    return true;
                }
                // Special case for misplaced elements (html with attributes inside body for example is emitted as an empty html element)
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
                if (_pendingOutputTokens.Count > 0) {
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
                if (_attributeIndex >= 0) {
                    if (_inAttributeValue) {
                        return _depth + 2;
                    }
                    return _depth + 1;
                }
                return _depth;
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
            if (_attributeIndex >= 0 && _inAttributeValue) {
                return -1;
            }
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
            if (_attributeIndex >= 0 && _inAttributeValue) {
                throw new ArgumentOutOfRangeException("i");
            }
            int attributeIndex = _attributeIndex;
            _attributeIndex = -1;
            if (i < 0 || i >= AttributeCount) {
                _attributeIndex = attributeIndex;
                throw new ArgumentOutOfRangeException("i");
            }
            _attributeIndex = i;
            _inAttributeValue = false;
        }

        public override bool MoveToAttribute(string name, string ns)
        {
            if (_attributeIndex >= 0 && _inAttributeValue) {
                return false;
            }
            int attributeIndex = _attributeIndex;
            _attributeIndex = -1;
            if (NodeType == XmlNodeType.Element || NodeType == XmlNodeType.DocumentType) {
                if (String.IsNullOrEmpty(ns)
                    || String.Equals(ns, Constants.XhtmlNamespaceUri, StringComparison.Ordinal)) {
                    _attributeIndex = GetAttributeIndex(name);
                    _inAttributeValue = false;
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
            if (_attributeIndex >= 0 && !_inAttributeValue) {
                _attributeIndex = -1;
                return true;
            }
            return false;
        }

        public override bool MoveToFirstAttribute()
        {
            if (_attributeIndex >= 0 && _inAttributeValue) {
                return false;
            }
            int attributeIndex = _attributeIndex;
            _attributeIndex = -1;
            if (AttributeCount <= 0) {
                _attributeIndex = attributeIndex;
                return false;
            }
            _attributeIndex = 0;
            _inAttributeValue = false;
            return true;
        }

        public override bool MoveToNextAttribute()
        {
            if (_attributeIndex >= 0 && _inAttributeValue) {
                return false;
            }
            if (_attributeIndex < 0) {
                return MoveToFirstAttribute();
            }
            _attributeIndex++;
            if (_attributeIndex < 0 || _attributeIndex >= AttributeCount) {
                _attributeIndex--;
                return false;
            }
            _inAttributeValue = false;
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

        public override bool ReadAttributeValue()
        {
            if (_attributeIndex < 0) {
                return false;
            }
            _inAttributeValue = !_inAttributeValue;
            return _inAttributeValue;
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
                if (_attributeIndex >= 0 && _pendingOutputTokens.Count > 0) {
                    return _pendingOutputTokens.Peek().attributes[_attributeIndex].LineNumber;
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
                if (_attributeIndex >= 0 && _pendingOutputTokens.Count > 0) {
                    return _pendingOutputTokens.Peek().attributes[_attributeIndex].LinePosition;
                }
                return _lineInfo.LinePosition;
            }
        }

        #endregion
    }
}
