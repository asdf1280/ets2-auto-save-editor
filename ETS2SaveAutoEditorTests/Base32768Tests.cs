using Microsoft.VisualStudio.TestTools.UnitTesting;
using ETS2SaveAutoEditor;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ETS2SaveAutoEditor.Tests {
    [TestClass()]
    public class Base32768Tests {
        [TestMethod]
        public void TestEmptyData() {
            byte[] data = new byte[0];
            string encoded = Base32768.EncodeBase32768(data);
            byte[] decoded = Base32768.DecodeBase32768(encoded);

            CollectionAssert.AreEqual(data, decoded);
        }

        [TestMethod]
        public void TestSingleByteData() {
            byte[] data = new byte[] { 42 };
            string encoded = Base32768.EncodeBase32768(data);
            byte[] decoded = Base32768.DecodeBase32768(encoded);

            CollectionAssert.AreEqual(data, decoded);
        }

        [TestMethod]
        public void TestMultipleBytesData() {
            byte[] data = new byte[] { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10 };
            string encoded = Base32768.EncodeBase32768(data);
            byte[] decoded = Base32768.DecodeBase32768(encoded);

            CollectionAssert.AreEqual(data, decoded);
        }

        [TestMethod]
        public void TestLargeData() {
            // Create a large random data array
            Random random = new Random();
            byte[] data = new byte[999];
            random.NextBytes(data);

            string encoded = Base32768.EncodeBase32768(data);
            byte[] decoded = Base32768.DecodeBase32768(encoded);

            CollectionAssert.AreEqual(data, decoded);
        }

        [TestMethod]
        public void TestNullData() {
            byte[] data = null;

            try {
                string encoded = Base32768.EncodeBase32768(data);
                Assert.Fail("Expected ArgumentNullException was not thrown.");
            } catch (ArgumentNullException) {
                // Test passed, ArgumentNullException was thrown as expected.
            }
        }
    }
}