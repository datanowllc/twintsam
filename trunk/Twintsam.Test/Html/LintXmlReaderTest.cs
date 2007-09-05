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
using System.Xml;

namespace Twintsam.Html
{
    [TestClass]
    public class LintXmlReaderTest
    {
        [TestMethod]
#if NUNIT
        [Category("LintXmlReader")]
#endif
        public void LintXmlReader1()
        {
            using (LintXmlReader reader = new LintXmlReader(XmlReader.Create(new StringReader("<root>text</root>")))) {
                while (reader.Read()) {
                    // do nothing
                }
            }
        }

        [TestMethod]
#if NUNIT
        [Category("LintXmlReader")]
#endif
        public void LintXmlReader2()
        {
            using (LintXmlReader reader = new LintXmlReader(XmlReader.Create(new StringReader("<root>text1<empty/>text2</root>")))) {
                while (reader.Read()) {
                    // do nothing
                }
            }
        }


        [TestMethod]
#if NUNIT
        [Category("LintXmlReader")]
#endif
        public void LintXmlReader3()
        {
            using (LintXmlReader reader = new LintXmlReader(XmlReader.Create(new StringReader("<root><child>text</child></root>")))) {
                while (reader.Read()) {
                    // do nothing
                }
            }
        }

        [TestMethod]
#if NUNIT
        [Category("LintXmlReader")]
#endif
        public void LintXmlReader4()
        {
            using (LintXmlReader reader = new LintXmlReader(XmlReader.Create(new StringReader("<root><!--comment--></root>")))) {
                while (reader.Read()) {
                    // do nothing
                }
            }
        }

        [TestMethod]
#if NUNIT
        [Category("LintXmlReader")]
#endif
        public void LintXmlReader5()
        {
            using (LintXmlReader reader = new LintXmlReader(XmlReader.Create(new StringReader("<!--comment--><root></root>")))) {
                while (reader.Read()) {
                    // do nothing
                }
            }
        }

        [TestMethod]
#if NUNIT
        [Category("LintXmlReader")]
#endif
        public void LintXmlReader6()
        {
            using (LintXmlReader reader = new LintXmlReader(XmlReader.Create(new StringReader("<root></root><!--comment-->")))) {
                while (reader.Read()) {
                    // do nothing
                }
            }
        }

        [TestMethod]
#if NUNIT
        [Category("LintXmlReader")]
#endif
        public void LintXmlReader7()
        {
            XmlReaderSettings settings = new XmlReaderSettings();
            settings.ProhibitDtd = false;
            using (LintXmlReader reader = new LintXmlReader(XmlReader.Create(new StringReader("<!DOCTYPE root><root></root>"), settings))) {
                while (reader.Read()) {
                    // do nothing
                }
            }
        }
    }
}
