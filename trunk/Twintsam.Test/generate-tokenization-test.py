import sys
import codecs
import simplejson

try:
	from cStringIO import StringIO
except:
	from StringIO import StringIO

tests = simplejson.load(file(sys.argv[1]))

output = codecs.open(sys.argv[2], 'w', 'ascii', 'backslashreplace')

prefix = sys.argv[3].replace('.', '_')

contentModelFlags = {
	'PCDATA': 'ContentModel.Pcdata',
	'RCDATA': 'ContentModel.Rcdata',
	'CDATA': 'ContentModel.Cdata',
	'PLAINTEXT': 'ContentModel.PlainText',
}

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
	input = test['input'].replace('"', r'\"')
	lastStartTag = test.get('lastStartTag', '').replace('"', r'\"')
	
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

	expectedOutput = u'new object[] { '
	for token in tokens:
		if token == 'ParseError':
			expectedOutput += u'"ParseError", '
		elif token[0] == 'DOCTYPE':
			expectedOutput += u'new object[] { "DOCTYPE", "%s", %s }, ' % (token[1].replace('"', r'\"'), token[2] and 'true' or 'false')
		elif token[0] == 'StartTag':
			expectedOutput += u'new object[] { "StartTag", "%s", ' % token[1].replace('"', r'\"')
			expectedOutput += u'new KeyValuePair<string,string>[] { '
			for key, value in token[2].iteritems():
				expectedOutput += u'new KeyValuePair<string,string>("%s", "%s"), ' % (key.replace('"', r'\"'), value.replace('"', r'\"'))
			expectedOutput += u' } '
			expectedOutput += u' }, '
		elif token[0] == 'EndTag':
			expectedOutput += u'new string[] { "EndTag", "%s" }, ' % token[1].replace('"', r'\"')
		elif token[0] == 'Comment':
			expectedOutput += u'new string[] { "Comment", "%s" }, ' % token[1].replace('"', r'\"')
		elif token[0] == 'Character':
			expectedOutput += u'new string[] { "Character", "%s" }, ' % token[1].replace('"', r'\"')
	expectedOutput += u'}'
			
	contentModels = test.get('contentModelFlags', [test.get('contentModelFlag', 'PCDATA')])

	for contentModel in contentModels:
		output.write("""
#if !NUNIT
		[TestMethod]
		[Description("%s")]
#else
		[TestMethod(Description="%s")]
#endif
		public void Test_%s_%d_%s()
		{
			DoTest("%s", %s, %s, "%s");
		}
		""" % (description, description, prefix, i, contentModel, \
			input.replace('\0', '\\0'), expectedOutput, contentModelFlags.get(contentModel, 'ContentModel.Pcdata'), lastStartTag))
	i += 1

output.write("""
	}
}
""")

output.flush()
output.close()
