using System;
using System.IO;
using System.Text;
using System.Xml;

namespace Twintsam.Html
{
    public abstract class HtmlTokenizer : IDisposable
    {
        #region Factory
        // TODO: add overloads with 'string inputUri' and Stream, and settings
        public static HtmlTokenizer Create(TextReader input)
        {
            return new HtmlTextTokenizer(input);
        }

        public static HtmlTokenizer Create(TextReader input, string lastEmittedStartTagName)
        {
            return new HtmlTextTokenizer(input, lastEmittedStartTagName);
        }
        #endregion

        public abstract bool IsFragmentTokenizer { get; }
        public abstract string FragmentContext { get; }

        public abstract ReadState ReadState { get; }
        public abstract bool EOF { get; }

        public abstract XmlNameTable NameTable { get; }

        public abstract ContentModel ContentModel { get; set; }

        public abstract XmlNodeType TokenType { get; }
        public abstract string Name { get; }
        public abstract bool HasTrailingSolidus { get; }
        public abstract bool IsIncorrectDoctype { get; }
        public abstract string Value { get; }
        public virtual bool HasAttributes { get { return AttributeCount > 0; } }
        public abstract int AttributeCount { get; }
        public virtual string this[int index] { get { return GetAttribute(index); } }
        public virtual string this[string name] { get { return GetAttribute(name); } }

        public abstract bool Read();
        public abstract string GetAttributeName(int index);
        public virtual char GetAttributeQuoteChar(int index) { return '"'; }
        public abstract string GetAttribute(int index);
        public virtual int GetAttributeIndex(string name)
        {
            int attributeCount = AttributeCount;
            for (int i = 0; i < attributeCount; i++) {
                if (String.Equals(GetAttributeName(i), name, StringComparison.OrdinalIgnoreCase)) {
                    return i;
                }
            }
            return -1;
        }
        public virtual string GetAttribute(string name)
        {
            int attributeIndex = GetAttributeIndex(name);
            if (attributeIndex < 0) {
                return null;
            }
            return GetAttribute(attributeIndex);
        }
        public abstract void Close();

        #region IDisposable

        protected virtual void Dispose(bool disposing)
        {
            if (ReadState != ReadState.Closed) {
                Close();
            }
        }

        void IDisposable.Dispose()
        {
            Dispose(true);
        }

        #endregion

        private bool _isParseErrorFatal;

        public virtual event EventHandler<ParseErrorEventArgs> ParseError;

        public virtual bool IsParseErrorFatal
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

        protected virtual void OnParseError(string message)
        {
            ParseErrorEventArgs args = new ParseErrorEventArgs(message, this as IXmlLineInfo);
            OnParseError(args);
        }

        protected virtual void OnParseError(ParseErrorEventArgs args)
        {
            if (ParseError != null) {
                ParseError(this, args);
            }

            if (IsParseErrorFatal) {
                throw new XmlException(args.Message, null, args.LineNumber, args.LinePosition);
            }
        }
    }
}
