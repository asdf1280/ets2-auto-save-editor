using ASE.Utils;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

namespace ASE.SII2Parser {
    public class SIIParser2 {
        public static readonly byte[] HEADER_ENCRYPTED = [0x53, 0x63, 0x73, 0x43]; // ScsC
        public static readonly byte[] HEADER_BINARY = [0x42, 0x53, 0x49, 0x49]; // BSII
        public static readonly byte[] HEADER_STRING = [0x53, 0x69, 0x69, 0x4e]; // SiiN
        public static readonly byte[] HEADER_UTF8BOM = [0xEF, 0xBB, 0xBF];

        internal static StreamWriter? verboseLogger = null;

        public static SII2 Parse(byte[] data, bool verbose = false) {
            byte[] header = data[0..4];

            bool isVerboseLowerLayer = false;

            if (verbose && verboseLogger == null) {
                var fileName = "verbose" + DateTime.Now.ToString("yyyyMMdd_HHmmss") + ".log.txt";
                verboseLogger = new StreamWriter(new FileStream(fileName, FileMode.Create, FileAccess.Write));
                verboseLogger.WriteLine("ASE Version " + MainWindow.Version + " - SII2 Parser Bullshit 5\n");
                verboseLogger.Write("Beginning to parse SII file...\n\n");
            } else if (verbose) {
                verboseLogger?.WriteLine("Beginning of another layer of parsing SII file...\n\n");
                isVerboseLowerLayer = true;
            } else {
                verboseLogger = null;
                verboseLogger?.Close();
            }

            try {
                if (header[..3].SequenceEqual(HEADER_UTF8BOM)) {
                    data = data[3..];
                    header = data[0..4];
                    verboseLogger?.WriteLine("UTF-8 BOM detected. Removing it.");
                }

                // Print header as string
                if (verboseLogger == null) {
                    string headerString = Encoding.UTF8.GetString(header);
                    verboseLogger?.WriteLine("Header: " + headerString);
                }

                if (header.SequenceEqual(HEADER_ENCRYPTED)) {
                    data = SII2ScsCDecryptor.Decrypt(data);
                    verboseLogger?.WriteLine("Decrypted SII file. Running 'Parse' again.");
                    return Parse(data, verbose);
                }
                if (header.SequenceEqual(HEADER_BINARY)) {
                    verboseLogger?.WriteLine("Binary SII file detected. Parsing as BSII.");
                    return SII2BSIIDecoder.Decode(data);
                }
                if (header.SequenceEqual(HEADER_STRING)) {
                    verboseLogger?.WriteLine("String SII file detected. Parsing as SiiN.");
                    return SII2SiiNDecoder.Decode(BetterThanStupidMS.UTF8.GetString(data));
                }
                verboseLogger?.WriteLine("Unsupported SII format: " + BitConverter.ToString(header));
                throw new ArgumentException("Unsupported SII format");
            } finally {
                if (!isVerboseLowerLayer) {
                    verboseLogger?.Close();
                    verboseLogger = null;
                } else {
                    verboseLogger?.WriteLine("End of another layer of parsing SII file...\n\n");
                }
            }
        }

        public static bool IsSupported(byte[] data) {
            byte[] header = data[0..4];

            if (header[..3].SequenceEqual(HEADER_UTF8BOM)) {
                data = data[3..];
                header = data[0..4];
            }

            return header.SequenceEqual(HEADER_ENCRYPTED) || header.SequenceEqual(HEADER_BINARY) || header.SequenceEqual(HEADER_STRING);
        }

        public static SII2 Parse(Stream input) {
            if (!input.CanRead) {
                throw new ArgumentException("Stream must be readable");
            }

            MemoryStream memoryStream = new();
            input.CopyTo(memoryStream);

            return Parse(memoryStream.ToArray());
        }

        public static SII2 Parse(string path) {
            using FileStream stream = new(path, FileMode.Open);
            return Parse(stream);
        }
    }

    class SII2ScsCDecryptor {
        private static readonly byte[] SII_AES_KEY = [0x2a, 0x5f, 0xcb, 0x17, 0x91, 0xd2, 0x2f, 0xb6, 0x02, 0x45, 0xb3, 0xd8, 0x36, 0x9e, 0xd0, 0xb2,
                                                      0xc2, 0x73, 0x71, 0x56, 0x3f, 0xbf, 0x1f, 0x3c, 0x9e, 0xdf, 0x6b, 0x11, 0x82, 0x5a, 0x5d, 0x0a];

