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
using System.Text;
using System.IO;
using System.Xml;

namespace Twintsam.Html
{
    [TestClass]
    public partial class HtmlReaderTreeConstructionTest
    {
        private int parseErrors;

        private void reader_ParseError(object source, ParseErrorEventArgs args)
        {
            parseErrors++;
        }

        private void DoTest(string input, int parseErrors, string expectedOutput)
        {
            HtmlReader reader = new HtmlReader(new StringReader(input));
            reader.ParseError += new EventHandler<ParseErrorEventArgs>(reader_ParseError);

            StringBuilder actualOutput = new StringBuilder(expectedOutput.Length);
            while (reader.Read()) {
                actualOutput.Append("| ");
                actualOutput.Append(' ', (reader.Depth - 1) * 2);

                switch (reader.NodeType) {
                case XmlNodeType.DocumentType:
                    actualOutput.Append("<!DOCTYPE ").Append(reader.Name).Append('>');
                    break;
                case XmlNodeType.Element:
                    actualOutput.Append("<").Append(reader.Name).Append('>');
                    while (reader.MoveToFirstAttribute()) {
                        actualOutput.AppendLine();
                        actualOutput.Append("| ");
                        actualOutput.Append(' ', (reader.Depth - 1) * 2);
                     
                        actualOutput.Append(reader.Name).Append("=\"").Append(reader.Value.Replace("\"", "&quot;")).Append('"');
                    }
                    break;
                case XmlNodeType.EndElement:
                    break;
                case XmlNodeType.Comment:
                    actualOutput.Append("<!-- ").Append(reader.Value).Append(" -->");
                    break;
                case XmlNodeType.Text:
                    actualOutput.Append('"').Append(reader.Value.Replace("\"", "&quot;")).Append('"');
                    break;
                default:
                    Assert.Fail("Unexpected token type: {0}", reader.NodeType);
                    break;
                }

                actualOutput.AppendLine();
            }

            Assert.AreEqual(expectedOutput, actualOutput.ToString());
            Assert.AreEqual(parseErrors, this.parseErrors);
        }

        [TestInitialize]
        public void Initialize()
        {
            parseErrors = 0;
        }
    }
}
