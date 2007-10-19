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
using System.IO;

namespace Twintsam.IO
{
    [TestClass]
    public class SeekableReadOnlyStreamConstructorTest
    {
        [TestMethod]
        [ExpectedException(typeof(ArgumentNullException))]
        public void ConstructorThrowsExceptionOnNullArgument()
        {
            new SeekableReadOnlyStream(null);
        }

        [TestMethod]
        [ExpectedException(typeof(ArgumentOutOfRangeException))]
        public void ConstructorThrowsExceptionOnNegativeBufferSize()
        {
            new SeekableReadOnlyStream(Stream.Null, -1);
        }

        [TestMethod]
        [ExpectedException(typeof(ArgumentOutOfRangeException))]
        public void ConstructorThrowsExceptionOnZeroBufferSize()
        {
            new SeekableReadOnlyStream(Stream.Null, 0);
        }
    }

    [TestClass]
    public class SeekableReadOnlyStreamTest
    {
        private static readonly byte[] TEST_BYTES = new byte[] { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10 };

        private SeekableReadOnlyStream stream;

        [TestInitialize]
        public void SetUp()
        {
            Stream baseStream = new MemoryStream(TEST_BYTES);
            stream = new SeekableReadOnlyStream(baseStream);
        }

        [TestMethod]
        public void TestReadByte()
        {
            foreach (byte b in TEST_BYTES) {
                Assert.AreEqual((int)b, stream.ReadByte());
            }
            Assert.AreEqual(-1, stream.ReadByte());
        }

        [TestMethod]
        public void TestRead()
        {
            byte[] bytes = new byte[TEST_BYTES.Length];
            Assert.AreEqual(bytes.Length, stream.Read(bytes, 0, bytes.Length));
            CollectionAssert.AreEqual(TEST_BYTES, bytes);
        }

        private void TestPartialRead(int offset, int count)
        {
            byte[] bytes = new byte[offset + count];
            Assert.AreEqual(Math.Min(TEST_BYTES.Length, count),
                stream.Read(bytes, offset, count));
            byte[] expectedBytes = new byte[offset + count];
            Buffer.BlockCopy(TEST_BYTES, 0, expectedBytes, offset,
                Math.Min(TEST_BYTES.Length, count));
            CollectionAssert.AreEqual(expectedBytes, bytes);
        }

        [TestMethod]
        public void TestReadWithShorterDestination()
        {
            TestPartialRead(0, TEST_BYTES.Length - 1);
        }

        [TestMethod]
        public void TestReadWithOffset()
        {
            TestPartialRead(1, TEST_BYTES.Length - 1);
        }

        [TestMethod]
        public void TestReadWithLongerDestination()
        {
            TestPartialRead(0, TEST_BYTES.Length + 1);
        }

        [TestMethod]
        public void TestReadWithLongerDestinationAndOffset()
        {
            TestPartialRead(1, TEST_BYTES.Length);
        }

        [TestMethod]
        public void TestMultipleRead()
        {
            byte[] bytes = new byte[TEST_BYTES.Length];
            Assert.AreEqual(TEST_BYTES.Length / 2, stream.Read(bytes, 0, TEST_BYTES.Length / 2));
            Assert.AreEqual(TEST_BYTES.Length / 2, stream.Read(bytes, TEST_BYTES.Length / 2, TEST_BYTES.Length / 2));
            CollectionAssert.AreEqual(TEST_BYTES, bytes);
        }

        [TestMethod]
        public void TestGetPosition()
        {
            long position = 0;
            foreach (byte b in TEST_BYTES) {
                Assert.AreEqual(position++, stream.Position);
                stream.ReadByte();
            }
            Assert.AreEqual(position, stream.Position);
        }

        [TestMethod]
        public void TestSetPositionToSameValue()
        {
            stream.ReadByte();
            Assert.AreEqual(1L, stream.Position);
            stream.Position = 1;
            Assert.AreEqual(1L, stream.Position);
        }

        [TestMethod]
        public void TestSetPositionToLowerValue()
        {
            stream.ReadByte();
            Assert.AreEqual(1L, stream.Position);
            stream.Position = 0;
            Assert.AreEqual(0L, stream.Position);
        }

        [TestMethod]
        [ExpectedException(typeof(InvalidOperationException))]
        public void TestSetPositionToUpperValue()
        {
            stream.ReadByte();
            stream.Position = stream.Position + 1;
        }

        [TestMethod]
        [ExpectedException(typeof(ArgumentOutOfRangeException))]
        public void TestSetPositionToNegativeValue()
        {
            stream.ReadByte();
            stream.Position = -1;
        }

        [TestMethod]
        [ExpectedException(typeof(InvalidOperationException))]
        public void TestSeekFromEnd()
        {
            stream.Seek(0, SeekOrigin.End);
        }

        [TestMethod]
        public void TestSeekToSamePositionFromBeginning()
        {
            byte[] bytes = new byte[2];
            stream.Read(bytes, 0, 2);
            Assert.AreEqual(2L, stream.Position);
            Assert.AreEqual(2L, stream.Seek(2, SeekOrigin.Begin));
            Assert.AreEqual(2L, stream.Position);
        }

        [TestMethod]
        public void TestSeekToSamePositionFromCurrent()
        {
            byte[] bytes = new byte[2];
            stream.Read(bytes, 0, 2);
            Assert.AreEqual(2L, stream.Position);
            Assert.AreEqual(2L, stream.Seek(0, SeekOrigin.Current));
            Assert.AreEqual(2L, stream.Position);
        }

        [TestMethod]
        public void TestSeekBackwardsFromBeginning()
        {
            byte[] bytes = new byte[2];
            stream.Read(bytes, 0, 2);
            Assert.AreEqual(2L, stream.Position);
            Assert.AreEqual(1L, stream.Seek(1, SeekOrigin.Begin));
            Assert.AreEqual(1L, stream.Position);
        }

        [TestMethod]
        public void TestSeekBackwardsFromCurrent()
        {
            byte[] bytes = new byte[2];
            stream.Read(bytes, 0, 2);
            Assert.AreEqual(2L, stream.Position);
            Assert.AreEqual(1L, stream.Seek(-1, SeekOrigin.Current));
            Assert.AreEqual(1L, stream.Position);
        }

        [TestMethod]
        [ExpectedException(typeof(InvalidOperationException))]
        public void TestSeekForwardsFromBeginning()
        {
            stream.Seek(1, SeekOrigin.Begin);
        }

        [TestMethod]
        [ExpectedException(typeof(InvalidOperationException))]
        public void TestSeekForwardsFromCurrent()
        {
            byte[] bytes = new byte[2];
            stream.Read(bytes, 0, 2);
            stream.Seek(1, SeekOrigin.Current);
        }
    }
}
