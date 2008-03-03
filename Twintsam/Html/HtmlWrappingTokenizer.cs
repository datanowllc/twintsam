using System;
using System.Xml;

namespace Twintsam.Html
{
    public class HtmlWrappingTokenizer : HtmlTokenizer
    {
        private HtmlTokenizer _tokenizer;

        protected HtmlWrappingTokenizer(HtmlTokenizer tokenizer)
        {
            if (tokenizer == null) {
                throw new ArgumentNullException("tokenizer");
            }
            _tokenizer = tokenizer;
        }

        protected HtmlTokenizer Tokenizer { get { return _tokenizer; } }

        public override bool IsFragmentTokenizer { get { return _tokenizer.IsFragmentTokenizer; } }
        public override string FragmentContext { get { return _tokenizer.FragmentContext; } }

        public override ReadState ReadState
        {
            get { return _tokenizer.ReadState; }
        }

        public override bool EOF
        {
            get { return _tokenizer.EOF; }
        }

        public override XmlNameTable NameTable
        {
            get { return _tokenizer.NameTable; }
        }

        public override ContentModel ContentModel
        {
            get { return _tokenizer.ContentModel; }
            set { _tokenizer.ContentModel = value; }
        }

        public override XmlNodeType TokenType
        {
            get { return _tokenizer.TokenType; }
        }

        public override string Name
        {
            get { return _tokenizer.Name; }
        }

        public override bool HasTrailingSolidus
        {
            get { return _tokenizer.HasTrailingSolidus; }
        }

        public override bool ForceQuirks
        {
            get { return _tokenizer.ForceQuirks; }
        }

        public override string Value
        {
            get { return _tokenizer.Value; }
        }

        public override bool HasAttributes
        {
            get { return base.HasAttributes; }
        }

        public override string this[int index]
        {
            get { return base[index]; }
        }

        public override string this[string name]
        {
            get { return base[name]; }
        }

        public override int AttributeCount
        {
            get { return _tokenizer.AttributeCount; }
        }

        public override bool Read()
        {
            return _tokenizer.Read();
        }

        public override string GetAttributeName(int index)
        {
            return _tokenizer.GetAttributeName(index);
        }

        public override char GetAttributeQuoteChar(int index)
        {
            return _tokenizer.GetAttributeQuoteChar(index);
        }

        public override string GetAttribute(int index)
        {
            return _tokenizer.GetAttribute(index);
        }

        public override int GetAttributeIndex(string name)
        {
            return base.GetAttributeIndex(name);
        }

        public override string GetAttribute(string name)
        {
            return _tokenizer.GetAttribute(name);
        }

        public override void Close()
        {
            _tokenizer.Close();
        }
    }
}