        public static byte[] Decrypt(byte[] data) {
            if (!data[0..4].SequenceEqual(SIIParser2.HEADER_ENCRYPTED)) {
                throw new ArgumentException("Data is not ScsC");
            }
            if (data.Length <= 4 + 32 + 16) { // 4 bytes header, 32 bytes HMAC, 16 bytes IV
                throw new ArgumentException("Data is too short");
            }

            Aes aes = Aes.Create();
            aes.Key = SII_AES_KEY;
            aes.IV = data[36..52];
            aes.Mode = CipherMode.CBC;
            aes.Padding = PaddingMode.None;

            var decryptor = aes.CreateDecryptor();
            var stream1 = new MemoryStream(data[(4 + 16 + 32 + 4)..]); // Skip 4 bytes of the length of the data
            var stream2 = new CryptoStream(stream1, decryptor, CryptoStreamMode.Read);
            var stream3 = new ZLibStream(stream2, CompressionMode.Decompress);
            var stream = new MemoryStream();
            stream3.CopyTo(stream);

            return stream.ToArray();
        }
    }

    sealed class BSIIField {
        public string name = "";
        public int type = 0;
        public object? data = null;
    }

    sealed class BSIIStruct {
        public string name = "";
        public List<BSIIField> fields = [];
    }

    class SII2BSIIDecoder {
        private static readonly string TOKEN_CHARS = "0123456789abcdefghijklmnopqrstuvwxyz_";
        public static SII2 Decode(byte[] data) {
            if (!data[0..4].SequenceEqual(SIIParser2.HEADER_BINARY)) {
                throw new ArgumentException("Data is not BSII");
            }

            var decoder = new SII2BSIIDecoder(data);
            return decoder.GetParsed();
        }

        private byte[] data;
        private int offset = 0;
        private Dictionary<int, BSIIStruct> structures = [];

        private SII2BSIIDecoder(byte[] siiData) {
            // Header is already checked
            data = siiData;
        }

        private int RemainingBytes => data.Length - offset;

        private byte[] NextBuffer(int n) {
            if (n < 0) {
                throw new ArgumentException("Invalid read length");
            }
            if (offset + n > data.Length) {
                throw new ArgumentException("End of data reached");
            }
            byte[] result = data[offset..(offset + n)];
            offset += n;
            return result;
        }

        private byte NextBuffer() {
            if (offset >= data.Length) throw new ArgumentException("End of data reached");
            offset += 1;
            return data[offset - 1];
        }

        private string ReadString() {
            int length = ByteEncoder.DecodeInt32(NextBuffer(4));
            string result = Encoding.UTF8.GetString(NextBuffer(length));
            return result;
        }

        private string GetEncodedString(ulong value) {
            string s = "";
            while (value % (ulong)(TOKEN_CHARS.Length + 1) > 0) {
                int ci = (int)(value % (ulong)(TOKEN_CHARS.Length + 1)) - 1;
                value /= (ulong)(TOKEN_CHARS.Length + 1);

                s += TOKEN_CHARS[ci];
            }
            return s;
        }

        private string ReadToken() {
            byte parts = NextBuffer();
            string s = "";
            if (parts == 0) return "null";
            if (parts == 0xFF) { // Special hex token
                ulong value = ByteEncoder.DecodeUInt64(NextBuffer(8));
                do {
                    bool isLast = (value & ~(ulong)0xFFFF) == 0;
                    int part = (int)(value & 0xFFFF);

                    if (isLast) {
                        s = $".{part:x}" + s;
                    } else {  // 4 characters hex
                        s = $".{part:x4}" + s;
                    }

                    value >>= 16;
                } while (value > 0);

                return "_nameless" + s;
            }

            // Encoded strings separated by dots
            for (int i = 0; i < parts; i++) {
                ulong value = ByteEncoder.DecodeUInt64(NextBuffer(8));
                s += GetEncodedString(value);
                if (i < parts - 1) {
                    s += ".";
                }
            }
            return s;
        }

        private string ReadFloat2() {
            float f1 = ByteEncoder.DecodeFloat(NextBuffer(4));
            float f2 = ByteEncoder.DecodeFloat(NextBuffer(4));
            return $"({EncodeFloat(f1)}, {EncodeFloat(f2)})";
        }

        private string ReadFloat3() {
            float f1 = ByteEncoder.DecodeFloat(NextBuffer(4));
            float f2 = ByteEncoder.DecodeFloat(NextBuffer(4));
            float f3 = ByteEncoder.DecodeFloat(NextBuffer(4));
            return $"({EncodeFloat(f1)}, {EncodeFloat(f2)}, {EncodeFloat(f3)})";
        }

        private string ReadFloat4() {
            float f1 = ByteEncoder.DecodeFloat(NextBuffer(4));
            float f2 = ByteEncoder.DecodeFloat(NextBuffer(4));
            float f3 = ByteEncoder.DecodeFloat(NextBuffer(4));
            float f4 = ByteEncoder.DecodeFloat(NextBuffer(4));
            return $"({EncodeFloat(f1)}; {EncodeFloat(f2)}, {EncodeFloat(f3)}, {EncodeFloat(f4)})";
        }

