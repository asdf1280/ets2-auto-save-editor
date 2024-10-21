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
using static System.Net.Mime.MediaTypeNames;

namespace ETS2SaveAutoEditor {
    internal enum PositionDataHeader : byte {
        KEY,
        END
    }

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
                    if(BitConverter.IsLittleEndian)
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

    internal class SCSSpecialString {
        public static float ParseScsFloat(string data) {
            if (data.StartsWith("&")) {
                byte[] bytes = new byte[4];
                for (int i = 0; i < 4; i++) {
                    bytes[i] = byte.Parse(data.Substring(i * 2 + 1, 2), System.Globalization.NumberStyles.HexNumber);
                }
                if (BitConverter.IsLittleEndian) // The hex float notation in game save files is stored in big-endian format. We'll reverse them for little-endian systems.
                    Array.Reverse(bytes);
                return BitConverter.ToSingle(bytes, 0);
            } else {
                return float.Parse(data);
            }
        }

        public static string EncodeScsFloat(float value) {
            byte[] bytes = BitConverter.GetBytes(value);
            if (BitConverter.IsLittleEndian) // The hex float notation in game save files is stored in big-endian format.
                Array.Reverse(bytes);
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

        public static string EncodeDecimalPosition(float[] data) {
            if (data.Length != 7) {
                throw new ArgumentException("Invalid data length");
            }

            return $"({data[0]}, {data[1]}, {data[2]}) ({data[3]}; {data[4]}, {data[5]}, {data[6]})";
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
        public static AESEncoder InstanceC = new AESEncoder("Q1*ZlH1k%42^8KzhC*t2yN7NE&ZVGjufTEw3@6wu#%&YUi1i9R", "000000112");

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
            return data;
        }

        public string Decode(string hex) {
            var array = HexEncoder.HexStringToByteArray(hex);

            var decryptor = AES.CreateDecryptor();
            var ms = new MemoryStream(array);
            var cs = new CryptoStream(ms, decryptor, CryptoStreamMode.Read);
            var zs = new DeflateStream(cs, CompressionMode.Decompress);
            var data = new StreamReader(zs, Encoding.UTF8).ReadToEnd();
            zs.Close();
            cs.Close();
            ms.Close();
            return data;
        }

        public byte[] BEncode(byte[] original) {
            var ms = new MemoryStream();
            var zs = new DeflateStream(ms, CompressionLevel.Optimal, false);
            zs.Write(original, 0, original.Length);
            zs.Close();
            return ms.ToArray();
        }

        public byte[] BDecode(byte[] encoded) {
            var ms = new MemoryStream(encoded);
            var zs = new DeflateStream(ms, CompressionMode.Decompress);
            var ms2 = new MemoryStream();
            zs.CopyTo(ms2);
            zs.Close();
            ms.Close();
            return ms2.ToArray();
        }
    }
}
