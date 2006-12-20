using System;
using System.Collections.Generic;
using System.Text;
using System.Xml;
using System.Diagnostics;

namespace Twintsam.Html
{
    public partial class HtmlReader : XmlReader
    {
        private bool _isParseErrorFatal;
        private ReadState _readState = ReadState.Initial;

        #region Constructors
        // TODO: public constructors

        private HtmlReader()
        {
            // http://www.whatwg.org/specs/web-apps/current-work/#tokenization:
            // The state machine must start in the data state.
            _currentParsingFunction = ParseData;
        }
        #endregion

        #region Parse Error
        public event EventHandler<ParseErrorEventArgs> ParseError;

        public bool IsParseErrorFatal
        {
            get { return _isParseErrorFatal; }
            set
            {
                if (ReadState != ReadState.Initial) {
                    throw new InvalidOperationException();
                }
                _isParseErrorFatal = value;
            }
        }

        protected void OnParseError(string message)
        {
            ParseErrorEventArgs args = new ParseErrorEventArgs(message, this);

            if (ParseError != null) {
                ParseError(this, args);
            }

            if (IsParseErrorFatal) {
                _readState = ReadState.Error;
                throw new XmlException(args.Message, null, args.LineNumber, args.LinePosition);
            }
        }
        #endregion

        public override int AttributeCount
        {
            get { throw new NotImplementedException(); }
        }

        public override string BaseURI
        {
            get { throw new NotImplementedException(); }
        }

        public override void Close()
        {
            _readState = ReadState.Closed;
            throw new NotImplementedException();
        }

        public override int Depth
        {
            get { throw new NotImplementedException(); }
        }

        public override bool EOF
        {
            get { return _readState == ReadState.EndOfFile; }
        }

        public override string GetAttribute(int i)
        {
            throw new NotImplementedException();
        }

        public override string GetAttribute(string name, string namespaceURI)
        {
            throw new NotImplementedException();
        }

        public override string GetAttribute(string name)
        {
            throw new NotImplementedException();
        }

        public override bool HasValue
        {
            get { throw new NotImplementedException(); }
        }

        public override bool IsEmptyElement
        {
            get { return Constants.IsVoidElement(LocalName); }
        }

        public override string LocalName
        {
            get { throw new NotImplementedException(); }
        }

        public override string LookupNamespace(string prefix)
        {
            if (prefix == null) {
                throw new ArgumentNullException("prefix");
            }
            if (prefix.Length == 0) {
                return NameTable.Add(Constants.XhtmlNamespaceUri);
            }
            return null;
        }

        public override bool MoveToAttribute(string name, string ns)
        {
            throw new NotImplementedException();
        }

        public override bool MoveToAttribute(string name)
        {
            throw new NotImplementedException();
        }

        public override bool MoveToElement()
        {
            throw new NotImplementedException();
        }

        public override bool MoveToFirstAttribute()
        {
            throw new NotImplementedException();
        }

        public override bool MoveToNextAttribute()
        {
            throw new NotImplementedException();
        }

        public override XmlNameTable NameTable
        {
            get { throw new NotImplementedException(); }
        }

        public override string NamespaceURI
        {
            get
            {
                return (NodeType == XmlNodeType.Element
                    || NodeType == XmlNodeType.EndElement)
                    ? Constants.XhtmlNamespaceUri
                    : String.Empty;
            }
        }

        public override XmlNodeType NodeType
        {
            get { throw new NotImplementedException(); }
        }

        public override string Prefix
        {
            get { return String.Empty; }
        }

        public override bool Read()
        {
            throw new NotImplementedException();
        }

        public override bool ReadAttributeValue()
        {
            throw new NotImplementedException();
        }

        public override ReadState ReadState
        {
            get { return _readState; }
        }

        public override void ResolveEntity()
        {
            Debug.Assert(NodeType != XmlNodeType.EntityReference);
            throw new InvalidOperationException();
        }

        public override string Value
        {
            get { throw new NotImplementedException(); }
        }
    }
}
