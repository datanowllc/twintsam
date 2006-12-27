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
	description = test['description'].replace('"', r'\"')
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
	""" % (description, description, prefix, i, test['input'].replace('"', r'\"')))
	
	tokens = list(test['output'])
	x = 1
	while x < len(tokens):
		if tokens[x][0] == 'Character' and tokens[x-1][0] == 'Character':
			# Merge consecutive 'Character' tokens
			tokens[x-1][1] += tokens[x][1]
			del tokens[x]
		elif tokens[x] == 'ParseError' and ((x == len(tokens) - 1) or ((x < len(tokens) - 1) and (tokens[x-1][0] == 'Character' and tokens[x+1][0] == 'Character'))):
			# Reorder 'ParseError' tokens if found in between to 'Character' tokens' or as the last token
			tokens[x] = tokens[x-1]
			tokens[x-1] = 'ParseError'
		else:
			x += 1
	
	for token in tokens:
		if token == 'ParseError':
			output.write('"%s", ' % token)
		elif token[0] == 'DOCTYPE':
			output.write('new object[] { "DOCTYPE", "%s", %s }, ' % (token[1].replace('"', r'\"'), token[2] and 'true' or 'false'))
		elif token[0] == 'StartTag':
			output.write('new object[] { "StartTag", "%s", ' % token[1].replace('"', r'\"'))
			output.write('new KeyValuePair<string,string>[] { ')
			for key, value in token[2].iteritems():
				output.write('new KeyValuePair<string,string>("%s", "%s"), ' % (key.replace('"', r'\"'), value.replace('"', r'\"')))
			output.write(' } ')
			output.write(' }, ')
		elif token[0] == 'EndTag':
			output.write('new string[] { "EndTag", "%s" }, ' % token[1].replace('"', r'\"'))
		elif token[0] == 'Comment':
			output.write('new string[] { "Comment", "%s" }, ' % token[1].replace('"', r'\"'))
		elif token[0] == 'Character':
			output.write('new string[] { "Character", "%s" }, ' % token[1].replace('"', r'\"'))
	
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
