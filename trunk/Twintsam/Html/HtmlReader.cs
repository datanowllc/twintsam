using System;

namespace Twintsam.Html
{
    public partial class HtmlReader
    {
        // TODO: public constructors

        private HtmlReader()
        {
            // http://www.whatwg.org/specs/web-apps/current-work/#tokenization:
            // The state machine must start in the data state.
            _currentParsingFunction = ParseData;
        }
    }
}
