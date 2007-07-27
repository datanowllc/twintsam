using System;
using System.Xml;

namespace Twintsam.Html
{
    public class ParseErrorEventArgs : EventArgs, IXmlLineInfo
    {
        private string _message;
        private bool _hasLineInfo;
        private int _lineNumber;
        private int _linePosition;

        protected internal ParseErrorEventArgs(string message, IXmlLineInfo lineInfo)
        {
            this._message = message;
            if (lineInfo != null) {
                this._hasLineInfo = lineInfo.HasLineInfo();
                this._lineNumber = lineInfo.LineNumber;
                this._linePosition = lineInfo.LinePosition;
            }
        }

        public string Message
        {
            get { return _message; }
        }

        #region IXmlLineInfo Members

        public bool HasLineInfo()
        {
            return _hasLineInfo;
        }

        public int LineNumber
        {
            get { return _lineNumber; }
        }

        public int LinePosition
        {
            get { return _linePosition; }
        }

        #endregion
    }
}
