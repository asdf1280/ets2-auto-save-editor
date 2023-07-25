using System;
using System.Linq;
using System.Text;

public class Base32768 {
    private static readonly ushort[] charSheet = new ushort[] { 48, 57, 65, 90, 97, 122, 256, 750, 13056, 13310, 13312, 19893, 19968, 40869, 40960, 42182, 44032, 55203, 63744, 64045, 64256, 64511, 65072, 65103, 65136, 65276, 65281, 65439 };

    private static ushort EncodeChar(int c) {
        int count = 0;
        for (int i = 0; i < charSheet.Length; i += 2) {
            int start = charSheet[i];
            int end = charSheet[i + 1];
            count += end - start + 1;

            // we need to return 48(start) + c and
            // count = count_0 + end - start + 1
            // start = end - count + 1
            // start + c = end - count + 1 + c

            if (count > c) {
                return (ushort)(end - count + 1 + c);
            }
        }
        return 0;
    }

    private static int DecodeChar(ushort c) {
        int count = 0;
        for (int i = 0; i < charSheet.Length; i += 2) {
            int start = charSheet[i];
            int end = charSheet[i + 1];

            int rangeSize = end - start + 1;

            if (c >= start && c <= end) {
                return c - start + count;
            }
            count += rangeSize;
        }
        return 0;
    }

    // 32768: 15 bits at once
    // lcm 15, 8 = 
    //
    //
    //
    //                              /                             /                             /
    // 0 0 0 0 0 0 0 0 0 0 0 0 0 0 0 0 0 0 0 0 0 0 0 0 0 0 0 0 0 0 0 0 0 0 0 0 0 0 0 0 0 0 0 0 0 0 0 0 0 0 0 0 0 0 0 0 
    //                /               /               /               /               /               /
    public static string EncodeBase32768(byte[] data) {
        int totalIterations = (int)Math.Ceiling(data.Length * 8 / 15.0);
        int lastBitsToIgnore = totalIterations * 15 - data.Length * 8;
        var sb = new StringBuilder();

        for (int i = 0; i < totalIterations; i++) {
            int v = 0;
            for (int j = 0; j < 15; j++) {
                int currentBit = i * 15 + j;
                int byteAt = currentBit / 8;
                int bitInByte = currentBit % 8;
                bool isOn = false;
                if (byteAt < data.Length) {
                    isOn = ((data[byteAt] >> (7 - bitInByte)) & 1) != 0;
                }
                v += (isOn ? 1 : 0) << (14 - j);
            }
            sb.Append((char)EncodeChar(v));
        }
        sb.Append((char)EncodeChar(lastBitsToIgnore));

        return sb.ToString();
    }

    // Each character in string represents 15-bit integer
    public static byte[] DecodeBase32768(string data) {
        ushort[] characters = (from c in data.ToCharArray() select (ushort)c).ToArray();
        int[] decoded = (from c in characters select DecodeChar(c)).ToArray();
        int lastBitsToIgnore = decoded[characters.Length - 1];
        int byteCount = (characters.Length * 15 - lastBitsToIgnore) / 8;

        byte[] bytes = new byte[byteCount];
        for (int i = 0; i < byteCount; i++) {
            for (int j = 0; j < 8; j++) {
                int currentBit = i * 8 + j;
                int charAt = currentBit / 15;
                int bitInChar = currentBit % 15;
                bool isOn = ((decoded[charAt] >> (14 - bitInChar)) & 1) != 0;
                bytes[i] += (byte)((isOn ? 1 : 0) << (7 - j));
            }
        }
        return bytes;
    }
}