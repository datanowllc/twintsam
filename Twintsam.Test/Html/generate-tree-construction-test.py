import sys
import codecs

tests = codecs.open(sys.argv[1], 'r', 'utf-8')

output = codecs.open(sys.argv[2], 'w', 'utf-8')

# TODO

output.flush()
output.close()
