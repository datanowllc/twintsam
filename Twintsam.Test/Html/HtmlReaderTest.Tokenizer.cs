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

namespace Twintsam.Html
{
    [TestClass]
    public partial class HtmlReaderTest
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
            actualOutput.Add("ParseError");
        }

        private void DoTest(string input, IEnumerable expectedOutput)
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
                    Hashtable attrs = new Hashtable();
                    foreach (object attr in attributes) {
                        attrs.Add(
                            HtmlReader_Attribute_name.GetValue(attr),
                            HtmlReader_Attribute_value.GetValue(attr));
                    }
                    actualOutput.Add(new object[] { "StartTag", name, attrs });
                    break;
                case XmlNodeType.EndElement:
                    actualOutput.Add(new object[] { "EndTag", name });
                    break;
                case XmlNodeType.Comment:
                    actualOutput.Add(new object[] { "Comment", value });
                    break;
                case XmlNodeType.Text:
                    actualOutput.Add(new object[] { "Character", value });
                    break;
                default:
                    Assert.Fail("Unexpected token type: {0}", nodeType);
                    break;
                }
            }
        }

        [TestInitialize]
        public void Initialize()
        {
            actualOutput.Clear();
        }
    }
}
