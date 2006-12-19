using System;
using System.Collections.Generic;
using System.Text;

namespace Twintsam.Html
{
    public partial class HtmlReader
    {
        private struct Attribute : IComparable<Attribute>
        {
            public string name;
            public string value;
            public char quoteChar;

            #region IComparable<Attribute> Members

            int IComparable<Attribute>.CompareTo(Attribute other)
            {
                return String.CompareOrdinal(name, other.name);
            }

            #endregion
        }
    }
}
