using System;
using System.Collections.Generic;
using System.Text;
using System.Diagnostics.CodeAnalysis;
using System.Diagnostics;
using System.Globalization;

namespace Twintsam.Html
{
    public partial class HtmlReader
    {
        private readonly Encoding Windows1252Encoding = Encoding.GetEncoding(1252);

        private const int MAX_UNICODE_CODEPOINT = 0x10FFFF; // 1114111
        private const int MAX_UNICODE_CODEPOINT_HEXDIGITS = 6;
        private const int MAX_UNICODE_CODEPOINT_DIGITS = 9;

        // http://www.whatwg.org/specs/web-apps/current-work/#consume
        private string EatEntity()
        {
            // NOTE: the ampersand has already been eaten

            if (NextInputChar == '#') {
                bool isHex = false;
                char next = PeekChar(1);
                if (next == 'x' || next == 'X') {
                    isHex = true;
                    next = PeekChar(2);
                }
                Predicate<char> condition = delegate(char c) {
                    return ('0' <= c && c <= '9') || (isHex && ('A' <= c && c <= 'F') || ('a' <= c && c <= 'f'));
                };
                if (!condition(next)) {
                    OnParseError("Unescaped &#" + (isHex ? "x" : ""));
                    return null;
                }
                // From this point, we know we at least have a "well-formed" numeric entity, so we can safely consume '&#x' characters
                EatChars(isHex ? 2 : 1);

                SkipChars(delegate(char c) { return c == '0'; });

                string digits = PeekChars(condition, MAX_UNICODE_CODEPOINT_DIGITS); // base-10 numeric strings are always longer than base-16 ones for the same value

                int length = digits.Length;
                if (PeekChar(length) == ';') {
                    length++;
                } else {
                    OnParseError("Entity does not end with a semi-colon");
                }
                EatChars(length);

                int codepoint;
                if (digits.Length == 0) {
                    // given that we skipped leading zeros, this means there were only zeros
                    codepoint = REPLACEMENT_CHAR;
                } else if (digits.Length > (isHex ? MAX_UNICODE_CODEPOINT_HEXDIGITS : MAX_UNICODE_CODEPOINT_DIGITS)) {
                    OnParseError("Too many digits, it cannot be a valid Unicode character");
                    codepoint = REPLACEMENT_CHAR;
                } else {
                    codepoint = Int32.Parse(digits, isHex ? NumberStyles.AllowHexSpecifier : NumberStyles.None, CultureInfo.InvariantCulture);
                    if (codepoint <= 0 || codepoint > MAX_UNICODE_CODEPOINT) {
                        OnParseError("Invalid Unicode character &#" + (isHex ? "x" : "") + digits + ";");
                        codepoint = REPLACEMENT_CHAR;
                    }
                }

                if (128 <= codepoint && codepoint <= 159) {
                    return Windows1252Encoding.GetString(new byte[] { (byte)codepoint });
                }
                try {
                    return Char.ConvertFromUtf32(codepoint);
                } catch (ArgumentOutOfRangeException) {
                    return null;
                }
            } else {
                string entityName = PeekChars(HtmlEntities.LonguestEntityNameLength);
                int semicolonIndex = entityName.IndexOf(';');
                if (semicolonIndex >= 0) {
                    entityName = entityName.Substring(0, semicolonIndex);
                }

                if (entityName.Length == 0) {
                    if (NextInputChar == EOF_CHAR) {
                        OnParseError("Unexpected end of file in character entity");
                    } else {
                        OnParseError("Empty entity name &;");
                    }
                    return null;
                }

                int foundChar = -1;
                while (foundChar < 0 && entityName.Length >= HtmlEntities.ShortestEntityNameLength) {
                    int c;
                    if (HtmlEntities.TryGetChar(entityName, out c)) {
                        foundChar = c;
                    } else {
                        entityName = entityName.Substring(0, entityName.Length - 1);
                    }
                }

                if (foundChar < 0) {
                    OnParseError("Named entity not found: " + entityName);
                    return null;
                }

                int length = entityName.Length;
                if (PeekChar(length) == ';') {
                    length++;
                } else {
                    OnParseError("Entity does not end with a semi-colon");
                }
                EatChars(length);

                return Char.ConvertFromUtf32(foundChar);
            }
        }
    }
}
