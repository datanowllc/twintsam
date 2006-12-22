import sys
import codecs
import simplejson

tests = simplejson.load(file(sys.argv[1]))

output = codecs.open(sys.argv[2], 'w', 'utf-8')

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
using System.Collections.Generic;

namespace Twintsam.Html
{
    public partial class HtmlReaderTokenizationTest
    {
""")

i = 0
for test in tests['tests']:
	description = test['description'].replace('"', '\"')
	output.write("""
#if !NUNIT
	[TestMethod]
	[Description("%s")]
#else
	[TestMethod(Description="%s")]
#endif
	public void Test_%s_%d()
	{
		DoTest("%s", new object[] {
	""" % (description, description, prefix, i, test['input']))
	
	for token in test['output']:
		if token in ('ParseError', 'AtheistParseError'):
			output.write('"%s", ' % token)
		elif token[0] == 'DOCTYPE':
			output.write('new object[] { "DOCTYPE", "%s", %s }, ' % (token[1], token[2] and 'true' or 'false'))
		elif token[0] == 'StartTag':
			output.write('new object[] { "StartTag", "%s", ' % token[1])
			if token[2]:
				output.write('new KeyValuePair<string,string>[] { ')
				for key, value in token[2].iteritems():
					output.write('new KeyValuePair<string,string>("%s", "%s"), ' % (key, value))
				output.write(' } ')
			else:
				output.write('null')
			output.write(' }, ')
		elif token[0] == 'EndTag':
			output.write('new string[] { "EndTag", "%s" }, ' % token[1])
		elif token[0] == 'Comment':
			output.write('new string[] { "Comment", "%s" }, ' % token[1])
		elif token[0] == 'Character':
			output.write('new string[] { "Character", "%s" }, ' % token[1])
	
	output.write("""
		});
	}
	""")
	i += 1

output.write("""
	}
}
""")

output.flush()
output.close()
