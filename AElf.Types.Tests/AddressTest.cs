﻿/*
using System;
//using AElf.Common;
//using AElf.Cryptography;
//using AElf.Cryptography.ECDSA;
using Xunit;

namespace AElf.Types.Tests
{
    public class AddressTest
    {
        [Fact]
        public void UsageBuild()
        {
            Random rnd = new Random();
            
            // sha sha of pub key
            byte[] kp = CryptoHelpers.GenerateKeyPair().PublicKey;
            
            byte[] hash = new byte[3];
            rnd.NextBytes(hash);
            
            Address adr = Address.FromPublicKey(kp);
            string adr_formatted = adr.GetFormatted();
            ;
            
            Assert.True(true);
        }
        
        [Fact]
        public void Usage()
        {
            // Chain id prefix
            
            // ------ User side 
            
//            byte[] chainId = Hash.Generate().DumpByteArray();
//            string b58ChainId = Base58CheckEncoding.Encode(chainId);
//            string chainPrefix = b58ChainId.Substring(0, 4);
//            
//            // sha sha of pub key
//            KeyPairGenerator kpg = new KeyPairGenerator();
//            byte[] kp = kpg.Generate().GetEncodedPublicKey();
//            
//            // ------ chain side
//            
//            Address a = new Address();
//            var s = a.ToByteArray();
//            var ds = Address.Parser.ParseFrom(s);
//            
//            Assert.Equal(ds.ChainId, chainPrefix);
//            Assert.Equal(kp, ds.Value);
        }


    }
}
*/