        private string ReadFloat8() {
            float[] fs = new float[8];
            for (int i = 0; i < 8; i++) {
                fs[i] = ByteEncoder.DecodeFloat(NextBuffer(4));
            }
            int t = (int)Math.Floor(fs[3]);
            fs[0] += ((t & 0xFFF) - 2048) << 9;
            fs[2] += (((t >> 12) & 0xFFF) - 2048) << 9;
            return $"({EncodeFloat(fs[0])}, {EncodeFloat(fs[1])}, {EncodeFloat(fs[2])}) ({EncodeFloat(fs[4])}; {EncodeFloat(fs[5])}, {EncodeFloat(fs[6])}, {EncodeFloat(fs[7])})";
        }

        private string JsonEncodeString(string s) {
            byte[] utf8Bytes = Encoding.UTF8.GetBytes(s);
            StringBuilder stringBuilder = new();

            foreach (byte b in utf8Bytes) {
                if (b == (byte)'"') {
                    // Escape double-quote as \"
                    stringBuilder.Append("\\\"");
                } else if (b >= 32 && b <= 126) {
                    // Safe ASCII character, directly append to the string
                    stringBuilder.Append((char)b);
                } else {
                    // Unsafe character, escape in \x hex format
                    stringBuilder.Append("\\x");
                    stringBuilder.Append(b.ToString("x2"));
                }
            }

            return "\"" + stringBuilder.ToString() + "\"";
        }

        private string EncodeFloat(float f) {
            return SCSSpecialString.EncodeScsFloat(f);
            //return f.ToString("R"); // Debugging
        }

