using System;
using System.Diagnostics;
using System.IO;
using System.Text;

namespace Twintsam.IO
{
    public static class MetaDetector
    {
        // http://www.whatwg.org/specs/web-apps/current-work/multipage/section-parsing.html#determining0

        public static Encoding Detect(Stream stream)
        {
            if (stream == null) {
                throw new ArgumentNullException("stream");
            }
            if (!stream.CanSeek && !stream.CanRead) {
                throw new ArgumentException();
            }

            int next;
            while (true) {
                next = stream.ReadByte();
                if (next < 0) {
                    throw new NotImplementedException();
                }
                switch (next) {
                case '<':
                    next = stream.ReadByte();
                    if (next < 0) {
                        throw new NotImplementedException();
                    }
                    switch (next) {
                    case '!':
                        if (SkipIfCurrentIs(stream, 0x2D /* - */) && SkipIfCurrentIs(stream, 0x2D /* - */)) {
                            SkipComment(stream);
                        } else {
                            SkipUntilGreaterThanSign(stream);
                        }
                        break;
                    case '?':
                        SkipUntilGreaterThanSign(stream);
                        break;
                    case '/':
                        if (SkipIfCurrentIsAnASCIILetter(stream)) {
                            throw new NotImplementedException();
                        } else {
                            SkipUntilGreaterThanSign(stream);
                        }
                        break;
                    case 'm':
                    case 'M':
                        if (SkipIfCurrentIsAny(stream, 0x45, 0x65 /* eE */) && SkipIfCurrentIsAny(stream, 0x54, 0x74 /* tT */)
                            && SkipIfCurrentIsAny(stream, 0x41, 0x61 /* aA */) && SkipIfCurrentIsASpace(stream)) {
                            string attributeName;
                            string attributeValue;
                            while (GetAnAttribute(stream, out attributeName, out attributeValue)) {
                                switch (attributeName)
	                            {
                                case "charset":
                                    try {
                                        return Encoding.GetEncoding(attributeValue);
                                    } catch(ArgumentException) { }
                                    break;
                                case "content":
                                    string encoding = ExtractEncodingFromContentType(attributeValue);
                                    try {
                                        return Encoding.GetEncoding(encoding);
                                    } catch (ArgumentException) { }
                                    break;
	                            default:
                                    break;
	                            }
                            }
                            break;
                        } else {
                            goto default;
                        }
                    default:
                        if (SkipIfCurrentIsAnASCIILetter(stream)) {
                            throw new NotImplementedException();
                        }
                        break;
                    }
                    break;
                }
            }
        }

        private static bool SkipIfCurrentIs(Stream stream, byte b)
        {
            int next = stream.ReadByte();
            if (next == b) {
                return true;
            } else {
                stream.Seek(-1, SeekOrigin.Current);
                return false;
            }
        }

        private static bool SkipIfCurrentIsAny(Stream stream, params byte[] bytes)
        {
            Debug.Assert(bytes.Length > 0);

            int next = stream.ReadByte();
            foreach (byte b in bytes) {
                if (next == b) {
                    return true;
                }
            }
            stream.Seek(-1, SeekOrigin.Current);
            return false;
        }

        private static bool SkipIfCurrentIsASpace(Stream stream)
        {
            return SkipIfCurrentIsAny(stream, 0x09, 0x0A, 0x0B, 0x0C, 0x0D, 0x20);
        }

        private static bool SkipIfCurrentIsAnASCIILetter(Stream stream)
        {
            int next = stream.ReadByte();
            if ((0x41 <= next && next <= 0x5A) || (0x61 <= next && next <= 0x7A)) {
                return true;
            } else {
                stream.Seek(-1, SeekOrigin.Current);
                return false;
            }
        }

        private static void SkipComment(Stream stream)
        {
            // Advance until the next "-->" (the two hyphens might be the one we already seen)
            int next;
            int hyphenCount = 2;
            while (true) {
                next = stream.ReadByte();
                if (next < 0) {
                    throw new NotImplementedException();
                }
                switch (next) {
                case '-':
                    hyphenCount++;
                    break;
                case '>':
                    if (hyphenCount >= 2) {
                        return;
                    } else {
                        goto default;
                    }
                default:
                    hyphenCount = 0;
                    break;
                }
            }
        }

        private static void SkipUntilGreaterThanSign(Stream stream)
        {
            int next;
            while (true) {
                next = stream.ReadByte();
                if (next < 0) {
                    throw new NotImplementedException();
                }
                if (next == '>') {
                    return;
                }
            }
        }

        private static bool GetAnAttribute(Stream stream, out string attributeName, out string attributeValue)
        {
            // http://www.whatwg.org/specs/web-apps/current-work/multipage/section-parsing.html#get-an
            attributeName = null;
            attributeValue = null;

            int next;
            while (true) {
                next = stream.ReadByte();
                if (next < 0) {
                    throw new NotImplementedException();
                }
                switch (next) {
                case '\t':
                case '\n':
                case '\v':
                case '\f':
                case '\r':
                case ' ':
                    break; // skip
                case '<':
                    stream.Seek(-1, SeekOrigin.Current);
                    return false;
                case '>':
                    return false;
                default:
                    StringBuilder name = new StringBuilder();
                    StringBuilder value = new StringBuilder();

                    name.Append((char)next);

                    throw new NotImplementedException();
                    break;
                }
            }
        }

        private static string ExtractEncodingFromContentType(string contentType)
        {
            // http://www.whatwg.org/specs/web-apps/current-work/multipage/section-content-type-sniffing.html#algorithm3
            throw new NotImplementedException();
        }
    }
}
