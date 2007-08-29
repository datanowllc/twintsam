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
            if (parseErrors == 0) {
                Trace.WriteLine("");
                Trace.WriteLine("Actual errors:");
            }
            Trace.WriteLine(String.Format("{0},{1}: {2}", args.LineNumber, args.LinePosition, args.Message));
            parseErrors++;
        }

        private void DoTest(string input, string expectedOutput, string[] parseErrors)
        {
            expectedOutput.Trim();

            input = input.Replace("\n", Environment.NewLine);
            expectedOutput = expectedOutput.Replace("\n", Environment.NewLine);

            HtmlReader reader = new HtmlReader(new StringReader(input));
            reader.ParseError += new EventHandler<ParseErrorEventArgs>(reader_ParseError);

            Trace.WriteLine("Input:");
            Trace.WriteLine(input);
            Trace.WriteLine("");
            if (parseErrors == null || parseErrors.Length == 0) {
                Trace.WriteLine("No expected error.");
            } else {
                Trace.WriteLine("Expected errors:");
                foreach (string parseError in parseErrors) {
                    Trace.WriteLine(parseError);
                }
            }
            Trace.WriteLine("");
            Trace.WriteLine("Expected:");
            Trace.WriteLine(expectedOutput);
            StringBuilder actualOutput = new StringBuilder(expectedOutput.Length);
            try {
                while (reader.Read()) {
                    if (reader.NodeType == XmlNodeType.EndElement) {
                        continue;
                    }

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
                        throw new InvalidOperationException("EndElement should have been handled above !?");
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
                Assert.AreEqual(expectedOutput, actualOutput.ToString().Trim());
                Assert.AreEqual(parseErrors, this.parseErrors);
            } catch (NotImplementedException nie) {
                // Amnesty for those that confess
#if !NUNIT
                Trace.Write(nie);
                Assert.Inconclusive("Not Implemented");
#endif
            } finally {
                if (this.parseErrors == 0) {
                    Trace.WriteLine("");
                    Trace.WriteLine("No actual error.");
                }
                Trace.WriteLine("");
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
