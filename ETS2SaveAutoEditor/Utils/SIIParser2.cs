using ETS2SaveAutoEditor.Utils;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using System.Web;
using System.Windows;

namespace ETS2SaveAutoEditor.SII2Parser {
    public class SIIParser2 {
        public static readonly byte[] HEADER_ENCRYPTED = [0x53, 0x63, 0x73, 0x43]; // ScsC
        public static readonly byte[] HEADER_BINARY = [0x42, 0x53, 0x49, 0x49]; // BSII
        public static readonly byte[] HEADER_STRING = [0x53, 0x69, 0x69, 0x4e]; // SiiN
        public static readonly byte[] HEADER_UTF8BOM = [0xEF, 0xBB, 0xBF];

        public static SII2 Parse(byte[] data) {
            byte[] header = data[0..4];

            if (header[..3].SequenceEqual(HEADER_UTF8BOM)) {
                data = data[3..];
                header = data[0..4];
            }
            if (header.SequenceEqual(HEADER_ENCRYPTED)) {
                data = SII2ScsCDecryptor.Decrypt(data);
                return Parse(data);
            }
            if (header.SequenceEqual(HEADER_BINARY)) {
                return SII2BSIIDecoder.Decode(data);
            }
            if (header.SequenceEqual(HEADER_STRING)) {
                return SII2SiiNDecoder.Decode(Encoding.UTF8.GetString(data));
            }
            throw new ArgumentException("Unsupported SII format");
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
        private Dictionary<int, BSIIStruct> structures = new();

        private SII2BSIIDecoder(byte[] siiData) {
            // Header is already checked
            data = siiData;
        }

        private int RemainingBytes => data.Length - offset;

        private byte[] ReadN(int n) {
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

        private byte Read1() {
            if (offset >= data.Length) throw new ArgumentException("End of data reached");
            offset += 1;
            return data[offset - 1];
        }

        private string ReadString() {
            int length = ByteEncoder.DecodeInt32(ReadN(4));
            string result = Encoding.UTF8.GetString(ReadN(length));
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
            byte parts = Read1();
            string s = "";
            if (parts == 0) return "null";
            if (parts == 0xFF) { // Special hex token
                ulong value = ByteEncoder.DecodeUInt64(ReadN(8));
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
                ulong value = ByteEncoder.DecodeUInt64(ReadN(8));
                s += GetEncodedString(value);
                if (i < parts - 1) {
                    s += ".";
                }
            }
            return s;
        }

        private string JsonEncodeString(string s) {
            return JsonSerializer.Serialize(s);
        }

        private string EncodeFloat(float f) {
            return SCSSpecialString.EncodeScsFloat(f);
        }

        private void SaveFieldValue(Entity2 unit, BSIIField field) {
            int type = field.type;
            if (type == 0x01) { // string
                unit.Set(field.name, JsonEncodeString(ReadString()));
                return;
            }
            if (type == 0x02) { // string[]
                uint count = ByteEncoder.DecodeUInt32(ReadN(4));
                string[] strings = new string[count];
                for (int i = 0; i < count; i++) {
                    strings[i] = JsonEncodeString(ReadString());
                }
                unit.Set(field.name, strings);
                return;
            }
            if (type == 0x03) { // encoded string
                ulong value = ByteEncoder.DecodeUInt64(ReadN(8));
                var s = GetEncodedString(value);
                if(s == "") s = "\"\""; // Empty string should be '""'
                unit.Set(field.name, s);
                return;
            }
            if (type == 0x04) { // encoded string[]
                uint count = ByteEncoder.DecodeUInt32(ReadN(4));
                string[] strings = new string[count];
                for (int i = 0; i < count; i++) {
                    ulong value = ByteEncoder.DecodeUInt64(ReadN(8));
                    strings[i] = GetEncodedString(value);
                    if(strings[i] == "") strings[i] = "\"\""; // Empty string should be '""'
                }
                unit.Set(field.name, strings);
                return;
            }
            if (type == 0x05) {  // float
                unit.Set(field.name, EncodeFloat(ByteEncoder.DecodeFloat(ReadN(4))));
                return;
            }
            if (type == 0x06) { // float[]
                uint count = ByteEncoder.DecodeUInt32(ReadN(4));
                string[] floats = new string[count];
                for (int i = 0; i < count; i++) {
                    floats[i] = EncodeFloat(ByteEncoder.DecodeFloat(ReadN(4)));
                }
                unit.Set(field.name, floats);
                return;
            }
            if (type == 0x07) { // (float, float)
                float f1 = ByteEncoder.DecodeFloat(ReadN(4));
                float f2 = ByteEncoder.DecodeFloat(ReadN(4));
                unit.Set(field.name, $"({EncodeFloat(f1)}, {EncodeFloat(f2)})");
                return;
            }
            if (type == 0x08) { // (float, float)[]
                uint count = ByteEncoder.DecodeUInt32(ReadN(4));
                string[] floats = new string[count];
                for (int i = 0; i < count; i++) {
                    float f1 = ByteEncoder.DecodeFloat(ReadN(4));
                    float f2 = ByteEncoder.DecodeFloat(ReadN(4));
                    floats[i] = $"({EncodeFloat(f1)}, {EncodeFloat(f2)})";
                }
                unit.Set(field.name, floats);
                return;
            }
            if (type == 0x09) // (float, float, float)
            {
                float f1 = ByteEncoder.DecodeFloat(ReadN(4));
                float f2 = ByteEncoder.DecodeFloat(ReadN(4));
                float f3 = ByteEncoder.DecodeFloat(ReadN(4));
                unit.Set(field.name, $"({EncodeFloat(f1)}, {EncodeFloat(f2)}, {EncodeFloat(f3)})");
                return;
            }
            if (type == 0x0A) { // (float, float, float)[]
                uint count = ByteEncoder.DecodeUInt32(ReadN(4));
                string[] floats = new string[count];
                for (int i = 0; i < count; i++) {
                    float f1 = ByteEncoder.DecodeFloat(ReadN(4));
                    float f2 = ByteEncoder.DecodeFloat(ReadN(4));
                    float f3 = ByteEncoder.DecodeFloat(ReadN(4));
                    floats[i] = $"({EncodeFloat(f1)}, {EncodeFloat(f2)}, {EncodeFloat(f3)})";
                }
                unit.Set(field.name, floats);
                return;
            }
            if (type == 0x11) { // (int, int, int)
                int i1 = ByteEncoder.DecodeInt32(ReadN(4));
                int i2 = ByteEncoder.DecodeInt32(ReadN(4));
                int i3 = ByteEncoder.DecodeInt32(ReadN(4));
                unit.Set(field.name, $"({i1}, {i2}, {i3})");
                return;
            }
            if (type == 0x12) { // (int, int, int)[]
                uint count = ByteEncoder.DecodeUInt32(ReadN(4));
                string[] ints = new string[count];
                for (int i = 0; i < count; i++) {
                    int i1 = ByteEncoder.DecodeInt32(ReadN(4));
                    int i2 = ByteEncoder.DecodeInt32(ReadN(4));
                    int i3 = ByteEncoder.DecodeInt32(ReadN(4));
                    ints[i] = $"({i1}, {i2}, {i3})";
                }
                unit.Set(field.name, ints);
                return;
            }
            if (type == 0x17) { // (float, float, float, float)
                float f1 = ByteEncoder.DecodeFloat(ReadN(4));
                float f2 = ByteEncoder.DecodeFloat(ReadN(4));
                float f3 = ByteEncoder.DecodeFloat(ReadN(4));
                float f4 = ByteEncoder.DecodeFloat(ReadN(4));
                unit.Set(field.name, $"({EncodeFloat(f1)}; {EncodeFloat(f2)}, {EncodeFloat(f3)}, {EncodeFloat(f4)})");
                return;
            }
            if (type == 0x18) { // (float, float, float, float)[]
                uint count = ByteEncoder.DecodeUInt32(ReadN(4));
                string[] floats = new string[count];
                for (int i = 0; i < count; i++) {
                    float f1 = ByteEncoder.DecodeFloat(ReadN(4));
                    float f2 = ByteEncoder.DecodeFloat(ReadN(4));
                    float f3 = ByteEncoder.DecodeFloat(ReadN(4));
                    float f4 = ByteEncoder.DecodeFloat(ReadN(4));
                    floats[i] = $"({EncodeFloat(f1)}; {EncodeFloat(f2)}, {EncodeFloat(f3)}, {EncodeFloat(f4)})";
                }
                unit.Set(field.name, floats);
                return;
            }
            if (type == 0x19) { // float[8] but must be converted to (float * 7)
                float[] f = new float[8];
                for (int i = 0; i < 8; i++) {
                    f[i] = ByteEncoder.DecodeFloat(ReadN(4));
                }
                int t = (int)Math.Floor(f[3]);
                f[0] += ((t & 0xFFF) - 2048) << 9;
                f[2] += (((t >> 12) & 0xFFF) - 2048) << 9;

                unit.Set(field.name, $"({EncodeFloat(f[0])}, {EncodeFloat(f[1])}, {EncodeFloat(f[2])}) ({EncodeFloat(f[4])}; {EncodeFloat(f[5])}, {EncodeFloat(f[6])}, {EncodeFloat(f[7])})");
                return;
            }
            if (type == 0x1A) { // float[8][] but must be converted to (float * 7)[]
                uint count = ByteEncoder.DecodeUInt32(ReadN(4));
                string[] vecs = new string[count];
                for (int i = 0; i < count; i++) {
                    float[] f = new float[8];
                    for (int j = 0; j < 8; j++) {
                        f[j] = ByteEncoder.DecodeFloat(ReadN(4));
                    }
                    int t = (int)Math.Floor(f[3]);
                    f[0] += ((t & 0xFFF) - 2048) << 9;
                    f[2] += (((t >> 12) & 0xFFF) - 2048) << 9;

                    vecs[i] = $"({EncodeFloat(f[0])}, {EncodeFloat(f[1])}, {EncodeFloat(f[2])}) ({EncodeFloat(f[4])}; {EncodeFloat(f[5])}, {EncodeFloat(f[6])}, {EncodeFloat(f[7])})";
                }
                unit.Set(field.name, vecs);
                return;
            }
            if (type == 0x25) { // int32
                unit.Set(field.name, ByteEncoder.DecodeInt32(ReadN(4)) + "");
                return;
            }
            if (type == 0x26) { // int32[]
                uint count = ByteEncoder.DecodeUInt32(ReadN(4));
                string[] ints = new string[count];
                for (int i = 0; i < count; i++) {
                    ints[i] = ByteEncoder.DecodeInt32(ReadN(4)) + "";
                }
                unit.Set(field.name, ints);
                return;
            }
            if (type == 0x27 || type == 0x2F) { // uint32. 0x2f unclear. (only used for cash in game)
                unit.Set(field.name, ByteEncoder.DecodeUInt32(ReadN(4)) + "");
                return;
            }
            if (type == 0x28) { // uint32[]
                uint count = ByteEncoder.DecodeUInt32(ReadN(4));
                string[] ints = new string[count];
                for (int i = 0; i < count; i++) {
                    ints[i] = ByteEncoder.DecodeUInt32(ReadN(4)) + "";
                }
                unit.Set(field.name, ints);
                return;
            }
            if (type == 0x29) { // int16
                unit.Set(field.name, ByteEncoder.DecodeInt16(ReadN(2)) + "");
                return;
            }
            if (type == 0x2A) { // int16[]
                uint count = ByteEncoder.DecodeUInt32(ReadN(4));
                string[] ints = new string[count];
                for (int i = 0; i < count; i++) {
                    ints[i] = ByteEncoder.DecodeInt16(ReadN(2)) + "";
                }
                unit.Set(field.name, ints);
                return;
            }
            if (type == 0x2B) { // uint16
                unit.Set(field.name, ByteEncoder.DecodeUInt16(ReadN(2)) + "");
                return;
            }
            if (type == 0x2C) { // uint16[]
                uint count = ByteEncoder.DecodeUInt32(ReadN(4));
                string[] ints = new string[count];
                for (int i = 0; i < count; i++) {
                    ints[i] = ByteEncoder.DecodeUInt16(ReadN(2)) + "";
                }
                unit.Set(field.name, ints);
                return;
            }
            if (type == 0x31) { // int64
                unit.Set(field.name, ByteEncoder.DecodeInt64(ReadN(8)) + "");
                return;
            }
            if (type == 0x32) { // int64[]
                uint count = ByteEncoder.DecodeUInt32(ReadN(4));
                string[] ints = new string[count];
                for (int i = 0; i < count; i++) {
                    ints[i] = ByteEncoder.DecodeInt64(ReadN(8)) + "";
                }
                unit.Set(field.name, ints);
                return;
            }
            if (type == 0x33) { // uint64
                unit.Set(field.name, ByteEncoder.DecodeUInt64(ReadN(8)) + "");
                return;
            }
            if (type == 0x34) { // uint64[]
                uint count = ByteEncoder.DecodeUInt32(ReadN(4));
                string[] ints = new string[count];
                for (int i = 0; i < count; i++) {
                    ints[i] = ByteEncoder.DecodeUInt64(ReadN(8)) + "";
                }
                unit.Set(field.name, ints);
                return;
            }
            if (type == 0x35) { // bool
                unit.Set(field.name, Read1() > 0 ? "true" : "false");
                return;
            }
            if (type == 0x36) { // bool[]
                uint count = ByteEncoder.DecodeUInt32(ReadN(4));
                string[] bools = new string[count];
                for (int i = 0; i < count; i++) {
                    bools[i] = Read1() > 0 ? "true" : "false";
                }
                unit.Set(field.name, bools);
                return;
            }
            if (type == 0x37) { // Enum
                Dictionary<int, string> enumValues = (Dictionary<int, string>)field.data!;
                int enumValue = ByteEncoder.DecodeInt32(ReadN(4));
                unit.Set(field.name, enumValues[enumValue]);
                return;
            }
            if (type == 0x38) { // Enum[]
                Dictionary<int, string> enumValues = (Dictionary<int, string>)field.data!;
                uint count = ByteEncoder.DecodeUInt32(ReadN(4));
                string[] enums = new string[count];
                for (int i = 0; i < count; i++) {
                    int enumValue = ByteEncoder.DecodeInt32(ReadN(4));
                    enums[i] = enumValues[enumValue];
                }
                unit.Set(field.name, enums);
                return;
            }
            if (type == 0x39 || type == 0x3B || type == 0x3D) { // Three types of pointers, but doesn't matter when converting to string.
                unit.Set(field.name, ReadToken());
                return;
            }
            if (type == 0x3A || type == 0x3C || type == 0x3E) { // Array of pointers.
                uint count = ByteEncoder.DecodeUInt32(ReadN(4));
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
            byte[] header = ReadN(4);
            if (!header.SequenceEqual(SIIParser2.HEADER_BINARY)) {
                throw new ArgumentException("Data is not BSII");
            }

            int version = ByteEncoder.DecodeInt32(ReadN(4));
            if (version != 3) {
                throw new ArgumentException($"Unsupported BSII version. Expected 3, got {version}");
            }

            SII2 sii = [];
            Game2 game = new(sii);

            while (true) { // Read blocks
                int blockType = ByteEncoder.DecodeInt32(ReadN(4));
                byte validity = Read1();

                if (blockType == 0 && validity == 0) {
                    // EOF
                    break;
                }

                if (blockType == 0) { // Definition of a structure
                    int structId = ByteEncoder.DecodeInt32(ReadN(4));
                    string structName = ReadString();

                    BSIIStruct st = new() {
                        name = structName
                    };

                    while (true) { // Read fields
                        int fieldType = ByteEncoder.DecodeInt32(ReadN(4));
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

                            int enumItems = ByteEncoder.DecodeInt32(ReadN(4));
                            for (int i = 0; i < enumItems; i++) {
                                int enumValue = ByteEncoder.DecodeInt32(ReadN(4));
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

            return sii;
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
