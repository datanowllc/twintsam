using System;
using System.Collections.Generic;
using System.Text;

namespace Twintsam
{
    internal static class Constants
    {
        public const string XhtmlNamespaceUri = "http://www.w3.org/199/xhtml";

        // http://www.whatwg.org/specs/web-apps/current-work/#space
        public const string SpaceCharacters = " \t\n\v\f\r";

        public static bool IsSpaceCharacter(char c)
        {
            return SpaceCharacters.IndexOf(c) >= 0;
        }

        public static readonly string[] VoidElements = {
            "base", "link", "meta", "hr", "br", "img", "embed", "param", "area", "col", "input",
        };

        public static readonly string[] CdataElements = { "style", "script", };

        public static readonly string[] RcdataElements = { "title", "textarea", };

        public static readonly string[] SpecialElements = {
            "address", "area", "base", "basefont", "bgsound", "blockquote", "body", "br",
            "center", "col", "colgroup", "dd", "dir", "div", "dl", "dt", "embed",
            "fieldset", "form", "frame", "frameset", "h1", "h2", "h3", "h4", "h5", "h6",
            "head", "hr", "iframe", "image", "img", "input", "isindex", "li", "link",
            "listing", "menu", "meta", "noembed", "noframes", "noscript", "ol", "optgroup",
            "option", "p", "param", "plaintext", "pre", "script", "select", "spacer",
            "style", "tbody", "textarea", "tfoot", "thead", "title", "tr", "ul", "wbr",
        };

        public static readonly string[] ScopingElements = {
            "button", "caption", "html", "marquee", "object", "table", "td", "th",
        };

        public static readonly string[] FormattingElements = {
            "a", "b", "big", "em", "font", "i", "nobr", "s", "small", "strike", "strong", "tt", "u",
        };

        public static bool IsVoidElement(string element)
        {
            return Is(VoidElements, element);
        }

        private static bool Is(string[] elements, string element)
        {
            return Array.Exists<string>(elements, delegate(string item)
            {
                return String.Equals(item, element, StringComparison.OrdinalIgnoreCase);
            });
        }
    }
}
