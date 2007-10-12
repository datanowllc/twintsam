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
		return line + "\n"

class DefaultDict(dict):
    def __init__(self, default, *args, **kwargs):
        self.default = default
        dict.__init__(self, *args, **kwargs)
    
    def __getitem__(self, key):
        return dict.get(self, key, self.default)

class TestData(object):
    def __init__(self, f, newTestHeading="data"):
        self.f = f
        self.newTestHeading = newTestHeading
    
    def __iter__(self):
        data = DefaultDict(None)
        key=None
        for line in self.f:
            heading = self.isSectionHeading(line)
            if heading:
                if data and heading == self.newTestHeading:
                    #Remove trailing newline
                    data[key] = data[key][:-1]
                    yield self.normaliseOutput(data)
                    data = DefaultDict(None)
                key = heading
                data[key]=""
            elif key is not None:
                data[key] += line
        if data:
            yield self.normaliseOutput(data)
        
    def isSectionHeading(self, line):
        """If the current heading is a test section heading return the heading,
        otherwise return False"""
        if line.startswith("#"):
            return line[1:].strip()
        else:
            return False
    
    def normaliseOutput(self, data):
        #Remove trailing newlines
        for key,value in data.iteritems():
            if value.endswith("\n"):
                data[key] = value[:-1]
        return data

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

i = 0
for test in TestData(LinesIterator(tests)):
		input = test.get("data", "")
		parseErrors = test.get("errors", "").split("\n")
		fragmentContainer = test.get("document-fragment", None)
		expectedOutput = test.get("document", "")
		
		output.write("""
		[TestMethod]
		[Description("%s")]
#if NUNIT
		[Category("TreeConstruction.%s")]
#endif
		public void Test_%s_%d()
		{
			DoTest("%s", %s, "%s", new string[] { %s });
		}
		""" % (input.replace('"', '\\"').replace('\n', '\\n'),
				prefix, prefix, i,
				input.replace('"', '\\"').replace('\n', '\\n'),
				fragmentContainer is None and 'null' or '"%s"' % fragmentContainer,
				expectedOutput.replace('"', '\\"').replace('\n', '\\n'),
				", ".join(['"%s"' % error.replace('"', '\\"').replace('\n', '\\n') for error in parseErrors])))
		
		i += 1

output.write("""
	}
}
""")

output.flush()
output.close()