        private void SaveFieldValue(Entity2 unit, BSIIField field) {
            int type = field.type;
            if (type == 0x01) { // string
                unit.Set(field.name, JsonEncodeString(ReadString()));
                return;
            }
            if (type == 0x02) { // string[]
                uint count = ByteEncoder.DecodeUInt32(NextBuffer(4));
                string[] strings = new string[count];
                for (int i = 0; i < count; i++) {
                    strings[i] = JsonEncodeString(ReadString());
                }
                unit.Set(field.name, strings);
                return;
            }
            if (type == 0x03) { // token
                ulong value = ByteEncoder.DecodeUInt64(NextBuffer(8));
                var s = GetEncodedString(value);
                if (s == "") s = "\"\""; // Empty string should be '""'
                unit.Set(field.name, s);
                return;
            }
            if (type == 0x04) { // token[]
                uint count = ByteEncoder.DecodeUInt32(NextBuffer(4));
                string[] strings = new string[count];
                for (int i = 0; i < count; i++) {
                    ulong value = ByteEncoder.DecodeUInt64(NextBuffer(8));
                    strings[i] = GetEncodedString(value);
                    if (strings[i] == "") strings[i] = "\"\""; // Empty string should be '""'
                }
                unit.Set(field.name, strings);
                return;
            }
            if (type == 0x05) { // float
                unit.Set(field.name, EncodeFloat(ByteEncoder.DecodeFloat(NextBuffer(4))));
                return;
            }
            if (type == 0x06) { // float[]
                uint count = ByteEncoder.DecodeUInt32(NextBuffer(4));
                string[] floats = new string[count];
                for (int i = 0; i < count; i++) {
                    floats[i] = EncodeFloat(ByteEncoder.DecodeFloat(NextBuffer(4)));
                }
                unit.Set(field.name, floats);
                return;
            }
            if (type == 0x07) { // float2 (x, y)
                unit.Set(field.name, ReadFloat2());
                return;
            }
            if (type == 0x08) { // float2[]
                uint count = ByteEncoder.DecodeUInt32(NextBuffer(4));
                string[] floats = new string[count];
                for (int i = 0; i < count; i++)
                    floats[i] = ReadFloat2();
                unit.Set(field.name, floats);
                return;
            }
            if (type == 0x09) // float3 (x, y, z)
            {
                unit.Set(field.name, ReadFloat3());
                return;
            }
            if (type == 0x0A) { // float3[]
                uint count = ByteEncoder.DecodeUInt32(NextBuffer(4));
                string[] floats = new string[count];
                for (int i = 0; i < count; i++) {
                    floats[i] = ReadFloat3();
                }
                unit.Set(field.name, floats);
                return;
            }
            if (type == 0x0B) // float4 (x, y, z, w)
            {
                unit.Set(field.name, ReadFloat4());
                return;
            }
            if (type == 0x0C) { // float4[]
                uint count = ByteEncoder.DecodeUInt32(NextBuffer(4));
                string[] floats = new string[count];
                for (int i = 0; i < count; i++) {
                    floats[i] = ReadFloat4();
                }
                unit.Set(field.name, floats);
                return;
            }
            if (type == 0x0D) { // fixed (int)
                unit.Set(field.name, ByteEncoder.DecodeInt32(NextBuffer(4)) + "");
                return;
            }
            if (type == 0x0E) { // fixed[]
                uint count = ByteEncoder.DecodeUInt32(NextBuffer(4));
                string[] ints = new string[count];
                for (int i = 0; i < count; i++) {
                    ints[i] = ByteEncoder.DecodeInt32(NextBuffer(4)) + "";
                }
                unit.Set(field.name, ints);
                return;
            }
            if (type == 0x0F) { // fixed2 (int) (x, y)
                int i1 = ByteEncoder.DecodeInt32(NextBuffer(4));
                int i2 = ByteEncoder.DecodeInt32(NextBuffer(4));
                unit.Set(field.name, $"({i1}, {i2})");
                return;
            }
            if (type == 0x10) { // fixed2[]
                uint count = ByteEncoder.DecodeUInt32(NextBuffer(4));
                string[] ints = new string[count];
                for (int i = 0; i < count; i++) {
                    int i1 = ByteEncoder.DecodeInt32(NextBuffer(4));
                    int i2 = ByteEncoder.DecodeInt32(NextBuffer(4));
                    ints[i] = $"({i1}, {i2})";
                }
                unit.Set(field.name, ints);
                return;
            }
            if (type == 0x11) { // fixed3 (int) (x, y, z)
                int i1 = ByteEncoder.DecodeInt32(NextBuffer(4));
                int i2 = ByteEncoder.DecodeInt32(NextBuffer(4));
                int i3 = ByteEncoder.DecodeInt32(NextBuffer(4));
                unit.Set(field.name, $"({i1}, {i2}, {i3})");
                return;
            }
            if (type == 0x12) { // fixed3[]
                uint count = ByteEncoder.DecodeUInt32(NextBuffer(4));
                string[] ints = new string[count];
                for (int i = 0; i < count; i++) {
                    int i1 = ByteEncoder.DecodeInt32(NextBuffer(4));
                    int i2 = ByteEncoder.DecodeInt32(NextBuffer(4));
                    int i3 = ByteEncoder.DecodeInt32(NextBuffer(4));
                    ints[i] = $"({i1}, {i2}, {i3})";
                }
                unit.Set(field.name, ints);
                return;
            }
            if (type == 0x13) { // fixed4 (int) (x, y, z, w)
                int i1 = ByteEncoder.DecodeInt32(NextBuffer(4));
                int i2 = ByteEncoder.DecodeInt32(NextBuffer(4));
                int i3 = ByteEncoder.DecodeInt32(NextBuffer(4));
                int i4 = ByteEncoder.DecodeInt32(NextBuffer(4));
                unit.Set(field.name, $"({i1}; {i2}, {i3}, {i4})");
                return;
            }
            if (type == 0x14) { // fixed4[]
                uint count = ByteEncoder.DecodeUInt32(NextBuffer(4));
                string[] ints = new string[count];
                for (int i = 0; i < count; i++) {
                    int i1 = ByteEncoder.DecodeInt32(NextBuffer(4));
                    int i2 = ByteEncoder.DecodeInt32(NextBuffer(4));
                    int i3 = ByteEncoder.DecodeInt32(NextBuffer(4));
                    int i4 = ByteEncoder.DecodeInt32(NextBuffer(4));
                    ints[i] = $"({i1}; {i2}, {i3}, {i4})";
                }
                unit.Set(field.name, ints);
                return;
            }
            if (type == 0x15 || type == 0x41) { // int2 (x, y)
                int i1 = ByteEncoder.DecodeInt32(NextBuffer(4));
                int i2 = ByteEncoder.DecodeInt32(NextBuffer(4));
                unit.Set(field.name, $"({i1}, {i2})");
                return;
            }
            if (type == 0x16 || type == 0x42) { // int2[]
                uint count = ByteEncoder.DecodeUInt32(NextBuffer(4));
                string[] ints = new string[count];
                for (int i = 0; i < count; i++) {
                    int i1 = ByteEncoder.DecodeInt32(NextBuffer(4));
                    int i2 = ByteEncoder.DecodeInt32(NextBuffer(4));
                    ints[i] = $"({i1}, {i2})";
                }
                unit.Set(field.name, ints);
                return;
            }
            if (type == 0x17) { // quaternion (w, x, y, z) - basically float4 but with different meaning
                unit.Set(field.name, ReadFloat4());
                return;
            }
            if (type == 0x18) { // quaternion[]
                uint count = ByteEncoder.DecodeUInt32(NextBuffer(4));
                string[] s = new string[count];
                for (int i = 0; i < count; i++) {
                    s[i] = ReadFloat4();
                }
                unit.Set(field.name, s);
                return;
            }
            if (type == 0x19) { // placement (float[8]) needs conversion to float[7] (x, y, z) (w; x, y, z)
                unit.Set(field.name, ReadFloat8());
                return;
            }
            if (type == 0x1A) { // placement[]
                uint count = ByteEncoder.DecodeUInt32(NextBuffer(4));
                string[] s = new string[count];
                for (int i = 0; i < count; i++) {
                    s[i] = ReadFloat8();
                }
                unit.Set(field.name, s);
                return;
            }
            if (type == 0x25) { // s32 (int)
                unit.Set(field.name, ByteEncoder.DecodeInt32(NextBuffer(4)) + "");
                return;
            }
            if (type == 0x26) { // s32[]
                uint count = ByteEncoder.DecodeUInt32(NextBuffer(4));
                string[] ints = new string[count];
                for (int i = 0; i < count; i++) {
                    ints[i] = ByteEncoder.DecodeInt32(NextBuffer(4)) + "";
                }
                unit.Set(field.name, ints);
                return;
            }
            if (type == 0x27 || type == 0x2F) { // u32 (uint). 0x2f is unclear. (only used for cash in game)
                unit.Set(field.name, ByteEncoder.DecodeUInt32(NextBuffer(4)) + "");
                return;
            }
            if (type == 0x28) { // u32[]
                uint count = ByteEncoder.DecodeUInt32(NextBuffer(4));
                string[] ints = new string[count];
                for (int i = 0; i < count; i++) {
                    ints[i] = ByteEncoder.DecodeUInt32(NextBuffer(4)) + "";
                }
                unit.Set(field.name, ints);
                return;
            }
            if (type == 0x29) { // s16 (short)
                unit.Set(field.name, ByteEncoder.DecodeInt16(NextBuffer(2)) + "");
                return;
            }
            if (type == 0x2A) { // s16[]
                uint count = ByteEncoder.DecodeUInt32(NextBuffer(4));
                string[] ints = new string[count];
                for (int i = 0; i < count; i++) {
                    ints[i] = ByteEncoder.DecodeInt16(NextBuffer(2)) + "";
                }
                unit.Set(field.name, ints);
                return;
            }
            if (type == 0x2B) { // u16 (ushort)
                unit.Set(field.name, ByteEncoder.DecodeUInt16(NextBuffer(2)) + "");
                return;
            }
            if (type == 0x2C) { // u16[]
                uint count = ByteEncoder.DecodeUInt32(NextBuffer(4));
                string[] ints = new string[count];
                for (int i = 0; i < count; i++) {
                    ints[i] = ByteEncoder.DecodeUInt16(NextBuffer(2)) + "";
                }
                unit.Set(field.name, ints);
                return;
            }
            if (type == 0x31) { // s64 (long)
                unit.Set(field.name, ByteEncoder.DecodeInt64(NextBuffer(8)) + "");
                return;
            }
            if (type == 0x32) { // s64[]
                uint count = ByteEncoder.DecodeUInt32(NextBuffer(4));
                string[] ints = new string[count];
                for (int i = 0; i < count; i++) {
                    ints[i] = ByteEncoder.DecodeInt64(NextBuffer(8)) + "";
                }
                unit.Set(field.name, ints);
                return;
            }
            if (type == 0x33) { // u64 (ulong)
                unit.Set(field.name, ByteEncoder.DecodeUInt64(NextBuffer(8)) + "");
                return;
            }
            if (type == 0x34) { // u64[]
                uint count = ByteEncoder.DecodeUInt32(NextBuffer(4));
                string[] ints = new string[count];
                for (int i = 0; i < count; i++) {
                    ints[i] = ByteEncoder.DecodeUInt64(NextBuffer(8)) + "";
                }
                unit.Set(field.name, ints);
                return;
            }
            if (type == 0x35) { // bool
                unit.Set(field.name, NextBuffer() > 0 ? "true" : "false");
                return;
            }
            if (type == 0x36) { // bool[]
                uint count = ByteEncoder.DecodeUInt32(NextBuffer(4));
                string[] bools = new string[count];
                for (int i = 0; i < count; i++) {
                    bools[i] = NextBuffer() > 0 ? "true" : "false";
                }
                unit.Set(field.name, bools);
                return;
            }
            if (type == 0x37) { // enum
                Dictionary<int, string> enumValues = (Dictionary<int, string>)field.data!;
                int enumValue = ByteEncoder.DecodeInt32(NextBuffer(4));
                unit.Set(field.name, enumValues[enumValue]);
                return;
            }
            if (type == 0x38) { // enum[]
                Dictionary<int, string> enumValues = (Dictionary<int, string>)field.data!;
                uint count = ByteEncoder.DecodeUInt32(NextBuffer(4));
                string[] enums = new string[count];
                for (int i = 0; i < count; i++) {
                    int enumValue = ByteEncoder.DecodeInt32(NextBuffer(4));
                    enums[i] = enumValues[enumValue];
                }
                unit.Set(field.name, enums);
                return;
            }
            if (type == 0x39 || type == 0x3B || type == 0x3D) { // Three types of pointers, owner_ptr, inner_ptr, link_ptr. doesn't matter when converting to string.
                unit.Set(field.name, ReadToken());
                return;
            }
            if (type == 0x3A || type == 0x3C || type == 0x3E) { // Array of pointers.
                uint count = ByteEncoder.DecodeUInt32(NextBuffer(4));
                string[] arr = new string[count];
                for (int i = 0; i < count; i++) {
                    arr[i] = ReadToken();
                }
                unit.Set(field.name, arr);
                return;
            }

            // No type matched. Dump and throw.
            // Dump from -32 bytes from now to the end of the data to dump.dat
            // Then throw an exception.
            int offsetStart = Math.Max(0, offset - 32);
            File.WriteAllBytes("dump.dat", data[offsetStart..]);

            MemoryStream memoryStream = new();
            StreamWriter streamWriter = new(memoryStream, Encoding.UTF8);
            unit.Unit.WriteTo(streamWriter);
            streamWriter.Close();

            File.WriteAllText("dump.txt", $"Dumped from offset {offsetStart} to the end of the data to dump.dat.\n\nUnknown field type 0x{field.type:x2} of {unit.Type}:{field.name} at offset {offset}.\n\nUnit information:\n\n" + Encoding.UTF8.GetString(memoryStream.ToArray()));
            throw new ArgumentException($"Unknown field type 0x{field.type:x2} of {unit.Type}:{field.name} at offset {offset}. Dumped since ERROR-32 bytes to dump.dat");
        }

