﻿using System;
using AElf.Common;
using AElf.Cryptography;
using Shouldly;
using Xunit;

namespace AElf.Types.Tests
{
    public class AddressTest
    {
        [Fact]
        public void Generate_And_Compare_Address()
        {
            //Generate default
            var address1 = Address.Generate();
            var address2 = Address.Generate();
            var result = address1.CompareTo(address2);
            result.ShouldNotBe(0);
            address1.ShouldNotBeSameAs(address2);

            //Generate from String
            var address3 = Address.FromString("Test");
            address3.ShouldNotBe(null);

            //Generate from byte
            var bytes = new byte[30];
            new Random().NextBytes(bytes);
            var address4 = Address.FromBytes(bytes);
            address4.ShouldNotBe(null);

            //Generate from public key
            var pk = CryptoHelpers.GenerateKeyPair().PublicKey;
            var address5 = Address.FromPublicKey(pk);
            address5.ShouldNotBe(null);

            //Build Contract address
            var chainId = 1234;
            var serialNumber = (ulong) 10;
            var address6 = Address.BuildContractAddress(chainId, serialNumber);
            address6.ShouldNotBe(null);
        }

        [Fact]
        public void Get_Address_Info()
        {
            var pk = CryptoHelpers.GenerateKeyPair().PublicKey;
            var address = Address.FromPublicKey(pk);
            var addressString = address.GetFormatted();
            var pubkeyHash = address.GetPublicKeyHash();
            addressString.ShouldNotBe(string.Empty);
            addressString.StartsWith("ELF").ShouldBe(true);
            pubkeyHash.ShouldNotBe(string.Empty);
        }

        [Fact]
        public void Parse_Address_FromString()
        {
            string addStr = "ELF_5rYq3rGiULxGS51xAYF6Una1RH2bhm3REEZdda6o5NJwvRF";
            var address = Address.Parse(addStr);
            address.ShouldNotBe(null);
            var addStr1 = address.GetFormatted();
            addStr1.ShouldBe(addStr);

            addStr = "ELF_345678icdfvbghnjmkdfvgbhtn";
            Should.Throw<System.FormatException>(() => { address = Address.Parse(addStr); });
        }
    }
}