#if !NUNIT
using Microsoft.VisualStudio.TestTools.UnitTesting;
#else
using NUnit.Framework;
#endif

using System;
using System.Collections.Generic;
using System.Xml;

namespace Twintsam.Html
{
    internal class LintXmlReader : XmlReader, IXmlLineInfo
    {
        private readonly XmlReader _reader;
        private readonly IXmlLineInfo _lineInfo;
        private readonly Stack<string> _openElements = new Stack<string>();

        public LintXmlReader(XmlReader reader)
        {
            if (reader == null) {
                throw new ArgumentNullException("reader");
            }
            _reader = reader;
            _lineInfo = reader as IXmlLineInfo;
        }

        public override int AttributeCount
        {
            get { return _reader.AttributeCount; }
        }

        public override string BaseURI
        {
            get { return _reader.BaseURI; }
        }

        public override void Close()
        {
            _reader.Close();
        }

        public override int Depth
        {
            get { return _reader.Depth; }
        }

        public override bool EOF
        {
            get { return _reader.EOF; }
        }

        public override string GetAttribute(int i)
        {
            return _reader.GetAttribute(i);
        }

        public override string GetAttribute(string name, string namespaceURI)
        {
            return _reader.GetAttribute(name, namespaceURI);
        }

        public override string GetAttribute(string name)
        {
            return _reader.GetAttribute(name);
        }

        public override bool HasValue
        {
            get { return _reader.HasValue; }
        }

        public override bool IsEmptyElement
        {
            get { return _reader.IsEmptyElement; }
        }

        public override string LocalName
        {
            get { return _reader.LocalName; }
        }

        public override string LookupNamespace(string prefix)
        {
            return _reader.LookupNamespace(prefix);
        }

        public override bool MoveToAttribute(string name, string ns)
        {
            return _reader.MoveToAttribute(name, ns);
        }

        public override bool MoveToAttribute(string name)
        {
            return _reader.MoveToAttribute(name);
        }

        public override bool MoveToElement()
        {
            return _reader.MoveToElement();
        }

        public override bool MoveToFirstAttribute()
        {
            return _reader.MoveToFirstAttribute();
        }

        public override bool MoveToNextAttribute()
        {
            return _reader.MoveToNextAttribute();
        }

        public override XmlNameTable NameTable
        {
            get { return _reader.NameTable; }
        }

        public override string NamespaceURI
        {
            get { return _reader.NamespaceURI; }
        }

        public override XmlNodeType NodeType
        {
            get { return _reader.NodeType; }
        }

        public override string Prefix
        {
            get { return _reader.Prefix; }
        }

        public override bool Read()
        {
            if (_reader.Read()) {
                if (_reader.NodeType == XmlNodeType.EndElement) {
                    string openElement = _openElements.Pop();
                    Assert.AreEqual(_reader.Name, openElement);
                }
                Assert.AreEqual(_openElements.Count, _reader.Depth);
                if (_reader.NodeType == XmlNodeType.Element && !_reader.IsEmptyElement) {
                    _openElements.Push(_reader.Name);
                }
                return true;
            } else {
                Assert.AreEqual(0, _openElements.Count);
                return false;
            }
        }

        public override bool ReadAttributeValue()
        {
            throw new NotSupportedException();
        }

        public override ReadState ReadState
        {
            get { return _reader.ReadState; }
        }

        public override void ResolveEntity()
        {
            throw new NotSupportedException();
        }

        public override string Value
        {
            get { return _reader.Value; }
        }

        #region IXmlLineInfo Membres

        public bool HasLineInfo()
        {
            return (_lineInfo == null) ? false : _lineInfo.HasLineInfo();
        }

        public int LineNumber
        {
            get { return (_lineInfo == null) ? 0 : _lineInfo.LineNumber; }
        }

        public int LinePosition
        {
            get { return (_lineInfo == null) ? 0 : _lineInfo.LinePosition; }
        }

        #endregion
    }
}
