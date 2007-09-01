import sys
import codecs

tests = codecs.open(sys.argv[1], 'r', 'utf-8')

output = codecs.open(sys.argv[2], 'w', 'ascii', 'backslashreplace')

prefix = sys.argv[3].replace('.', '_')

class LinesIterator(object):
	def __init__(self, iterable):
		self._iterator = iter(iterable)

	def __iter__(self):
		# Below, we'll use nested for...in loops over an instance of this class
		# so make sure we are an iteraTOR and not just an iteraBLE.
		return self

	def next(self):
		line = self._iterator.next()
		if line.endswith('\n'):
			line = line[:-1]
		if line.endswith('\r'):
			line = line[:-1]
		return line

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

tests = LinesIterator(tests)

i = 0
for line in tests:
	if line.startswith('#data'):
		inputLines = []
		for line in tests:
			if line.startswith('#error'):
				break
			else:
				inputLines.append(line)
		input = '\n'.join(inputLines)
		
		parseErrors = []
		for line in tests:
			if line.startswith('#document'):
				break
			else:
				parseErrors.append(line)
		
		outputLines = []
		for line in tests:
			if not line:
				# assuming tests are separated by a blank line
				break
			else:
				outputLines.append(line)
		expectedOutput = '\n'.join(outputLines)
		
		output.write("""
		[TestMethod]
		[Description("%s")]
#if NUNIT
		[Category("TreeConstruction.%s")]
#endif
		public void Test_%s_%d()
		{
			DoTest("%s", "%s", new string[] { %s });
		}
		""" % (input.replace('"', '\\"').replace('\n', '\\n'),
				prefix, prefix, i,
				input.replace('"', '\\"').replace('\n', '\\n'),
				expectedOutput.replace('"', '\\"').replace('\n', '\\n'),
				", ".join(['"%s"' % error.replace('"', '\\"').replace('\n', '\\n') for error in parseErrors])))
		
		i += 1

output.write("""
	}
}
""")

output.flush()
output.close()
