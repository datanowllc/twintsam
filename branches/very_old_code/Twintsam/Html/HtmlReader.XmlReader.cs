using System;
using System.Xml;
using System.Diagnostics;

namespace Twintsam.Html
{
    public partial class HtmlReader : XmlReader
    {
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

        private XmlNameTable nameTable = new NameTable();
        public override XmlNameTable NameTable
        {
            get { return nameTable; }
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

        public override string Prefix
        {
            get { return String.Empty; }
        }

        public override void ResolveEntity()
        {
            Debug.Assert(NodeType != XmlNodeType.EntityReference, "An EntityReference node shuld not ever be produced.");
            throw new InvalidOperationException();
        }
    }
}
