import sys
import codecs

tests = codecs.open(sys.argv[1], 'r', 'utf-8')

output = codecs.open(sys.argv[2], 'w', 'ascii', 'backslashreplace')

prefix = sys.argv[3].replace('.', '_')

output.write("""
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

namespace Twintsam.Html
{
    public partial class HtmlReaderTreeConstructionTest
    {
""")

# we use the fact that an iterator's __iter__ method
# returns the same object (with the same "iterating state")
# so we ensure we're working with an iteraTOR and not an iteraBLE
tests = iter(tests)

i = 0
for line in tests:
	if line.startswith('#data'):
		inputLines = []
		for line in tests:
			if line.startswith('#error'):
				break
			else:
				inputLines.append(line)
		if not line or not inputLines: # reached EOF
			break
		lastLine = inputLines[-1]
		if lastLine.endswith('\n'):
			lastLine = lastLine[:-1]
		if lastLine.endswith('\r'):
			lastLine = lastLine[:-1]
		inputLines[-1] = lastLine
		input = ''.join(inputLines)
		
		parseErrors = 0
		for line in tests:
			if line.startswith('#document'):
				break
			else:
				parseErrors += 1
		if not line: # reached EOF
			break
		
		outputLines = []
		for line in tests:
			if line in ['\n', '\r', '\r\n']:
				# assuming tests are separated by a blank line
				break
			else:
				outputLines.append(line)
		expectedOutput = ''.join(outputLines)
		
		output.write("""
		[TestMethod]
		[Category("TreeConstruction.%s")]
		public void Test_%s_%d()
		{
			DoTest(@"%s", %d, @"%s");
		}
		""" % (prefix, prefix, i, input.replace('"', '""'), parseErrors, expectedOutput.replace('"', '""')))
		
		i += 1

output.write("""
	}
}
""")

output.flush()
output.close()
