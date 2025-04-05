using System;
using System.Linq;
using System.Text;

namespace ASE.Utils {

    /// <summary>
    /// The <c>Base32768</c> class provides a custom encoding mechanism designed to encode binary data into a highly compact string format 
    /// using 32,768 unique characters. This encoding method takes advantage of the large range of Unicode characters to represent 
    /// data more efficiently compared to traditional Base64 or Base32 encodings, which use fewer characters.
    ///
    /// <para>
    /// Each character in Base32768 encoding represents a 15-bit integer value. This allows for more compact representations of binary 
    /// data, as it encodes more bits per character than common encoding schemes like Base64 (which encodes 6 bits per character). The 
    /// range of Unicode characters used for encoding is carefully chosen to avoid control characters and other characters that may 
    /// interfere with text encoding and display.
    /// </para>
    ///
    /// <para>
    /// The class provides two primary methods:
    /// <list type="bullet">
    ///   <item>
    ///     <term><see cref="EncodeBase32768(byte[])"/></term>
    ///     <description>Encodes a byte array into a Base32768-encoded string. Each 15 bits of data are encoded as a single Unicode character.</description>
    ///   </item>
    ///   <item>
    ///     <term><see cref="DecodeBase32768(string)"/></term>
    ///     <description>Decodes a Base32768-encoded string back into the original byte array.</description>
    ///   </item>
    /// </list>
    /// </para>
    ///
    /// <h3>Use Case</h3>
    /// <para>
    /// This encoding is especially useful for transmitting binary data in environments where size efficiency is critical and where 
    /// non-ASCII characters can be safely used, such as in QR codes, specialized storage formats, or other non-standard communication channels. 
    /// Since each character represents 15 bits, it is more efficient than encoding schemes that represent fewer bits per character.
    /// </para>
    ///
    /// <h3>Encoding Process</h3>
    /// <para>
    /// During the encoding process, the binary data is broken into chunks of 15 bits. Each chunk is then mapped to a unique Unicode character 
    /// from a predefined set of characters (specified in the internal <c>charSheet</c> array). The last chunk may contain fewer than 15 bits, 
    /// in which case the number of unused bits is appended as the final character in the encoded string, allowing for exact reconstruction 
    /// of the original data during decoding.
    /// </para>
    ///
    /// <h3>Decoding Process</h3>
    /// <para>
    /// During decoding, each character in the encoded string is converted back into a 15-bit value using the reverse mapping from the 
    /// <c>charSheet</c>. The method ensures that the original byte array is reconstructed accurately, taking into account any unused bits 
    /// indicated by the final character in the encoded string.
    /// </para>
    ///
    /// <h3>Performance Considerations</h3>
    /// <para>
    /// Base32768 is designed for scenarios where space efficiency and compactness are more critical than raw encoding/decoding speed. 
    /// It trades off some performance due to the larger number of bits per character and the use of a non-trivial character set, but 
    /// the resulting string is typically much shorter than if traditional encoding schemes were used.
    /// </para>
    ///
    /// <h3>Limitations</h3>
    /// <para>
    /// While Base32768 encoding is highly efficient in terms of string length, its reliance on a broad range of Unicode characters may 
    /// make it unsuitable for text environments that restrict characters to ASCII or specific encodings. It is also important to ensure 
    /// that the transmission and storage of such strings properly handle Unicode and are not subject to encoding transformations that 
    /// might alter the character set.
    /// </para>
    ///
    /// <example>
    /// The following example demonstrates how to use the <c>Base32768</c> class to encode and decode data:
    ///
    /// <code>
    /// byte[] data = new byte[] { 1, 2, 3, 4, 5 };
    /// string encoded = Base32768.EncodeBase32768(data);
    /// byte[] decoded = Base32768.DecodeBase32768(encoded);
    /// </code>
    /// </example>
    /// </summary>
    public class Base32768 {
        // Pair of code points to use in Base32768 output. Only the first 32768 characters are used.
        private static readonly ushort[] charSheet = [48, 57, 65, 90, 97, 122, 256, 750, 13056, 13310, 13312, 19893, 19968, 40869, 40960, 42182, 44032, 55203, 63744, 64045, 64256, 64511, 65072, 65103, 65136, 65276, 65281, 65439];

        /// <summary>
        /// Encodes a 15-bit integer value to a corresponding character from the Base32768 character sheet.
        /// </summary>
        /// <param name="c">The 15-bit integer to encode.</param>
        /// <returns>The encoded character as a ushort.</returns>
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

        /// <summary>
        /// Decodes a character from the Base32768 encoding back to its corresponding 15-bit integer value.
        /// </summary>
        /// <param name="c">The character to decode.</param>
        /// <returns>The decoded 15-bit integer value.</returns>
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
        //
        //                              /                             /                             /
        // 0 0 0 0 0 0 0 0 0 0 0 0 0 0 0 0 0 0 0 0 0 0 0 0 0 0 0 0 0 0 0 0 0 0 0 0 0 0 0 0 0 0 0 0 0 0 0 0 0 0 0 0 0 0 0 0 
        //                /               /               /               /               /               /
        /// <summary>
        /// Encodes a byte array into a Base32768 encoded string. Each 15 bits of the data are represented as a character.
        /// </summary>
        /// <param name="data">The byte array to encode.</param>
        /// <returns>A string representing the Base32768 encoded data.</returns>
        /// <exception cref="ArgumentNullException">Thrown if the input byte array is null.</exception>
        public static string EncodeBase32768(byte[] data) {
            if (data == null) {
                throw new ArgumentNullException("data");
            }
            if (data.Length == 0) {
                return "";
            }

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
        /// <summary>
        /// Decodes a Base32768 encoded string back into a byte array. Each character in the string represents a 15-bit integer.
        /// </summary>
        /// <param name="data">The Base32768 encoded string.</param>
        /// <returns>A byte array containing the decoded data.</returns>
        /// <exception cref="ArgumentNullException">Thrown if the input string is null.</exception>
        public static byte[] DecodeBase32768(string data) {
            if (data == null) {
                throw new ArgumentNullException("data");
            }
            if (data.Length == 0) {
                return new byte[0];
            }

            var characters = data.ToCharArray();
            int[] decoded = (from c in characters select DecodeChar(c)).ToArray();
            int lastBitsToIgnore = decoded[characters.Length - 1];
            int byteCount = ((characters.Length - 1) * 15 - lastBitsToIgnore) / 8;

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
}