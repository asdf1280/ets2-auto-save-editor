using System;
using System.Collections.Generic;
using System.Data.Common;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows;

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

    internal class HexEncoder {
        public static string ByteArrayToHexString(byte[] byteArray) {
            return BitConverter.ToString(byteArray).Replace("-", string.Empty);
        }

        public static byte[] HexStringToByteArray(string hexString) {
            int byteCount = hexString.Length / 2;
            byte[] byteArray = new byte[byteCount];

            for (int i = 0; i < byteCount; i++) {
                byteArray[i] = Convert.ToByte(hexString.Substring(i * 2, 2), 16);
            }

            return byteArray;
        }
    }

    internal class SCSSaveHexEncodingSupport {

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
            byte[] dBytes = HexEncoder.HexStringToByteArray(hexString);
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
    public class AlphabetEncoder {
        public static string Encode(string input) {
            if (string.IsNullOrEmpty(input))
                return input;

            char[] encodedChars = new char[input.Length];

            for (int i = 0; i < input.Length; i++) {
                char currentChar = input[i];

                if (currentChar >= 'A' && currentChar <= 'Z') {
                    int encodedValue = ((currentChar - 'A') + 12) % 26; // Apply the encoding rule

                    encodedChars[i] = (char)('A' + encodedValue);
                } else {
                    encodedChars[i] = currentChar; // If the character is not an uppercase letter, keep it unchanged
                }
            }

            return new string(encodedChars);
        }

        public static string Decode(string input) {
            if (string.IsNullOrEmpty(input))
                return input;

            char[] decodedChars = new char[input.Length];

            for (int i = 0; i < input.Length; i++) {
                char currentChar = input[i];

                if (currentChar >= 'A' && currentChar <= 'Z') {
                    int decodedValue = ((currentChar - 'A') - 12 + 26) % 26; // Apply the decoding rule

                    decodedChars[i] = (char)('A' + decodedValue);
                } else {
                    decodedChars[i] = currentChar; // If the character is not an uppercase letter, keep it unchanged
                }
            }

            return new string(decodedChars);
        }
    }

    internal class AESEncoder {
        public static AESEncoder InstanceA = new AESEncoder("A42Twypl*H03FV9XFVrjLJATCyxrc2bsE2qUFFvII@&l5WIFmy", "6Jvp0*1a2#ROBzQ1B5L3vIWK4F#spys$Lmcv7q8p!L8zcRfL!p");
        public static AESEncoder InstanceB = new AESEncoder("iU9SVr1mhkH%#I9LaZo4jjIBSl8X5u*cc2O0Ol%tjj4ahTwXr&", "7AaG#ZcoPJ@rF*eaLT!*@S2Zxc357W!6DcUYX63Wo*vRo44cdy");

        private static byte[] GenerateLength(string rawString, int bytes) {
            var data = Encoding.UTF8.GetBytes(rawString);
            int dataBytes = data.Length;
            using (SHA256 sha256 = SHA256.Create()) {
                byte[] arr = new byte[bytes];
                int offset = 0;
                while (offset < bytes) {
                    int length = Math.Min(dataBytes, bytes - offset);
                    Buffer.BlockCopy(data, 0, arr, offset, length);
                    offset += length;
                }

                return arr;
            }
        }

        public Aes AES;

        public static byte[] GetDataChecksum(string data) {
            return GetDataChecksum(Encoding.UTF8.GetBytes(data));
        }

        public static byte[] GetDataChecksum(byte[] data) {
            using (SHA256 sha = SHA256.Create()) {
                return sha.ComputeHash(data);
            }
        }

        private AESEncoder(string key, string iv) {
            AES = Aes.Create();
            AES.Key = GenerateLength(key, 256 / 16);
            AES.IV = GenerateLength(iv, 256 / 16);
            AES.Mode = CipherMode.CBC;
            AES.Padding = PaddingMode.PKCS7;
        }

        public string Encode(string original) {
            var encryptor = AES.CreateEncryptor();
            var ms = new MemoryStream();
            var cs = new CryptoStream(ms, encryptor, CryptoStreamMode.Write);
            var zs = new DeflateStream(cs, CompressionLevel.Optimal, false);
            var w = new StreamWriter(zs, Encoding.UTF8);
            w.Write(original);
            w.Close(); // Deflatestream must be closed to compress the data
            zs.Close();
            cs.Close();

            var data = HexEncoder.ByteArrayToHexString(ms.ToArray());
            MessageBox.Show(data);
            return data;
        }

        public string Decode(string hex) {
            MessageBox.Show(hex);
            var array = HexEncoder.HexStringToByteArray(hex);

            var decryptor = AES.CreateDecryptor();
            var ms = new MemoryStream(array);
            var cs = new CryptoStream(ms, decryptor, CryptoStreamMode.Read);
            var zs = new DeflateStream(cs, CompressionMode.Decompress);
            var data = new StreamReader(zs, Encoding.UTF8).ReadToEnd();
            return data;
        }
    }
}
