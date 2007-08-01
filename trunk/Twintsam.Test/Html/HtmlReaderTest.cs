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
using System.Collections.Generic;

namespace Twintsam.Html
{
    [TestClass]
    public class HtmlReaderTest
    {
        [TestMethod]
        [ExpectedException(typeof(ArgumentNullException))]
        [Category("HtmlReader.ctor")]
        public void ConstructorThrowsExceptionOnNullArgument()
        {
            new HtmlReader(null);
        }
    }
}