        private SII2 GetParsed() { // This function is to be called only once in the lifetime of the object. Since this is private, we can assume that it's safe.
            byte[] header = NextBuffer(4);
            if (!header.SequenceEqual(SIIParser2.HEADER_BINARY)) {
                throw new ArgumentException("Data is not BSII");
            }

            int version = ByteEncoder.DecodeInt32(NextBuffer(4));
            if (version != 3) {
                throw new ArgumentException($"Unsupported BSII version. Expected 3, got {version}");
            }

            SII2 sii = [];
            Game2 game = new(sii);

            while (true) { // Read blocks
                int blockType = ByteEncoder.DecodeInt32(NextBuffer(4));
                byte validity = NextBuffer();

                if (blockType == 0 && validity == 0) {
                    // EOF
                    break;
                }

                if (blockType == 0) { // Definition of a structure
                    int structId = ByteEncoder.DecodeInt32(NextBuffer(4));
                    string structName = ReadString();

                    BSIIStruct st = new() {
                        name = structName
                    };

                    while (true) { // Read fields
                        int fieldType = ByteEncoder.DecodeInt32(NextBuffer(4));
                        if (fieldType == 0) { // End of structure definition
                            break;
                        }

                        string fieldName = ReadString();

                        BSIIField field = new() {
                            name = fieldName,
                            type = fieldType
                        };

                        if (fieldType == 0x37 || fieldType == 0x38) { // Special type (enum)
                            Dictionary<int, string> enumValues = [];

                            int enumItems = ByteEncoder.DecodeInt32(NextBuffer(4));
                            for (int i = 0; i < enumItems; i++) {
                                int enumValue = ByteEncoder.DecodeInt32(NextBuffer(4));
                                string enumKeyword = ReadString(); // Enum name

                                enumValues[enumValue] = enumKeyword;
                            }

                            field.data = enumValues;
                        }

                        st.fields.Add(field);
                    }

                    structures[structId] = st;
                    continue;
                }

                // Data block
                offset -= 1; // Rewind the validity byte because in data block it's header of token.
                BSIIStruct structDef = structures[blockType];
                Entity2 unit = game.CreateNewUnit(structDef.name, ReadToken());

                foreach (var field in structDef.fields) {
                    SaveFieldValue(unit, field);
                }
            }

            sii.StructureData = structures;

            return sii;
        }
    }

