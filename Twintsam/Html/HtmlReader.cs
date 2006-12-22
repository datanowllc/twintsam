using System;
using System.IO;

namespace Twintsam.Html
{
    public partial class HtmlReader
    {
        // TODO: public constructors

        public HtmlReader(TextReader reader) : this()
        {
            if (reader == null) {
                throw new ArgumentNullException("reader");
            }
            _reader = reader;
        }

        private HtmlReader()
        {
            // http://www.whatwg.org/specs/web-apps/current-work/#tokenization:
            // The state machine must start in the data state.
            _currentParsingFunction = ParseData;
        }
    }
}