﻿using System;
using System.Linq;
using System.Runtime.CompilerServices;
using AElf.Common.Extensions;
using Google.Protobuf;

[assembly:InternalsVisibleTo("AElf.Kernel.Tests")]
[assembly:InternalsVisibleTo("AElf.Contracts.SideChain.Tests")]
namespace AElf.Common
{
    public partial class Address : ICustomDiagnosticMessage
    {
        /// <summary>
        /// Used to override IMessage's default string representation.
        /// </summary>
        /// <returns></returns>
        public string ToDiagnosticString()
        {
            return $@"""{DumpHex()}""";
        }

        // Make private to avoid confusion
        private Address(byte[] bytes)
        {
            if (bytes.Length < GlobalConfig.AddressLength)
            {
                throw new ArgumentOutOfRangeException($"Address bytes has to be at least {GlobalConfig.AddressLength}. The input is {bytes.Length} bytes long.");
            }
            var toTruncate = bytes.Length - GlobalConfig.AddressLength;
            Value = ByteString.CopyFrom(bytes.Skip(toTruncate).ToArray());
        }

        /// <summary>
        /// Creates an address from raw byte array. If the byte array is longer than required address length,
        /// the first bytes will be skipped. The input byte array is usually serialized uncompressed public key.
        /// </summary>
        /// <param name="bytes"></param>
        /// <returns></returns>
        public static Address FromRawBytes(byte[] bytes)
        {
            return new Address(bytes);
        }

        /// <summary>
        /// Creates an address from a string. This method is supposed to be used for test only.
        /// The hash bytes of the string will be used to create the address.
        /// </summary>
        /// <param name="name"></param>
        /// <returns></returns>
        internal static Address FromString(string name)
        {
            return new Address(name.CalculateHash());
        }

//        // ReSharper disable once InconsistentNaming
//        public static Address FromECKeyPair(ECKeyPair keyPair)
//        {
//            return new Address(keyPair.GetEncodedPublicKey());
//        }

        public static Address Generate()
        {
            return FromRawBytes(Guid.NewGuid().ToByteArray().CalculateHash());
        }
        
        #region Predefined

        public static readonly Address AElf = FromString("AElf");

        public static readonly Address Zero = new Address(new byte[] { }.CalculateHash());

        public static readonly Address Genesis = FromString("Genesis");        

        #endregion

        #region Load and dump
        /// <summary>
        /// Dumps the content value to byte array.
        /// </summary>
        /// <returns></returns>
        public byte[] DumpByteArray()
        {
            return Value.ToByteArray();
        }

        /// <summary>
        /// Dumps the content value to hex string.
        /// </summary>
        /// <returns></returns>
        public string DumpHex()
        {
            return Value.ToByteArray().ToHex();
        }

        /// <summary>
        /// Loads the content value from 32-byte long byte array.
        /// </summary>
        /// <param name="bytes"></param>
        /// <returns></returns>
        /// <exception cref="ArgumentOutOfRangeException"></exception>
        public static Address LoadByteArray(byte[] bytes)
        {
            if (bytes.Length != GlobalConfig.AddressLength)
            {
                throw new ArgumentOutOfRangeException(nameof(bytes));
            }
            return new Address
            {
                Value = ByteString.CopyFrom(bytes)
            };            
        }

        /// <summary>
        /// Loads the content value represented in hex string.
        /// </summary>
        /// <param name="hex"></param>
        /// <returns></returns>
        public static Address LoadHex(string hex)
        {
            var bytes = ByteArrayHelpers.FromHexString(hex);
            return LoadByteArray(bytes);
        }
        #endregion Load and dump
    }
}