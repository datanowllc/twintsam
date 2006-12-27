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
    public partial class HtmlReaderTokenizationTest
    {
        private static MethodInfo HtmlReader_ParseToken =
            typeof(HtmlReader).GetMethod("ParseToken", BindingFlags.Instance | BindingFlags.NonPublic);
        private static FieldInfo HtmlReader__tokenType =
            typeof(HtmlReader).GetField("_tokenType", BindingFlags.Instance | BindingFlags.NonPublic);
        private static FieldInfo HtmlReader__name =
            typeof(HtmlReader).GetField("_name", BindingFlags.Instance | BindingFlags.NonPublic);
        private static FieldInfo HtmlReader__value =
            typeof(HtmlReader).GetField("_value", BindingFlags.Instance | BindingFlags.NonPublic);
        private static FieldInfo HtmlReader__attributes =
            typeof(HtmlReader).GetField("_attributes", BindingFlags.Instance | BindingFlags.NonPublic);
        private static FieldInfo HtmlReader__doctypeInError =
            typeof(HtmlReader).GetField("_doctypeInError", BindingFlags.Instance | BindingFlags.NonPublic);

        private static Type HtmlReader_Attribute =
            typeof(HtmlReader).GetNestedType("Attribute", BindingFlags.Instance | BindingFlags.NonPublic);
        private static FieldInfo HtmlReader_Attribute_name =
            HtmlReader_Attribute.GetField("name");
        private static FieldInfo HtmlReader_Attribute_value =
            HtmlReader_Attribute.GetField("value");

        private ArrayList actualOutput = new ArrayList();

        private void reader_ParseError(object source, ParseErrorEventArgs args)
        {
            if (actualOutput.Count == 0 || actualOutput[actualOutput.Count - 1] as string != "ParseError") {
                actualOutput.Add("ParseError");
            }
        }

        private void DoTest(string input, IList expectedOutput)
        {
            HtmlReader reader = new HtmlReader(new StringReader(input));
            reader.ParseError += new EventHandler<ParseErrorEventArgs>(reader_ParseError);

            while ((bool) HtmlReader_ParseToken.Invoke(reader, null)) {
                XmlNodeType nodeType = (XmlNodeType) HtmlReader__tokenType.GetValue(reader);
                string name = (string) HtmlReader__name.GetValue(reader);
                string value = (string) HtmlReader__value.GetValue(reader);
                IEnumerable attributes = (IEnumerable) HtmlReader__attributes.GetValue(reader);
                bool doctypeInError = (bool) HtmlReader__doctypeInError.GetValue(reader);

                switch (nodeType) {
                case XmlNodeType.DocumentType:
                    actualOutput.Add(new object[] { "DOCTYPE", name, doctypeInError });
                    break;
                case XmlNodeType.Element:
                    Dictionary<string,string> attrs = new Dictionary<string,string>(StringComparer.InvariantCulture);
                    foreach (object attr in attributes) {
                        attrs.Add(
                            (string)HtmlReader_Attribute_name.GetValue(attr),
                            (string)HtmlReader_Attribute_value.GetValue(attr));
                    }
                    actualOutput.Add(new object[] { "StartTag", name, attrs });
                    break;
                case XmlNodeType.EndElement:
                    actualOutput.Add(new string[] { "EndTag", name });
                    break;
                case XmlNodeType.Comment:
                    actualOutput.Add(new string[] { "Comment", value });
                    break;
                case XmlNodeType.Text:
                    actualOutput.Add(new string[] { "Character", value });
                    break;
                default:
                    Assert.Fail("Unexpected token type: {0}", nodeType);
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

                Assert.AreEqual(expected.GetType(), actual.GetType());

                if (expected.GetType() != typeof(string)) {
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
        }

        [Conditional("TRACE")]
        private void TraceOutput(ICollection collection)
        {
            Trace.Write("{ ");
            foreach (object obj in collection) {
                if (obj is ICollection) {
                    TraceOutput((ICollection)obj);
                } else if (obj is KeyValuePair<string,string>) {
                    KeyValuePair<string, string> pair = (KeyValuePair<string, string>)obj;
                    Trace.Write('"');
                    Trace.Write(pair.Key);
                    Trace.Write("\"=\"");
                    Trace.Write(pair.Value);
                    Trace.Write('"');
                } else {
                    Trace.Write('"');
                    Trace.Write(obj);
                    Trace.Write('"');
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
