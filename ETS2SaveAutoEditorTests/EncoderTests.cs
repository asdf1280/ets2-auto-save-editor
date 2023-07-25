using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ETS2SaveAutoEditorTests {
    using ETS2SaveAutoEditor;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using System.Collections.Generic;

    [TestClass]
    public class PositionCodeEncoderTests {
        [TestMethod]
        public void EncodeAndDecodePositionCode_ValidData_RoundTrip() {
            // Arrange
            PositionData testData = new PositionData {
                TrailerConnected = true,
                Positions = new List<float[]>
                {
                new float[] { 1.0f, 2.0f, 3.0f, 4.0f, 5.0f, 6.0f, 7.0f },
                new float[] { 8.0f, 9.0f, 10.0f, 11.0f, 12.0f, 13.0f, 14.0f },
            }
            };

            // Act
            string encoded = PositionCodeEncoder.EncodePositionCode(testData);
            PositionData decodedData = PositionCodeEncoder.DecodePositionCode(encoded);

            // Assert
            Assert.AreEqual(testData.TrailerConnected, decodedData.TrailerConnected);
            Assert.AreEqual(testData.Positions.Count, decodedData.Positions.Count);
            for (int i = 0; i < testData.Positions.Count; i++) {
                CollectionAssert.AreEqual(testData.Positions[i], decodedData.Positions[i]);
            }
        }
    }

}
