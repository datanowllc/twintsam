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
using System.Reflection;
using System.Collections;
using System.IO;
using System.Xml;
using System.Diagnostics;
using System.Collections.Generic;

namespace Twintsam.Html
{
    [TestClass]
    public partial class HtmlTextTokenizerTest
    {
        private static FieldInfo HtmlTextTokenizer__lastEmittedStartTagName =
            typeof(HtmlTextTokenizer).GetField("_lastEmittedStartTagName", BindingFlags.Instance | BindingFlags.NonPublic);
        private ArrayList actualOutput = new ArrayList();

        private void reader_ParseError(object source, ParseErrorEventArgs args)
        {
            actualOutput.Add(args);
        }

        private void DoTest(string input, IList expectedOutput, ContentModel contentModel, string lastStartTag)
        {
            HtmlTokenizer tokenizer = new HtmlTextTokenizer(new StringReader(input));
            tokenizer.ParseError += new EventHandler<ParseErrorEventArgs>(reader_ParseError);

            tokenizer.ContentModel = contentModel;
            if (!String.IsNullOrEmpty(lastStartTag)) {
                HtmlTextTokenizer__lastEmittedStartTagName.SetValue(tokenizer, lastStartTag);
            }

            Trace.Write("Input: ");
            Trace.WriteLine(input);
            try {
                while (tokenizer.Read()) {
                    switch (tokenizer.TokenType) {
                    case XmlNodeType.DocumentType:
                        actualOutput.Add(new object[] { "DOCTYPE", tokenizer.Name, tokenizer.GetAttribute("PUBLIC"), tokenizer.GetAttribute("SYSTEM"), !tokenizer.IsIncorrectDoctype });
                        break;
                    case XmlNodeType.Element:
                        Dictionary<string, string> attrs = new Dictionary<string, string>(
                            tokenizer.HasAttributes ? tokenizer.AttributeCount : 0,
                            StringComparer.InvariantCulture);
                        if (tokenizer.HasAttributes) {
                            for (int i = 0; i < tokenizer.AttributeCount; i++) {
                                attrs.Add(
                                    tokenizer.GetAttributeName(i),
                                    tokenizer.GetAttribute(i));
                            }
                        }
                        actualOutput.Add(new object[] { "StartTag", tokenizer.Name, attrs });
                        break;
                    case XmlNodeType.EndElement:
                        actualOutput.Add(new string[] { "EndTag", tokenizer.Name });
                        break;
                    case XmlNodeType.Comment:
                        actualOutput.Add(new string[] { "Comment", tokenizer.Value });
                        break;
                    case XmlNodeType.Text:
                    case XmlNodeType.Whitespace:
                        actualOutput.Add(new string[] { "Character", tokenizer.Value });
                        break;
                    default:
                        Assert.Fail("Unexpected token type: {0}", tokenizer.TokenType);
                        break;
                    }
                }

                Trace.Write("Expected output: ");
                TraceOutput(expectedOutput);
                Trace.WriteLine("");
                Trace.Write("Actual output: ");
                TraceOutput(actualOutput);
                Trace.WriteLine("");

                Assert.AreEqual(expectedOutput.Count, actualOutput.Count, "Not the same number of tokens");

                for (int i = 0; i < expectedOutput.Count; i++) {
                    object expected = expectedOutput[i];
                    object actual = actualOutput[i];


                    if (expected.GetType() == typeof(string)) {
                        // ParseError
#if !NUNIT
                        Assert.IsInstanceOfType(actual, typeof(ParseErrorEventArgs));
#else
                    Assert.IsInstanceOfType(typeof(ParseErrorEventArgs), actual);
#endif
                    } else {
                        Assert.AreEqual(expected.GetType(), actual.GetType());

                        object[] expectedToken = (object[])expected;
                        object[] actualToken = (object[])actual;

                        Assert.AreEqual(expectedToken.Length, actualToken.Length);

                        for (int j = 0; j < expectedToken.Length; j++) {
                            if (expectedToken[j] is ICollection<KeyValuePair<string, string>>) {
#if !NUNIT
                                Assert.IsInstanceOfType(actualToken[j], typeof(IDictionary<string, string>));
#else
                            Assert.IsInstanceOfType(typeof(IDictionary<string, string>), actualToken[j]);
#endif

                                ICollection<KeyValuePair<string, string>> expectedDict = (ICollection<KeyValuePair<string, string>>)expectedToken[j];
                                IDictionary<string, string> actualDict = (IDictionary<string, string>)actualToken[j];

                                Assert.AreEqual(expectedDict.Count, actualDict.Count);

                                foreach (KeyValuePair<string, string> attr in expectedDict) {
                                    Assert.AreEqual(attr.Value, actualDict[attr.Key]);
                                }
                            } else {
                                Assert.AreEqual(expectedToken[j], actualToken[j]);
                            }
                        }
                    }
                }
            } catch (NotImplementedException nie) {
                // Amnesty for those that confess
                Trace.WriteLine("");
                Trace.WriteLine(nie);
#if NUNIT
                Assert.Ignore("Not Implemented");
#else
                Assert.Inconclusive("Not Implemented");
#endif
            }
        }

        [Conditional("TRACE")]
        private void TraceString(string str)
        {
            Trace.Write('"');
            foreach (char c in str) {
                if (c == '\\') {
                    Trace.Write("\\\\");
                } else if (c == '"') {
                    Trace.Write("\\\"");
                } else if (32 <= c && c <= 127) {
                    Trace.Write(c);
                } else {
                    Trace.Write(String.Concat("\\u", ((int)c).ToString("X").PadLeft(4, '0')));
                }
            }
            Trace.Write('"');
        }

        [Conditional("TRACE")]
        private void TraceOutput(ICollection collection)
        {
            Trace.Write("{ ");
            foreach (object obj in collection) {
                if (obj == null) {
                    Trace.Write("null");
                } else if (obj is ICollection) {
                    TraceOutput((ICollection)obj);
                } else if (obj is KeyValuePair<string,string>) {
                    KeyValuePair<string, string> pair = (KeyValuePair<string, string>)obj;
                    TraceString(pair.Key);
                    Trace.Write("=");
                    TraceString(pair.Value);
                }
                else if (obj is ParseErrorEventArgs)
                {
                    ParseErrorEventArgs args = (ParseErrorEventArgs)obj;
                    Trace.Write("[\"ParseError\", ");
                    TraceString(args.Message);
                    Trace.Write("]");
                } else if (obj.GetType() == typeof(string))
                {
                    TraceString((string)obj);
                } else if (obj.GetType() == typeof(bool)) {
                    Trace.Write(obj);
                } else {
                    throw new ArgumentException(String.Concat("Unexpected type: ", obj.GetType().FullName));
                }
                Trace.Write(", ");
            }
            Trace.Write("}");
        }

        [TestInitialize]
        public void Initialize()
        {
            actualOutput.Clear();
        }
    }
}
