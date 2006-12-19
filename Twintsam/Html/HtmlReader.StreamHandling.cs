using System;
using System.Collections.Generic;
using System.Text;
using System.Xml;

namespace Twintsam.Html
{
    public partial class HtmlReader : IXmlLineInfo
    {
        #region IXmlLineInfo Members

        public bool HasLineInfo()
        {
            throw new NotImplementedException();
        }

        public int LineNumber
        {
            get { throw new NotImplementedException(); }
        }

        public int LinePosition
        {
            get { throw new NotImplementedException(); }
        }

        #endregion
    }
}
