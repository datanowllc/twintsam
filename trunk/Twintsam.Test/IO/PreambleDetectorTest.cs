#if !NUNIT
using Microsoft.VisualStudio.TestTools.UnitTesting;
#else
using NUnit.Framework;
using TestClass = NUnit.Framework.TestFixtureAttribute;
using TestMethod = NUnit.Framework.TestAttribute;
using TestInitialize = NUnit.Framework.SetUpAttribute;
using TestCleanup = NUnit.Framework.TearDownAttribute;
using ClassInitialize = NUnit.Framework.TestFixtureSetUpAttribute;
using ClassCleanup = NUnit.Framework.TestFixtureTearDownAttribute;
#endif

using System;
using System.IO;
using System.Text;

namespace Twintsam.IO
{
    [TestClass]
    public class PreambleDetectorTest
    {
        [TestMethod]
        public void DetectedEncodingsHavePreamble()
        {
            foreach (Encoding encoding in PreambleDetector.DetectedEncodings) {
                byte[] preamble = encoding.GetPreamble();
                Assert.IsTrue(preamble != null && preamble.Length > 0);
            }
        }

        [TestMethod]
        public void DetectPreambles()
        {
            foreach (Encoding encoding in PreambleDetector.DetectedEncodings) {
                byte[] preamble = encoding.GetPreamble();
                Stream stream = new MemoryStream(preamble);
                Assert.AreSame(encoding, PreambleDetector.Detect(stream));
                Assert.AreEqual(preamble.Length, stream.Position);
            }
        }

        [TestMethod]
        public void DontDetectOtherEncodings()
        {
            foreach (EncodingInfo encodingInfo in Encoding.GetEncodings()) {
                Encoding encoding = encodingInfo.GetEncoding();
                if (Array.IndexOf(PreambleDetector.DetectedEncodings, encoding) < 0) {
                    byte[] preamble = encoding.GetPreamble();
                    if (preamble != null && preamble.Length > 0) {
                        Stream stream = new MemoryStream(preamble);
                        Encoding detected = PreambleDetector.Detect(stream);
                        if (detected == null) {
                            Assert.AreEqual(0, stream.Position);
                        } else {
                            // might be that the preamble is a subset of the detected-encodings preambles
                            Assert.AreNotEqual(encoding, detected);
                            CollectionAssert.Contains(PreambleDetector.DetectedEncodings, detected);
                            CollectionAssert.IsSubsetOf(detected.GetPreamble(), preamble);
                        }
                    }
                }
            }
        }
    }
}
