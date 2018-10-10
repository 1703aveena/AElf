using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using AElf.Common.Extensions;
using Google.Protobuf;

// ReSharper disable once CheckNamespace
namespace AElf.Common
{
    public partial class Hash : ICustomDiagnosticMessage, IComparable<Hash>
    {
        private const int ByteArrayLength = 32;
        public string ToDiagnosticString()
        {
            return $@"""{Dumps()}""";
        }

        private Hash(byte[] bytes)
        {
            if (bytes.Length != ByteArrayLength)
            {
                throw new ArgumentOutOfRangeException($"Hash bytes has to be {ByteArrayLength} bytes long. The input is {bytes.Length} bytes long.");
            }
            Value = ByteString.CopyFrom(bytes.ToArray());
        }

        #region Hashes from various types

        public static Hash FromBytes(byte[] bytes)
        {
            return new Hash(bytes.CalculateHash());
        }

        public static Hash FromString(string str)
        {
            return FromBytes(Encoding.UTF8.GetBytes(str));
        }

        public static Hash FromMessage(IMessage message)
        {
            return FromBytes(message.ToByteArray());
        }

        public static Hash FromTwoHashes(Hash hash1, Hash hash2)
        {
            var hashes = new List<Hash>()
            {
                hash1, hash2
            };
            using (var mm = new MemoryStream())
            using (var stream = new CodedOutputStream(mm))
            {
                foreach (var hash in hashes.OrderBy(x=>x))
                {
                    hash.WriteTo(stream);
                }
                stream.Flush();
                mm.Flush();
                return FromBytes(mm.ToArray());
            }
        }
        
        public static Hash Generate()
        {
            return FromBytes(Guid.NewGuid().ToByteArray());
        }        

        #endregion

        #region Predefined

        public static readonly Hash Zero = Hash.FromBytes(new byte[] { });

        public static readonly Hash Default = Hash.FromString("AElf");

        public static readonly Hash Genesis = Hash.FromString("Genesis");        

        #endregion

        public Hash OfType(HashType hashType)
        {
            var hash = Clone();
            hash.HashType = hashType;
            return hash;
        }

        #region Comparing

        public static bool operator ==(Hash h1, Hash h2)
        {
            return h1?.Equals(h2) ?? ReferenceEquals(h2, null);
        }

        public static bool operator !=(Hash h1, Hash h2)
        {
            return !(h1 == h2);
        }

        public static bool operator <(Hash h1, Hash h2)
        {
            return CompareHash(h1, h2) < 0;
        }

        public static bool operator >(Hash h1, Hash h2)
        {
            return CompareHash(h1, h2) > 0;
        }

        private static int CompareHash(Hash hash1, Hash hash2)
        {
            if (hash1 != null)
            {
                return hash2 == null ? 1 : Compare(hash1, hash2);
            }
            
            if (hash2 == null)
            {
                return 0;
            }
            
            return -1;
        }
        
        private static int Compare(Hash x, Hash y)
        {
            if (x == null || y == null)
            {
                throw new InvalidOperationException("Cannot compare hash when hash is null");
            }
            
            var xValue = x.Value;
            var yValue = y.Value;
            for (var i = 0; i < Math.Min(xValue.Length, yValue.Length); i++)
            {
                if (xValue[i] > yValue[i])
                {
                    return 1;
                }

                if (xValue[i] < yValue[i])
                {
                    return -1;
                }
            }

            return 0;
        }
        
        public int CompareTo(Hash that)
        {
            return Compare(this, that);
        }        

        #endregion

        #region Bitwise operations

        public static Hash Xor(Hash h1, Hash h2)
        {
            var newHashBytes = new byte[h1.Value.Length];
            for (int i= 0; i < newHashBytes.Length; i++)
            {
                newHashBytes[i] = (byte) (h1.Value[i] ^ h2.Value[i]);
            }
            return new Hash()
            {
                Value = ByteString.CopyFrom(newHashBytes)
            };
        }

        #endregion
        
        #region Load and dump
        /// <summary>
        /// Dumps the content value to byte array.
        /// </summary>
        /// <returns></returns>
        public byte[] Dump()
        {
            return Value.ToByteArray();
        }

        /// <summary>
        /// Dumps the content value to hex string.
        /// </summary>
        /// <returns></returns>
        public string Dumps()
        {
            return Value.ToByteArray().ToHex();
        }

        /// <summary>
        /// Loads the content value from 32-byte long byte array.
        /// </summary>
        /// <param name="bytes"></param>
        /// <returns></returns>
        /// <exception cref="ArgumentOutOfRangeException"></exception>
        public static Hash Load(byte[] bytes)
        {
            if (bytes.Length != 32)
            {
                throw new ArgumentOutOfRangeException(nameof(bytes));
            }
            return new Hash
            {
                Value = ByteString.CopyFrom(bytes)
            };            
        }

        /// <summary>
        /// Loads the content value represented in hex string.
        /// </summary>
        /// <param name="hex"></param>
        /// <returns></returns>
        /// <exception cref="ArgumentOutOfRangeException"></exception>
        public static Hash Loads(string hex)
        {
            var bytes = ByteArrayHelpers.FromHexString(hex);
            return Load(bytes);
        }
        #endregion Load and dump
    }
}