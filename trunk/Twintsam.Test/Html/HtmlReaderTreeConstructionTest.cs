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
using System.Diagnostics;

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

            Trace.WriteLine("Input:");
            Trace.WriteLine(input);
            Trace.WriteLine("");
            Trace.WriteLine("Expected:");
            Trace.WriteLine(expectedOutput);
            Trace.WriteLine("");
            StringBuilder actualOutput = new StringBuilder(expectedOutput.Length);
            try {
                while (reader.Read()) {
                    actualOutput.Append("| ");
                    if (reader.Depth > 0) {
                        actualOutput.Append(' ', reader.Depth * 2);
                    }

                    switch (reader.NodeType) {
                    case XmlNodeType.DocumentType:
                        actualOutput.Append("<!DOCTYPE");
                        //if (reader.Name.Length > 0) {
                        actualOutput.Append(' ').Append(reader.Name);
                        //}
                        //string publicId = reader.GetAttribute("PUBLIC");
                        //string systemId = reader.GetAttribute("SYSTEM");
                        //if (publicId != null) {
                        //    actualOutput.Append("PUBLIC ")
                        //    .Append('"').Append(publicId).Append('"');
                        //} else if (systemId != null) {
                        //    actualOutput.Append("SYSTEM");
                        //}
                        //if (systemId != null) {
                        //    actualOutput.Append(' ').Append('"').Append(systemId).Append('"');
                        //}
                        actualOutput.Append('>');
                        break;
                    case XmlNodeType.Element:
                        actualOutput.Append("<").Append(reader.Name).Append('>');
                        while (reader.MoveToNextAttribute()) {
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
                Assert.AreEqual(
                    expectedOutput.Replace("\r\n", "\n"),
                    actualOutput.Replace("\r\n", "\n").ToString());
                Assert.AreEqual(parseErrors, this.parseErrors);
            } catch (NotImplementedException nie) {
                // Amnesty for those that confess
#if !NUNIT
                Trace.Write(nie);
                Assert.Inconclusive("Not Implemented");
#endif
            } finally {
                Trace.WriteLine("Actual:");
                Trace.WriteLine(actualOutput);
                Trace.WriteLine("");
            }
        }

        [TestInitialize]
        public void Initialize()
        {
            parseErrors = 0;
        }
    }
}
