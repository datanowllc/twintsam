using System;
using System.Collections.Generic;
using System.Text;
using System.Diagnostics.CodeAnalysis;
using System.Xml;

namespace Twintsam.Html
{
    public static class HtmlEntities
    {
        public const int ShortestEntityNameLength = 2;
        public const int LonguestEntityNameLength = 8;
        public const int NumberOfEntities = 259;
        public const int NumberOfAliases = 6;

        private static readonly Dictionary<string, int> EntityNamesToChars = new Dictionary<string, int>(NumberOfEntities);
        private static readonly Dictionary<int, string> CharsToEntityNames = new Dictionary<int, string>(NumberOfEntities - NumberOfAliases);

        [SuppressMessage("Microsoft.Performance", "CA1810:InitializeReferenceTypeStaticFieldsInline")]
        static HtmlEntities()
        {
            // Two-chars long entity names
            CharsToEntityNames.Add('\u003C', "lt");
            CharsToEntityNames.Add('\u003E', "gt");
            CharsToEntityNames.Add('\u039C', "Mu");
            CharsToEntityNames.Add('\u039D', "Nu");
            CharsToEntityNames.Add('\u039E', "Xi");
            CharsToEntityNames.Add('\u03A0', "Pi");
            CharsToEntityNames.Add('\u03BC', "mu");
            CharsToEntityNames.Add('\u03BD', "nu");
            CharsToEntityNames.Add('\u03BE', "xi");
            CharsToEntityNames.Add('\u03C0', "pi");
            CharsToEntityNames.Add('\u220B', "ni");
            CharsToEntityNames.Add('\u2228', "or");
            CharsToEntityNames.Add('\u2260', "ne");
            CharsToEntityNames.Add('\u2264', "le");
            CharsToEntityNames.Add('\u2265', "ge");

            // Three-chars long entity names
            CharsToEntityNames.Add('\u0026', "amp");
            CharsToEntityNames.Add('\u00A5', "yen");
            CharsToEntityNames.Add('\u00A8', "uml");
            CharsToEntityNames.Add('\u00AC', "not");
            CharsToEntityNames.Add('\u00AD', "shy");
            CharsToEntityNames.Add('\u00AE', "reg");
            CharsToEntityNames.Add('\u00B0', "deg");
            CharsToEntityNames.Add('\u00D0', "ETH");
            CharsToEntityNames.Add('\u00F0', "eth");
            CharsToEntityNames.Add('\u0397', "Eta");
            CharsToEntityNames.Add('\u03A1', "Rho");
            CharsToEntityNames.Add('\u03A4', "Tau");
            CharsToEntityNames.Add('\u03A6', "Phi");
            CharsToEntityNames.Add('\u03A7', "Chi");
            CharsToEntityNames.Add('\u03A8', "Psi");
            CharsToEntityNames.Add('\u03B7', "eta");
            CharsToEntityNames.Add('\u03C1', "rho");
            CharsToEntityNames.Add('\u03C4', "tau");
            CharsToEntityNames.Add('\u03C6', "phi");
            CharsToEntityNames.Add('\u03C7', "chi");
            CharsToEntityNames.Add('\u03C8', "psi");
            CharsToEntityNames.Add('\u03D6', "piv");
            CharsToEntityNames.Add('\u200D', "zwj");
            CharsToEntityNames.Add('\u200E', "lrm");
            CharsToEntityNames.Add('\u200F', "rlm");
            CharsToEntityNames.Add('\u2211', "sum");
            CharsToEntityNames.Add('\u2220', "ang");
            CharsToEntityNames.Add('\u2227', "and");
            CharsToEntityNames.Add('\u2229', "cap");
            CharsToEntityNames.Add('\u222A', "cup");
            CharsToEntityNames.Add('\u222B', "int");
            CharsToEntityNames.Add('\u223C', "sim");
            CharsToEntityNames.Add('\u2282', "sub");
            CharsToEntityNames.Add('\u2283', "sup");
            CharsToEntityNames.Add('\u25CA', "loz");

            // Four-chars long entity names
            CharsToEntityNames.Add('\u0022', "quot");
            CharsToEntityNames.Add('\u0027', "apos");
            CharsToEntityNames.Add('\u00A0', "nbsp");
            CharsToEntityNames.Add('\u00A2', "cent");
            CharsToEntityNames.Add('\u00A7', "sect");
            CharsToEntityNames.Add('\u00A9', "copy");
            CharsToEntityNames.Add('\u00AA', "ordf");
            CharsToEntityNames.Add('\u00AF', "macr");
            CharsToEntityNames.Add('\u00B2', "sup2");
            CharsToEntityNames.Add('\u00B3', "sup3");
            CharsToEntityNames.Add('\u00B6', "para");
            CharsToEntityNames.Add('\u00B9', "sup1");
            CharsToEntityNames.Add('\u00BA', "ordm");
            CharsToEntityNames.Add('\u00C4', "Auml");
            CharsToEntityNames.Add('\u00CB', "Euml");
            CharsToEntityNames.Add('\u00CF', "Iuml");
            CharsToEntityNames.Add('\u00D6', "Ouml");
            CharsToEntityNames.Add('\u00DC', "Uuml");
            CharsToEntityNames.Add('\u00E4', "auml");
            CharsToEntityNames.Add('\u00EB', "euml");
            CharsToEntityNames.Add('\u00EF', "iuml");
            CharsToEntityNames.Add('\u00F6', "ouml");
            CharsToEntityNames.Add('\u00FC', "uuml");
            CharsToEntityNames.Add('\u00FF', "yuml");
            CharsToEntityNames.Add('\u0178', "Yuml");
            CharsToEntityNames.Add('\u0192', "fnof");
            CharsToEntityNames.Add('\u02C6', "circ");
            CharsToEntityNames.Add('\u0392', "Beta");
            CharsToEntityNames.Add('\u0396', "Zeta");
            CharsToEntityNames.Add('\u0399', "Iota");
            CharsToEntityNames.Add('\u03B2', "beta");
            CharsToEntityNames.Add('\u03B6', "zeta");
            CharsToEntityNames.Add('\u03B9', "iota");
            CharsToEntityNames.Add('\u2002', "ensp");
            CharsToEntityNames.Add('\u2003', "emsp");
            CharsToEntityNames.Add('\u200C', "zwnj");
            CharsToEntityNames.Add('\u2022', "bull");
            CharsToEntityNames.Add('\u20AC', "euro");
            CharsToEntityNames.Add('\u211C', "real");
            CharsToEntityNames.Add('\u2190', "larr");
            CharsToEntityNames.Add('\u2191', "uarr");
            CharsToEntityNames.Add('\u2192', "rarr");
            CharsToEntityNames.Add('\u2193', "darr");
            CharsToEntityNames.Add('\u2194', "harr");
            CharsToEntityNames.Add('\u21D0', "lArr");
            CharsToEntityNames.Add('\u21D1', "uArr");
            CharsToEntityNames.Add('\u21D2', "rArr");
            CharsToEntityNames.Add('\u21D3', "dArr");
            CharsToEntityNames.Add('\u21D4', "hArr");
            CharsToEntityNames.Add('\u2202', "part");
            CharsToEntityNames.Add('\u2208', "isin");
            CharsToEntityNames.Add('\u220F', "prod");
            CharsToEntityNames.Add('\u221D', "prop");
            CharsToEntityNames.Add('\u2245', "cong");
            CharsToEntityNames.Add('\u2284', "nsub");
            CharsToEntityNames.Add('\u2286', "sube");
            CharsToEntityNames.Add('\u2287', "supe");
            CharsToEntityNames.Add('\u22A5', "perp");
            CharsToEntityNames.Add('\u22C5', "sdot");
            CharsToEntityNames.Add('\u3008', "lang");
            CharsToEntityNames.Add('\u3009', "rang");

            // Five-chars long entity names
            CharsToEntityNames.Add('\u00A1', "iexcl");
            CharsToEntityNames.Add('\u00A3', "pound");
            CharsToEntityNames.Add('\u00AB', "laquo");
            CharsToEntityNames.Add('\u00B4', "acute");
            CharsToEntityNames.Add('\u00B5', "micro");
            CharsToEntityNames.Add('\u00B8', "cedil");
            CharsToEntityNames.Add('\u00BB', "raquo");
            CharsToEntityNames.Add('\u00C2', "Acirc");
            CharsToEntityNames.Add('\u00C5', "Aring");
            CharsToEntityNames.Add('\u00C6', "AElig");
            CharsToEntityNames.Add('\u00CA', "Ecirc");
            CharsToEntityNames.Add('\u00CE', "Icirc");
            CharsToEntityNames.Add('\u00D4', "Ocirc");
            CharsToEntityNames.Add('\u00D7', "times");
            CharsToEntityNames.Add('\u00DB', "Ucirc");
            CharsToEntityNames.Add('\u00DE', "THORN");
            CharsToEntityNames.Add('\u00DF', "szlig");
            CharsToEntityNames.Add('\u00E2', "acirc");
            CharsToEntityNames.Add('\u00E5', "aring");
            CharsToEntityNames.Add('\u00E6', "aelig");
            CharsToEntityNames.Add('\u00EA', "ecirc");
            CharsToEntityNames.Add('\u00EE', "icirc");
            CharsToEntityNames.Add('\u00F4', "ocirc");
            CharsToEntityNames.Add('\u00FB', "ucirc");
            CharsToEntityNames.Add('\u00FE', "thorn");
            CharsToEntityNames.Add('\u0152', "OElig");
            CharsToEntityNames.Add('\u0153', "oelig");
            CharsToEntityNames.Add('\u02DC', "tilde");
            CharsToEntityNames.Add('\u0391', "Alpha");
            CharsToEntityNames.Add('\u0393', "Gamma");
            CharsToEntityNames.Add('\u0394', "Delta");
            CharsToEntityNames.Add('\u0398', "Theta");
            CharsToEntityNames.Add('\u039A', "Kappa");
            CharsToEntityNames.Add('\u03A3', "Sigma");
            CharsToEntityNames.Add('\u03A9', "Omega");
            CharsToEntityNames.Add('\u03B1', "alpha");
            CharsToEntityNames.Add('\u03B3', "gamma");
            CharsToEntityNames.Add('\u03B4', "delta");
            CharsToEntityNames.Add('\u03B8', "theta");
            CharsToEntityNames.Add('\u03BA', "kappa");
            CharsToEntityNames.Add('\u03C3', "sigma");
            CharsToEntityNames.Add('\u03C9', "omega");
            CharsToEntityNames.Add('\u03D2', "upsih");
            CharsToEntityNames.Add('\u2013', "ndash");
            CharsToEntityNames.Add('\u2014', "mdash");
            CharsToEntityNames.Add('\u2018', "lsquo");
            CharsToEntityNames.Add('\u2019', "rsquo");
            CharsToEntityNames.Add('\u201A', "sbquo");
            CharsToEntityNames.Add('\u201C', "ldquo");
            CharsToEntityNames.Add('\u201D', "rdquo");
            CharsToEntityNames.Add('\u201E', "bdquo");
            CharsToEntityNames.Add('\u2032', "prime");
            CharsToEntityNames.Add('\u2033', "Prime");
            CharsToEntityNames.Add('\u203E', "oline");
            CharsToEntityNames.Add('\u2044', "frasl");
            CharsToEntityNames.Add('\u2111', "image");
            CharsToEntityNames.Add('\u2122', "trade");
            CharsToEntityNames.Add('\u21B5', "crarr");
            CharsToEntityNames.Add('\u2203', "exist");
            CharsToEntityNames.Add('\u2205', "empty");
            CharsToEntityNames.Add('\u2207', "nabla");
            CharsToEntityNames.Add('\u2209', "notin");
            CharsToEntityNames.Add('\u2212', "minus");
            CharsToEntityNames.Add('\u221A', "radic");
            CharsToEntityNames.Add('\u221E', "infin");
            CharsToEntityNames.Add('\u2248', "asymp");
            CharsToEntityNames.Add('\u2261', "equiv");
            CharsToEntityNames.Add('\u2295', "oplus");
            CharsToEntityNames.Add('\u2308', "lceil");
            CharsToEntityNames.Add('\u2309', "rceil");
            CharsToEntityNames.Add('\u2663', "clubs");
            CharsToEntityNames.Add('\u2666', "diams");

            // Six-chars long entity names
            CharsToEntityNames.Add('\u00A4', "curren");
            CharsToEntityNames.Add('\u00A6', "brvbar");
            CharsToEntityNames.Add('\u00B1', "plusmn");
            CharsToEntityNames.Add('\u00B7', "middot");
            CharsToEntityNames.Add('\u00BC', "frac14");
            CharsToEntityNames.Add('\u00BD', "frac12");
            CharsToEntityNames.Add('\u00BE', "frac34");
            CharsToEntityNames.Add('\u00BF', "iquest");
            CharsToEntityNames.Add('\u00C0', "Agrave");
            CharsToEntityNames.Add('\u00C1', "Aacute");
            CharsToEntityNames.Add('\u00C3', "Atilde");
            CharsToEntityNames.Add('\u00C7', "Ccedil");
            CharsToEntityNames.Add('\u00C8', "Egrave");
            CharsToEntityNames.Add('\u00C9', "Eacute");
            CharsToEntityNames.Add('\u00CC', "Igrave");
            CharsToEntityNames.Add('\u00CD', "Iacute");
            CharsToEntityNames.Add('\u00D1', "Ntilde");
            CharsToEntityNames.Add('\u00D2', "Ograve");
            CharsToEntityNames.Add('\u00D3', "Oacute");
            CharsToEntityNames.Add('\u00D5', "Otilde");
            CharsToEntityNames.Add('\u00D8', "Oslash");
            CharsToEntityNames.Add('\u00D9', "Ugrave");
            CharsToEntityNames.Add('\u00DA', "Uacute");
            CharsToEntityNames.Add('\u00DD', "Yacute");
            CharsToEntityNames.Add('\u00E0', "agrave");
            CharsToEntityNames.Add('\u00E1', "aacute");
            CharsToEntityNames.Add('\u00E3', "atilde");
            CharsToEntityNames.Add('\u00E7', "ccedil");
            CharsToEntityNames.Add('\u00E8', "egrave");
            CharsToEntityNames.Add('\u00E9', "eacute");
            CharsToEntityNames.Add('\u00EC', "igrave");
            CharsToEntityNames.Add('\u00ED', "iacute");
            CharsToEntityNames.Add('\u00F1', "ntilde");
            CharsToEntityNames.Add('\u00F2', "ograve");
            CharsToEntityNames.Add('\u00F3', "oacute");
            CharsToEntityNames.Add('\u00F5', "otilde");
            CharsToEntityNames.Add('\u00F7', "divide");
            CharsToEntityNames.Add('\u00F8', "oslash");
            CharsToEntityNames.Add('\u00F9', "ugrave");
            CharsToEntityNames.Add('\u00FA', "uacute");
            CharsToEntityNames.Add('\u00FD', "yacute");
            CharsToEntityNames.Add('\u0160', "Scaron");
            CharsToEntityNames.Add('\u0161', "scaron");
            CharsToEntityNames.Add('\u039B', "Lambda");
            CharsToEntityNames.Add('\u03BB', "lambda");
            CharsToEntityNames.Add('\u03C2', "sigmaf");
            CharsToEntityNames.Add('\u2009', "thinsp");
            CharsToEntityNames.Add('\u2020', "dagger");
            CharsToEntityNames.Add('\u2021', "Dagger");
            CharsToEntityNames.Add('\u2026', "hellip");
            CharsToEntityNames.Add('\u2030', "permil");
            CharsToEntityNames.Add('\u2039', "lsaquo");
            CharsToEntityNames.Add('\u203A', "rsaquo");
            CharsToEntityNames.Add('\u2118', "weierp");
            CharsToEntityNames.Add('\u2200', "forall");
            CharsToEntityNames.Add('\u2217', "lowast");
            CharsToEntityNames.Add('\u2234', "there4");
            CharsToEntityNames.Add('\u2297', "otimes");
            CharsToEntityNames.Add('\u230A', "lfloor");
            CharsToEntityNames.Add('\u230B', "rfloor");
            CharsToEntityNames.Add('\u2660', "spades");
            CharsToEntityNames.Add('\u2665', "hearts");

            // Seven-chars long entity names
            CharsToEntityNames.Add('\u0395', "Epsilon");
            CharsToEntityNames.Add('\u039F', "Omicron");
            CharsToEntityNames.Add('\u03A5', "Upsilon");
            CharsToEntityNames.Add('\u03B5', "epsilon");
            CharsToEntityNames.Add('\u03BF', "omicron");
            CharsToEntityNames.Add('\u03C5', "upsilon");
            CharsToEntityNames.Add('\u2135', "alefsym");

            // Height-chars long entity names
            CharsToEntityNames.Add('\u03D1', "thetasym");

            // Copy CharsToEntityNames to EntityNamesToChars, inverting key and value
            foreach (KeyValuePair<int, string> item in CharsToEntityNames) {
                EntityNamesToChars.Add(item.Value, item.Key);
            }
            // Add aliases for some entity names
            EntityNamesToChars.Add("LT", '\u003C');
            EntityNamesToChars.Add("GT", '\u003E');
            EntityNamesToChars.Add("AMP", '\u0026');
            EntityNamesToChars.Add("REG", '\u00AE');
            EntityNamesToChars.Add("QUOT", '\u0022');
            EntityNamesToChars.Add("COPY", '\u00A9');
            EntityNamesToChars.Add("TRADE", '\u2122');
        }

        public static bool ContainsEntityName(string entityName)
        {
            return EntityNamesToChars.ContainsKey(entityName);
        }

        public static bool ContainsChar(int c)
        {
            return CharsToEntityNames.ContainsKey(c);
        }

        public static ICollection<string> EntityNames
        {
            get { return EntityNamesToChars.Keys; }
        }

        public static ICollection<int> Chars
        {
            get { return CharsToEntityNames.Keys; }
        }

        public static bool TryGetChar(string entityName, out int c)
        {
            return EntityNamesToChars.TryGetValue(entityName, out c);
        }

        public static bool TryGetEntityName(int c, out string entityName)
        {
            return CharsToEntityNames.TryGetValue(c, out entityName);
        }

        public static int GetChar(string entityName)
        {
            return EntityNamesToChars[entityName];
        }

        public static string GetEntityName(int c)
        {
            return CharsToEntityNames[c];
        }
    }
}