    class BSIIStructureDumper {
        public static void WriteStructureDataTo(TextWriter w, Dictionary<int, BSIIStruct> structures) {
            w.Write("// This file was generated by ASE for save editors. Refer to this file to understand what value fits in which field of savegame.\n\n");
            w.Write("// Note that this file is only generated when ASE decodes a binary SII save.\n\n");
            w.Write("// Some of types are documented in https://modding.scssoft.com/wiki/Documentation/Engine/Units\n\n");

            foreach (var structId in structures.Keys) {
                BSIIStruct structDef = structures[structId];
                w.Write($"STRUCTURE  0x{structId:x2}  {structDef.name}\n");
                w.Write($"    {"NAME",-40}  TYPE_ID  TYPE\n");
                foreach (var field in structDef.fields) {
                    w.Write($"    {field.name,-40}  0x{field.type:x2}     {StringifyType(field)}\n");
                }
                w.Write("\n");
            }

            w.Close();
        }

        private static string StringifyType(BSIIField field) {
            int type = field.type;
            if (type == 0x00) {
                return "Invalid";
            }
            if (type == 0x01) {
                return "string";
            }
            if (type == 0x02) {
                return "string[]";
            }
            if (type == 0x03) {
                return "token"; // 0,1,2,3,4,5,6,7,8,9,a,b,c,d,e,f,g,h,i,j,k,l,m,n,o,p,q,r,s,t,u,v,w,x,y,z,_
            }
            if (type == 0x04) {
                return "token[]";
            }
            if (type == 0x05) {
                return "float";
            }
            if (type == 0x06) {
                return "float[]";
            }
            if (type == 0x07) {
                return "float2";
            }
            if (type == 0x08) {
                return "float2[]";
            }
            if (type == 0x09) {
                return "float3";
            }
            if (type == 0x0A) {
                return "float3[]";
            }
            if (type == 0x0B) {
                return "float4";
            }
            if (type == 0x0C) {
                return "float4[]";
            }
            if (type == 0x0D) {
                return "fixed";
            }
            if (type == 0x0E) {
                return "fixed[]";
            }
            if (type == 0x0F) {
                return "fixed2";
            }
            if (type == 0x10) {
                return "fixed2[]";
            }
            if (type == 0x11) {
                return "fixed3";
            }
            if (type == 0x12) {
                return "fixed3[]";
            }
            if (type == 0x13) {
                return "fixed4";
            }
            if (type == 0x14) {
                return "fixed4[]";
            }
            if (type == 0x15) {
                return "int2";
            }
            if (type == 0x16) {
                return "int2[]";
            }
            if (type == 0x17) {
                return "quaternion";
            }
            if (type == 0x18) {
                return "quaternion[]";
            }
            if (type == 0x19) {
                return "placement";
            }
            if (type == 0x1A) {
                return "placement[]";
            }
            if (type == 0x25) {
                return "s32";
            }
            if (type == 0x26) {
                return "s32[]";
            }
            if (type == 0x27) {
                return "u32";
            }
            if (type == 0x28) {
                return "u32[]";
            }
            if (type == 0x29) {
                return "s16";
            }
            if (type == 0x2A) {
                return "s16[]";
            }
            if (type == 0x2B) {
                return "u16";
            }
            if (type == 0x2C) {
                return "u16[]";
            }
            if (type == 0x2D) {
                return "unknown";
            }
            if (type == 0x2E) {
                return "unknown[]";
            }
            if (type == 0x2F) {
                return "uint";
            }
            if (type == 0x30) {
                return "uint[]";
            }
            if (type == 0x31) {
                return "s64";
            }
            if (type == 0x32) {
                return "s64[]";
            }
            if (type == 0x33) {
                return "u64";
            }
            if (type == 0x34) {
                return "u64[]";
            }
            if (type == 0x35) {
                return "bool";
            }
            if (type == 0x36) {
                return "bool[]";
            }
            if (type == 0x37) {
                string s = "";
                Dictionary<int, string> data = (Dictionary<int, string>)field.data!;

                foreach (int enumValue in data.Keys) {
                    string enumName = data[enumValue];

                    s += $"{enumName}={enumValue}, ";
                }

                s = s[..^2];

                return "enum<" + s + ">";
            }
            if (type == 0x38) {
                string s = "";
                Dictionary<int, string> data = (Dictionary<int, string>)field.data!;

                foreach (int enumValue in data.Keys) {
                    string enumName = data[enumValue];

                    s += $"{enumName}={enumValue}, ";
                }

                s = s[..^2];

                return "enum<" + s + ">[]";
            }
            if (type == 0x39) {
                return "owner_ptr";
            }
            if (type == 0x3A) {
                return "owner_ptr[]";
            }
            if (type == 0x3B) {
                return "inner_ptr";
            }
            if (type == 0x3C) {
                return "inner_ptr[]";
            }
            if (type == 0x3D) {
                return "link_ptr";
            }
            if (type == 0x3E) {
                return "link_ptr[]";
            }
            if (type == 0x41) {
                return "int2 (id 0x41)";
            }
            if (type == 0x42) {
                return "int2[] (id 0x42)";
            }
            return "Unknown";
        }
    }

