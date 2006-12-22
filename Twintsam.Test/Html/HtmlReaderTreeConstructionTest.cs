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
            throw new NotImplementedException();
        }

        [TestInitialize]
        public void Initialize()
        {
            parseErrors = 0;
        }
    }
}
