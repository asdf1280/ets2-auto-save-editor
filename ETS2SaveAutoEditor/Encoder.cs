using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace ETS2SaveAutoEditor {
    internal enum PositionDataHeader : byte {
        KEY,
        END
    }


    internal struct PositionData {
        public List<float[]> Positions;
        public bool TrailerConnected;
    }

    internal class PositionCodeEncoder {
        private static readonly int POSITION_DATA_VERSION = 4;

        public static string EncodePositionCode(PositionData data) {
            MemoryStream memoryStream = new MemoryStream();
            BinaryWriter binaryWriter = new BinaryWriter(memoryStream, Encoding.ASCII);
            binaryWriter.Write(POSITION_DATA_VERSION);

            void sendPlacement(float[] p) {
                for (int t = 0; t < 7; t++) {
                    binaryWriter.Write(p[t]);
                }
            }
            binaryWriter.Write((byte)(data.Positions.Count + (data.TrailerConnected ? 1 << 7 : 0)));
            foreach (float[] p in data.Positions) {
                sendPlacement(p);
            }

            binaryWriter.Close();
            string encoded = Convert.ToBase64String(memoryStream.ToArray());
            int Eqs = 0;
            int i;
            for (i = encoded.Length - 1; i >= 0; i--) {
                if (encoded[i] == '=') {
                    Eqs++;
                } else break;
            }
            return encoded.Substring(0, i + 1) + Eqs.ToString("X");
        }
        public static PositionData DecodePositionCode(string encoded) {
            {
                Match matchCompression = Regex.Match(encoded, "(.)$");
                int Eqs = Convert.ToInt32(matchCompression.Groups[1].Value, 16);
                int segmentLength = matchCompression.Groups[0].Value.Length;
                encoded = encoded.Substring(0, encoded.Length - segmentLength);
                for (int i = 0; i < Eqs; i++) {
                    encoded += '=';
                }
            }
            byte[] data = Convert.FromBase64String(encoded);
            List<float[]> list = new List<float[]>();

            MemoryStream memoryStream = new MemoryStream(data);
            BinaryReader binaryReader = new BinaryReader(memoryStream, Encoding.UTF8);

            // Compatibility layer
            int version = binaryReader.ReadInt32();
            if (version != POSITION_DATA_VERSION) {
                if (version == 3) {
                    return DecodePositionCodeV3(encoded);
                }
                if (version == 2) {
                    var v2Positions = DecodePositionCodeV2(encoded);
                    return new PositionData {
                        TrailerConnected = true,
                        Positions = v2Positions
                    };
                }
                throw new IOException("incompatible version");
            }

            // Data exchange
            float[] receivePlacement() {
                float[] result = new float[7];
                for (int i = 0; i < 7; i++) {
                    result[i] = binaryReader.ReadSingle();
                }
                return result;
            }

            var length = binaryReader.ReadByte();
            var trailerConnected = (length & 1 << 7) > 0;
            length = (byte)(length & (~(1 << 7)));
            for (int i = 0; i < length; i++) {
                list.Add(receivePlacement());
            }
            return new PositionData {
                Positions = list,
                TrailerConnected = trailerConnected
            };
        }

        private static PositionData DecodePositionCodeV3(string encoded) {
            byte[] data = Convert.FromBase64String(encoded);
            List<float[]> list = new List<float[]>();

            MemoryStream memoryStream = new MemoryStream(data);
            BinaryReader binaryReader = new BinaryReader(memoryStream, Encoding.UTF8);

            // Compatibility layer
            int version = binaryReader.ReadInt32();
            if (version != 3) throw new IOException("incompatible version");

            // Data exchange
            float[] receivePlacement() {
                float[] result = new float[7];
                for (int i = 0; i < 7; i++) {
                    result[i] = binaryReader.ReadSingle();
                }
                return result;
            }

            var length = binaryReader.ReadByte();
            var trailerConnected = (length & 1 << 7) > 0;
            length = (byte)(length & (~(1 << 7)));
            for (int i = 0; i < length; i++) {
                list.Add(receivePlacement());
            }
            return new PositionData {
                Positions = list,
                TrailerConnected = trailerConnected
            };
        }

        private static List<float[]> DecodePositionCodeV2(string encoded) {
            byte[] data = Convert.FromBase64String(encoded);
            List<float[]> list = new List<float[]>();

            MemoryStream memoryStream = new MemoryStream(data);
            BinaryReader binaryReader = new BinaryReader(memoryStream, Encoding.UTF8);
            PositionDataHeader receiveHeader() {
                return (PositionDataHeader)binaryReader.ReadByte();
            }
            float[] receivePlacement() {
                float[] result = new float[7];
                for (int i = 0; i < 7; i++) {
                    result[i] = binaryReader.ReadSingle();
                }
                return result;
            }
            int version = binaryReader.ReadInt32();
            if (version != 2) throw new IOException("incompatible version");
            while (receiveHeader() == PositionDataHeader.KEY) {
                list.Add(receivePlacement());
            }
            return list;
        }
    }

    internal class SCSSpecialString {
        public static float ParseScsFloat(string data) {
            if (data.StartsWith("&")) {
                byte[] bytes = new byte[4];
                for (int i = 0; i < 4; i++) {
                    bytes[i] = byte.Parse(data.Substring(i * 2 + 1, 2), System.Globalization.NumberStyles.HexNumber);
                }
                return BitConverter.ToSingle(bytes, 0);
            } else {
                return float.Parse(data);
            }
        }

        public static string EncodeScsFloat(float value) {
            byte[] bytes = BitConverter.GetBytes(value);
            string hexString = BitConverter.ToString(bytes).Replace("-", "").ToLower();
            return "&" + hexString;
        }

        public static float[] DecodeSCSPosition(string placement) {
            var a = placement.Split(new string[] { "(", ")", ",", ";" }, StringSplitOptions.RemoveEmptyEntries);
            var q = from v in a select v.Trim() into b where b.Length > 0 select ParseScsFloat(b);
            return q.ToArray();
        }

        public static string EncodeSCSPosition(float[] data) {
            var data0 = (from d in data select EncodeScsFloat(d)).ToArray();
            return $"({data0[0]}, {data0[1]}, {data0[2]}) ({data0[3]}; {data0[4]}, {data0[5]}, {data0[6]})";
        }
    }

    internal class SCSSaveHexEncodingSupport {
        public static byte[] StringToByteArray(string hex) {
            int NumberChars = hex.Length / 2;
            byte[] bytes = new byte[NumberChars];
            using (var sr = new StringReader(hex)) {
                for (int i = 0; i < NumberChars; i++)
                    bytes[i] =
                      Convert.ToByte(new string(new char[2] { (char)sr.Read(), (char)sr.Read() }), 16);
            }
            return bytes;
        }

        public static string ByteArrayToString(byte[] bytes) {
            StringBuilder sb = new StringBuilder();
            foreach (byte b in bytes) {
                sb.Append(b.ToString("X2"));
            }
            return sb.ToString();
        }

        public static string GetUnescapedSaveName(string originalString) {
            originalString = originalString.Replace("@@noname_save_game@@", "Quick Save");
            if (originalString.Length == 0) {
                originalString = "[Autosave]";
            }
            var ml = Regex.Matches(originalString, @"(?<=[^\\]|^)\\");
            var hexString = "";
            for (int i = 0; i < originalString.Length; i++) {
                var found = false;
                var ch = originalString[i];
                for (int j = 0; j < ml.Count; j++) {
                    if (i == ml[j].Index) {
                        found = true;
                        ++i; // skip backslash
                        hexString += originalString[++i];
                        hexString += originalString[++i];
                    }
                }
                if (!found) {
                    byte[] stringBytes = Encoding.UTF8.GetBytes(ch + "");
                    StringBuilder sbBytes = new StringBuilder(stringBytes.Length * 2);
                    foreach (byte b in stringBytes) {
                        sbBytes.AppendFormat("{0:X2}", b);
                    }
                    hexString += sbBytes.ToString();
                }
            }
            byte[] dBytes = StringToByteArray(hexString);
            return Encoding.UTF8.GetString(dBytes);
        }

        public static string GetEscapedSaveName(string rawString) {
            byte[] utf8Bytes = Encoding.UTF8.GetBytes(rawString);

            StringBuilder stringBuilder = new StringBuilder();

            foreach (byte b in utf8Bytes) {
                stringBuilder.Append("\\x");
                stringBuilder.Append(b.ToString("x2"));
            }

            return stringBuilder.ToString();
        }
    }
}
