using System;
using System.Xml;

namespace Twintsam.Html
{
    internal class Attribute : IXmlLineInfo
    {
        public string name;
        public string value = "";
        public char quoteChar = ' ';

        private bool _hasLineInfo;
        private int _lineNumber;
        private int _linePosition;

        public Attribute(string name, IXmlLineInfo lineInfo)
        {
            this.name = name;

            _hasLineInfo = lineInfo.HasLineInfo();
            _lineNumber = lineInfo.LineNumber;
            _linePosition = lineInfo.LinePosition;
        }

        #region IXmlLineInfo Members
        public bool HasLineInfo() { return _hasLineInfo; }
        public int LineNumber { get { return _lineNumber; } }
        public int LinePosition { get { return _linePosition; } }
        #endregion
    }
}
