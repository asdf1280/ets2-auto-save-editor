using System;
using System.Collections;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;

namespace ETS2SaveAutoEditor.Utils {

    /// <summary>
    /// Bloom filter implementation for generic types by ChatGPT o1.
    /// </summary>
    /// <typeparam name="T"></typeparam>
    public class BloomFilter<T> {
        private readonly int _size;
        private readonly BitArray _bitArray;
        private readonly int _hashFunctionCount;

        public BloomFilter(int size, int hashFunctionCount) {
            if (size <= 0)
                throw new ArgumentOutOfRangeException(nameof(size), "Size must be positive.");
            if (hashFunctionCount <= 0)
                throw new ArgumentOutOfRangeException(nameof(hashFunctionCount), "Hash function count must be positive.");

            _size = size;
            _bitArray = new BitArray(size);
            _hashFunctionCount = hashFunctionCount;
        }

        private IEnumerable<int> GetHashes(T item) {
            byte[] bytes = ObjectToByteArray(item);
            uint hash1 = Fnv1aHash(bytes);
            uint hash2 = MurmurHash3(bytes);

            for (int i = 0; i < _hashFunctionCount; i++) {
                uint combinedHash = (hash1 + (uint)i * hash2) % (uint)_size;
                yield return (int)combinedHash;
            }
        }

        public void Add(T item) {
            foreach (int hash in GetHashes(item)) {
                _bitArray.Set(hash, true);
            }
        }

        public bool Contains(T item) {
            foreach (int hash in GetHashes(item)) {
                if (!_bitArray.Get(hash))
                    return false;
            }
            return true;
        }

        private byte[] ObjectToByteArray(T obj) {
            if (obj == null)
                return Array.Empty<byte>();

            return Encoding.UTF8.GetBytes(obj.ToString());
        }

        private uint Fnv1aHash(byte[] data) {
            const uint fnvOffsetBasis = 2166136261;
            const uint fnvPrime = 16777619;

            uint hash = fnvOffsetBasis;
            foreach (byte b in data) {
                hash ^= b;
                hash *= fnvPrime;
            }
            return hash;
        }

        private uint MurmurHash3(byte[] data) {
            const uint seed = 144;
            const uint c1 = 0xcc9e2d51;
            const uint c2 = 0x1b873593;

            uint hash = seed;
            int length = data.Length;
            int currentIndex = 0;

            while (length >= 4) {
                uint k = BitConverter.ToUInt32(data, currentIndex);
                k *= c1;
                k = (k << 15) | (k >> 17); // ROTL 15
                k *= c2;

                hash ^= k;
                hash = (hash << 13) | (hash >> 19); // ROTL 13
                hash = hash * 5 + 0xe6546b64;

                currentIndex += 4;
                length -= 4;
            }

            uint k1 = 0;
            switch (length) {
                case 3:
                    k1 ^= (uint)data[currentIndex + 2] << 16;
                    goto case 2;
                case 2:
                    k1 ^= (uint)data[currentIndex + 1] << 8;
                    goto case 1;
                case 1:
                    k1 ^= data[currentIndex];
                    k1 *= c1;
                    k1 = (k1 << 15) | (k1 >> 17); // ROTL 15
                    k1 *= c2;
                    hash ^= k1;
                    break;
            }

            // Finalization
            hash ^= (uint)data.Length;
            hash ^= hash >> 16;
            hash *= 0x85ebca6b;
            hash ^= hash >> 13;
            hash *= 0xc2b2ae35;
            hash ^= hash >> 16;

            return hash;
        }
    }
}
