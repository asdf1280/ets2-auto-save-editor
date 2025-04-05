using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;

namespace ASE.Utils {
    public struct PositionData {
        public List<float[]> Positions;
        public bool TrailerConnected;
        public bool MinifiedOrientation;
    }

    public class PositionCodeEncoder {
        private static readonly int POSITION_DATA_VERSION = 3;

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

            void sendPlacement(float[] p) {
                if (data.MinifiedOrientation) {
                    for (int i = 0; i < 4; i++)
                        ms2.Write(ByteEncoder.EncodeFloat(p[i], ByteOrder.BigEndian));

                    ms2.Write(ByteEncoder.EncodeFloat(p[5], ByteOrder.BigEndian));
                } else {
                    for (int i = 0; i < 7; i++)
                        ms2.Write(ByteEncoder.EncodeFloat(p[i], ByteOrder.BigEndian));
                }
            }

            // Header byte
            ms2.WriteByte((byte)((data.Positions.Count & 0b00111111) | (data.TrailerConnected ? 1 << 7 : 0) | (data.MinifiedOrientation ? 1 << 6 : 0)));
            foreach (float[] p in data.Positions) {
                sendPlacement(p);
            }

            ms2.Close();

            bs1.Write(Compressor.Compress(ms2.ToArray()));
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

            if (version != POSITION_DATA_VERSION && version != 2 && version != 1) { // Version 1 and 2 can be supported without additional code.
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
            MemoryStream ms2 = new(Compressor.Decompress(tempBuf.ToArray()));
            BinaryReader bs2 = new(ms2);

            byte length = bs2.ReadByte(); // Count of placements
            var trailerConnected = (length & 1 << 7) > 0; // The first bit indicates whether the trailer is connected.
            bool minifiedOrientation = (length & 1 << 6) > 0; // The second bit indicates whether the orientation is minified.
            length = (byte)(length & 0b00111111); // The remaining bits indicate the number of placements.

            // Data exchange
            float[] receivePlacement() {
                float[] result = new float[7];
                if (minifiedOrientation) {
                    for (int i = 0; i < 5; i++) {
                        byte[] bytes = bs2.ReadBytes(4);
                        result[i] = ByteEncoder.DecodeFloat(bytes, ByteOrder.BigEndian);
                    }
                    result[5] = result[4];
                    result[4] = 0;
                    result[6] = 0;
                } else {
                    for (int i = 0; i < 7; i++) {
                        byte[] bytes = bs2.ReadBytes(4);
                        result[i] = ByteEncoder.DecodeFloat(bytes, ByteOrder.BigEndian);
                    }
                }
                return result;
            }

            for (int i = 0; i < length; i++) {
                placements.Add(receivePlacement());
            }
            return new PositionData {
                Positions = placements,
                TrailerConnected = trailerConnected
            };
        }
    }

    public struct NavigationData {
        public List<(byte, int, int, int)> WaypointBehind;
        public List<(byte, int, int, int)> WaypointAhead;
        public List<(byte, int, int, int)> Avoid;
    }

    public class NavigationDataEncoder {
        private static readonly uint NAVIGATION_DATA_VERSION = (437 << 8) | 1; // Random number to discern position data from navigation data.

        public static string EncodeNavigationCode(NavigationData data) {
            MemoryStream ms1 = new();
            ms1.Write(ByteEncoder.EncodeUInt32(NAVIGATION_DATA_VERSION, ByteOrder.LittleEndian));

            MemoryStream ms2 = new MemoryStream();

            void WritePoint((byte, int, int, int) p) {
                ms2.WriteByte(p.Item1);
                ms2.Write(ByteEncoder.EncodeInt32(p.Item2, ByteOrder.LittleEndian));
                ms2.Write(ByteEncoder.EncodeInt32(p.Item3, ByteOrder.LittleEndian));
                ms2.Write(ByteEncoder.EncodeInt32(p.Item4, ByteOrder.LittleEndian));
            }

            // You can only have up to 10 waypoints, and 11 if there's forced destination. Therefore 4 bits are enough.
            // However, we will use the whole byte for future compatibility.

            // Similarly, there can only be 10 avoid points. Therefore 4 bits are enough.

            // Write waypoints behind
            ms2.WriteByte((byte)(data.WaypointBehind.Count & 0xFF));
            foreach (var p in data.WaypointBehind) {
                WritePoint(p);
            }

            // Write waypoints ahead
            ms2.WriteByte((byte)(data.WaypointAhead.Count & 0xFF));
            foreach (var p in data.WaypointAhead) {
                WritePoint(p);
            }

            // Write avoid points
            ms2.WriteByte((byte)(data.Avoid.Count & 0xFF));
            foreach (var p in data.Avoid) {
                WritePoint(p);
            }

            ms2.Close();

            //// For debugging, return hex dump of ms2.ToArray()
            //return BitConverter.ToString(ms2.ToArray()).Replace("-", " ");

            ms1.Write(Compressor.Compress(ms2.ToArray()));
            ms1.Close();

            string encoded = Base32768.EncodeBase32768(ms1.ToArray());
            return encoded;
        }
        public static NavigationData DecodeNavigationCode(string encoded) {
            // Base32768 is our own encoding to minimize the length of the visible string.
            byte[] data = Base32768.DecodeBase32768(encoded);

            MemoryStream ms1 = new(data);
            BinaryReader bs1 = new(ms1);

            // Compatibility layer.
            uint version = ByteEncoder.DecodeUInt32(bs1.ReadBytes(4), ByteOrder.LittleEndian);

            if (version != NAVIGATION_DATA_VERSION) {
                throw new IOException("incompatible version");
            }

            MemoryStream tempBuf = new();
            ms1.CopyTo(tempBuf); // Copy remaining data to a temporary buffer. (without version header)

            MemoryStream ms2 = new(Compressor.Decompress(tempBuf.ToArray()));
            BinaryReader bs2 = new(ms2);

            // Data exchange
            (byte, int, int, int) ReceivePoint() {
                byte a = bs2.ReadByte();
                int b = ByteEncoder.DecodeInt32(bs2.ReadBytes(4), ByteOrder.LittleEndian);
                int c = ByteEncoder.DecodeInt32(bs2.ReadBytes(4), ByteOrder.LittleEndian);
                int d = ByteEncoder.DecodeInt32(bs2.ReadBytes(4), ByteOrder.LittleEndian);
                return (a, b, c, d);
            }

            List<(byte, int, int, int)> waypointsBehind = new();
            List<(byte, int, int, int)> waypointsAhead = new();
            List<(byte, int, int, int)> avoids = new();

            byte waypointBehindCount = bs2.ReadByte();
            for (int i = 0; i < waypointBehindCount; i++) {
                waypointsBehind.Add(ReceivePoint());
            }
            byte waypointAheadCount = bs2.ReadByte();
            for (int i = 0; i < waypointAheadCount; i++) {
                waypointsAhead.Add(ReceivePoint());
            }
            byte avoidCount = bs2.ReadByte();
            for (int i = 0; i < avoidCount; i++) {
                avoids.Add(ReceivePoint());
            }

            return new NavigationData {
                WaypointBehind = waypointsBehind,
                WaypointAhead = waypointsAhead,
                Avoid = avoids
            };
        }
    }
}
