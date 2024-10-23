using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ETS2SaveAutoEditor.Utils {
    public struct PositionData {
        public List<float[]> Positions;
        public bool TrailerConnected;
    }

    public class PositionCodeEncoder {
        private static readonly int POSITION_DATA_VERSION = 2;

        public static string EncodePositionCode(PositionData data) {
            MemoryStream ms1 = new();
            BinaryWriter bs1 = new(ms1);
            {
                byte[] versionBuf = BitConverter.GetBytes(POSITION_DATA_VERSION);
                if (!BitConverter.IsLittleEndian) { // Reverse the byte order if the system is big-endian.
                    Array.Reverse(versionBuf);
                }
                bs1.Write(versionBuf);
            }

            MemoryStream ms2 = new MemoryStream();
            BinaryWriter bs2 = new BinaryWriter(ms2);

            void sendPlacement(float[] p) {
                for (int t = 0; t < 7; t++) {
                    byte[] b = BitConverter.GetBytes(p[t]);
                    // The coordinate values must be stored in big-endian format due to a bug in the initial implementation.
                    if (BitConverter.IsLittleEndian)
                        Array.Reverse(b);
                    bs2.Write(b);
                }
            }
            bs2.Write((byte)((data.Positions.Count & ~(1 << 7)) + (data.TrailerConnected ? 1 << 7 : 0)));
            foreach (float[] p in data.Positions) {
                sendPlacement(p);
            }

            bs2.Close();

            bs1.Write(AESEncoder.InstanceC.BEncode(ms2.ToArray()));
            bs1.Close();

            string encoded = Base32768.EncodeBase32768(ms1.ToArray());
            return encoded;
        }
        public static PositionData DecodePositionCode(string encoded) {
            // Base32768 is our own encoding to minimize the length of the visible string.
            byte[] data = Base32768.DecodeBase32768(encoded);

            List<float[]> placements = new List<float[]>();

            MemoryStream ms1 = new(data);
            BinaryReader bs1 = new(ms1);

            // Compatibility layer.
            int version;
            {
                byte[] b = bs1.ReadBytes(4);
                if (!BitConverter.IsLittleEndian) { // Reverse the byte order if the system is big-endian.
                    Array.Reverse(b);
                }
                version = BitConverter.ToInt32(b, 0);
            }

            if (version != POSITION_DATA_VERSION && version != 1) {
                // redundant compatibility checks
                //if (version == 3 || version == 4) {
                //    return DecodePositionCodeV3V4(encoded);
                //}
                //if (version == 2) {
                //    var v2Positions = DecodePositionCodeV2(encoded);
                //    return new PositionData {
                //        TrailerConnected = true,
                //        Positions = v2Positions
                //    };
                //}
                throw new IOException("incompatible version");
            }

            MemoryStream tempBuf = new();
            ms1.CopyTo(tempBuf); // Copy remaining data to a temporary buffer. (without version header)

            // Decrypt if it's using the latest key.
            MemoryStream ms2 = new(AESEncoder.InstanceC.BDecode(tempBuf.ToArray()));
            BinaryReader bs2 = new(ms2);

            // Data exchange
            float[] receivePlacement() {
                float[] result = new float[7];
                for (int i = 0; i < 7; i++) {
                    byte[] bytes = bs2.ReadBytes(4);
                    if (BitConverter.IsLittleEndian) { // The coordinate values must be stored in big-endian format due to a bug in the initial implementation.
                        Array.Reverse(bytes);
                    }
                    result[i] = BitConverter.ToSingle(bytes, 0);
                }
                return result;
            }

            byte length = bs2.ReadByte(); // Count of placements
            var trailerConnected = (length & 1 << 7) > 0; // The first bit indicates whether the trailer is connected.
            length = (byte)(length & (~(1 << 7))); // The remaining bits indicate the number of placements.
            for (int i = 0; i < length; i++) {
                placements.Add(receivePlacement());
            }
            return new PositionData {
                Positions = placements,
                TrailerConnected = trailerConnected
            };
        }
    }
}
