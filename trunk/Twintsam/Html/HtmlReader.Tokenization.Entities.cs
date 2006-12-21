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

        private static readonly Dictionary<string, string> EntityNamePrefixes = new Dictionary<string, string>(HtmlEntities.NumberOfEntities);

        [SuppressMessage("Microsoft.Performance", "CA1810:InitializeReferenceTypeStaticFieldsInline")]
        static HtmlReader()
        {
            // FIXME: that's really not optimized
            foreach (string entityName in HtmlEntities.EntityNames) {
                // Start with 'length-2' as we only want prefixes
                for (int length = entityName.Length - 2; length > 0; length--) {
                    string prefix = entityName.Substring(0, length);
                    EntityNamePrefixes[prefix] = prefix;
                }
            }
        }

        private static bool IsEntityNamePrefix(string s)
        {
            Debug.Assert(!String.IsNullOrEmpty(s));
            Debug.Assert(s.Length < HtmlEntities.LonguestEntityNameLength);

            return EntityNamePrefixes.ContainsKey(s);
        }

        // http://www.whatwg.org/specs/web-apps/current-work/#consume
        [SuppressMessage("Microsoft.Maintainability", "CA1502:AvoidExcessiveComplexity")]
        private string EatEntity()
        {
            // NOTE: the ampersand has already been eaten

            Debug.Assert(NextInputChar != EOF_CHAR);

            if (NextInputChar == '#') {
                bool isHex = false;
                int next = PeekChar(2);
                if (next == 'x' || next == 'X') {
                    isHex = true;
                    next = PeekChar(3);
                }
                if (next < '0' || '9' < next) {
                    OnParseError("not a numeric entity");
                    return null;
                }
                // From this point, we know we at least have a "well-formed" numeric entity, so we can safely consume '&#x' characters
                EatChars(isHex ? 3 : 2);

                int offset = 0;
                // Skip leading zeros
                do {
                    next = PeekChar(++offset);
                } while (next == '0');
                // collect digits
                StringBuilder sb = new StringBuilder(MAX_UNICODE_CODEPOINT_DIGITS); // use max-digits, as base-10 numeric strings are always longer than base-16 ones
                while (('0' <= next && next <= '9') || (isHex && ('A' <= next && next <= 'F') || ('a' <= next && next <= 'f'))) {
                    sb.Append((char)next);

                    next = PeekChar(++offset);
                }
                if (next != ';') {
                    OnParseError("the entity does not end with a semi-colon");
                } else {
                    offset++;
                }
                EatChars(offset);
                int codepoint;
                if (sb.Length == 0) {
                    // given that we skipped leading zeros, this means there were only zeros
                    codepoint = REPLACEMENT_CHAR;
                } else if (sb.Length > MAX_UNICODE_CODEPOINT_HEXDIGITS) {
                    OnParseError("too many hex-digits, cannot be a valid Unicode character");
                    codepoint = REPLACEMENT_CHAR;
                } else {
                    codepoint = Int32.Parse(sb.ToString(), NumberStyles.HexNumber, CultureInfo.InvariantCulture);
                    if (codepoint <= 0 || codepoint > MAX_UNICODE_CODEPOINT) {
                        OnParseError("Invalid Unicode character");
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
                int offset = 0;
                StringBuilder sb = new StringBuilder(HtmlEntities.LonguestEntityNameLength);
                int foundChar = -1;
                int foundEntityNameLength = -1;
                do {
                    int next = PeekChar(++offset);
                    if (next < 0 || next == ';') {
                        break;
                    }
                    sb.Append(Char.ConvertFromUtf32(next));
                    if (sb.Length >= HtmlEntities.ShortestEntityNameLength) {
                        int c;
                        if (HtmlEntities.TryGetChar(sb.ToString(), out c)) {
                            foundChar = c;
                            foundEntityNameLength = offset;
                        }
                    }
                } while (sb.Length < HtmlEntities.LonguestEntityNameLength && IsEntityNamePrefix(sb.ToString()));
                if (foundChar < 0) {
                    OnParseError("we didn't find any matching entity");
                    return null;
                }
                if (PeekChar(foundEntityNameLength) != ';') {
                    OnParseError("the entity does not end with a semi-colon");
                } else {
                    offset++;
                }
                EatChars(offset);
                return Char.ConvertFromUtf32(foundChar);
            }
        }
    }
}
