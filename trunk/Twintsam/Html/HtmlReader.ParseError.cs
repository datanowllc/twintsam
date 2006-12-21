using System;
using System.Xml;

namespace Twintsam.Html
{
    public partial class HtmlReader
    {
        private bool _isParseErrorFatal;

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
    }
}
