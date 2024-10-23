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

namespace ETS2SaveAutoEditor.Utils {
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

    public class Compressor {
        public static byte[] Compress(byte[] original) {
            var ms = new MemoryStream();
            var zs = new DeflateStream(ms, CompressionLevel.SmallestSize, false);
            zs.Write(original, 0, original.Length);
            zs.Close();
            return ms.ToArray();
        }

        public static byte[] Decompress(byte[] encoded) {
            var ms = new MemoryStream(encoded);
            var zs = new DeflateStream(ms, CompressionMode.Decompress);
            var ms2 = new MemoryStream();
            zs.CopyTo(ms2);
            zs.Close();
            ms.Close();
            return ms2.ToArray();
        }
    }

    internal class AESEncoder {
        public static AESEncoder InstanceA = new("A42Twypl*H03FV9XFVrjLJATCyxrc2bsE2qUFFvII@&l5WIFmy", "6Jvp0*1a2#ROBzQ1B5L3vIWK4F#spys$Lmcv7q8p!L8zcRfL!p");
        public static AESEncoder InstanceB = new("iU9SVr1mhkH%#I9LaZo4jjIBSl8X5u*cc2O0Ol%tjj4ahTwXr&", "7AaG#ZcoPJ@rF*eaLT!*@S2Zxc357W!6DcUYX63Wo*vRo44cdy");
        public static AESEncoder InstanceC = new("Q1*ZlH1k%42^8KzhC*t2yN7NE&ZVGjufTEw3@6wu#%&YUi1i9R", "000000112");

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

        public byte[] Encode(byte[] original) {
            var encryptor = AES.CreateEncryptor();
            var ms = new MemoryStream();
            var cs = new CryptoStream(ms, encryptor, CryptoStreamMode.Write);
            var zs = new DeflateStream(cs, CompressionLevel.Optimal);
            zs.Write(original);
            zs.Close(); // Deflatestream must be closed to compress the data
            cs.Close();

            return ms.ToArray();
        }

        public string Encode(string original) {
            return HexEncoder.ByteArrayToHexString(Encode(Encoding.UTF8.GetBytes(original)));
        }

        public byte[] Decode(byte[] encoded) {
            var decryptor = AES.CreateDecryptor();
            var ms = new MemoryStream(encoded);
            var cs = new CryptoStream(ms, decryptor, CryptoStreamMode.Read);
            var zs = new DeflateStream(cs, CompressionMode.Decompress);

            var ms2 = new MemoryStream();
            zs.CopyTo(ms2);

            zs.Close();
            return ms2.ToArray();
        }

        public string Decode(string encoded) {
            return Encoding.UTF8.GetString(Decode(HexEncoder.HexStringToByteArray(encoded)));
        }
    }
}
