using System;
using System.Xml;
using System.Diagnostics;

namespace Twintsam.Html
{
    public partial class HtmlReader : XmlReader
    {
        private ReadState _readState = ReadState.Initial;


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
