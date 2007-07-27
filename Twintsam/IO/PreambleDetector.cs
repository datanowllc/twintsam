using System;
using System.Collections.Generic;
using System.Text;
using System.IO;

namespace Twintsam.IO
{
    public static class PreambleDetector
    {
        private const int DEFAULT_BUFFER_SIZE = 3;

        public static readonly Encoding[] DetectedEncodings = {
            Encoding.BigEndianUnicode,
            Encoding.Unicode,
            Encoding.UTF8,
        };

        public static Encoding Detect(Stream stream)
        {
            return Detect(stream, DetectedEncodings, DEFAULT_BUFFER_SIZE);
        }

        public static Encoding Detect(Stream stream, IEnumerable<Encoding> encodings, int bufferSize)
        {
            if (stream == null) {
                throw new ArgumentNullException("stream");
            }
            if (encodings == null) {
                throw new ArgumentNullException("encodings");
            }
            if (!stream.CanSeek && !stream.CanRead) {
                throw new ArgumentException();
            }

            foreach (Encoding encoding in encodings) {
                stream.Seek(0, SeekOrigin.Begin);
                bool found = true;
                foreach (byte b in encoding.GetPreamble()) {
                    if (b != stream.ReadByte()) {
                        found = false;
                        break;
                    }
                }
                if (found) {
                    return encoding;
                }
            }
            stream.Seek(0, SeekOrigin.Begin);
            return null;
        }
    }
}