    enum SIIOpenStage {
        NotOpened,
        MagicNumberRead,
        Ready,
        Unit,
        Finished
    }

    class SII2SiiNDecoder {
        public static SII2 Decode(string data) {
            SIIParser2.verboseLogger?.WriteLine("The whole SII file is\n=========================================");
            SIIParser2.verboseLogger?.WriteLine(data);
            SIIParser2.verboseLogger?.WriteLine("=========================================");
            IEnumerable<string> lines = data.Replace("\r", "").Split('\n');

            SIIOpenStage siiOpenStage = SIIOpenStage.NotOpened;
            Unit2? currentUnit = null;
            string? currentKey = null;
            Value2? currentValue = null;

            Stopwatch stopwatch = new();
            stopwatch.Start();

            //var p = new Regex(@"^([a-z_]+) : ([_\-\.a-zA-Z0-9]+) {$", RegexOptions.Compiled);
            //var p1 = new Regex(@"^(.*?)(\[\d*\])?: (.*)", RegexOptions.Compiled); // Regex.Match(line, $"^\\s+{name}\\[\\d*\\]: (.*)$");

            SII2 sii = [];

            int ln = 0;
            foreach (string s in from a in lines select a.Trim() into b where b.Length > 0 select b) {
                SIIParser2.verboseLogger?.WriteLine($"Parsing line {ln}: {s} / Stage {siiOpenStage} / Current unit: {currentUnit?.Type} / Current key: {currentKey} / Current value: {currentValue}");
                ++ln;
                switch (siiOpenStage) {
                    case SIIOpenStage.NotOpened:
                        if (s != "SiiNunit") {
                            throw new ArgumentException("The file is not a valid SII file at line " + ln + ".");
                        }
                        siiOpenStage = SIIOpenStage.MagicNumberRead;
                        break;
                    case SIIOpenStage.MagicNumberRead:
                        if (s != "{") {
                            throw new ArgumentException("The file is not a valid SII file at line " + ln + ".");
                        }
                        siiOpenStage = SIIOpenStage.Ready;
                        break;
                    case SIIOpenStage.Ready:
                        if (s == "}") {
                            siiOpenStage = SIIOpenStage.Finished;
                            break;
                        }
                        //var match = p.Match(s);
                        //if (!match.Success) {
                        //    throw new ArgumentException("The file is not a valid SII file at line " + ln + ".");
                        //}
                        // ^^ While regex is perfect for parsing, it's not the best for performance. We need to use the old-school way of parsing.

                        string type;
                        string id; {
                            int sepIndex = s.IndexOf(" : ");
                            int braceIndex = s.IndexOf(" {");
                            SIIParser2.verboseLogger?.WriteLine($"sepIndex: {sepIndex} / braceIndex: {braceIndex}");
                            if (sepIndex == -1 || braceIndex == -1) throw new ArgumentException("The file is not a valid SII file at line " + ln + ".");
                            type = s[..sepIndex];
                            id = s[(sepIndex + 3)..braceIndex];
                        }

                        currentUnit = new(type, id);
                        currentKey = null;
                        currentValue = null;
                        siiOpenStage = SIIOpenStage.Unit;
                        break;
                    case SIIOpenStage.Unit:
                        if (s == "}") { // The unit is closed.
                                        // Save previous entry into the currentUnit
                            if (currentKey is not null) // Empty unit
                                currentUnit![currentKey] = currentValue!;

                            sii.UncheckedAdd(currentUnit!); // Quick add without ID duplication check. This can save 90% of time by reducing the number of checks. We assume that the SII file is valid.
                            siiOpenStage = SIIOpenStage.Ready;
                            break;
                        }
                        //var match = p1.Match(s);
                        //if (!match.Success) {
                        //    throw new ArgumentException("The file is not a valid SII file at line " + ln + ".");
                        //}
                        //string key = match.Groups[1].Value;
                        //bool isArrayElement = match.Groups[2].Value.Length > 0;
                        //string value = match.Groups[3].Value;
                        // ^^ While regex is perfect for parsing, it's not the best for performance. We need to use the old-school way of parsing.
                        string key;
                        bool isArrayElement = false;
                        string value; {
                            int i = s.IndexOf(':');
                            SIIParser2.verboseLogger?.WriteLine($"i: {i}");
                            if (i == -1) {
                                throw new ArgumentException("The file is not a valid SII file at line " + ln + ".");
                            }
                            key = s[..i];
                            if (key.EndsWith(']')) {
                                isArrayElement = true;
                                key = key[..key.IndexOf('[')];
                            }
                            value = s[(i + 1)..].Trim();
                        }

                        if (key != currentKey) {
                            // Save previous entry into the currentUnit
                            if (currentKey is not null)
                                currentUnit![currentKey] = currentValue!;

                            // Create a new entry
                            currentKey = key;
                            if (isArrayElement)
                                currentValue = new RawDataArrayValue2([value]);
                            else
                                currentValue = new RawDataValue2(value);
                        } else if (isArrayElement) {
                            if (key == currentKey) { // Append to the current array. Throw if the key doesn't match
                                if (currentValue is not RawDataArrayValue2) // Current value is size of the array.
                                    currentValue = new RawDataArrayValue2();

                                ((RawDataArrayValue2)currentValue).TypedValue.Add(value); // Append to the array.
                            }
                        } else { // Throw. Multiple non-array elements with the same key.
                            throw new ArgumentException("The file is not a valid SII file at line " + ln + ".");
                        }
                        break;
                }
            }

            Console.WriteLine($"[SII2] Parsed SII with {sii.Count} units. Elapsed time: {stopwatch.ElapsedMilliseconds} ms");
            return sii;
        }
    }
}
