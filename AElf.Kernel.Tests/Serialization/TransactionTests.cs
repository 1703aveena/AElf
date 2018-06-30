﻿using System;
using Google.Protobuf;
using Google.Protobuf.WellKnownTypes;
using Xunit;

namespace AElf.Kernel.Tests.Serialization
{
    public class TransactionTests
    {
        [Fact]
        public void FromTo()
        {
            Transaction t = new Transaction();
            t.From = new byte[] { 0x01, 0x02 };
            t.To = new byte[] { 0x03, 0x04 };

            byte[] b = t.ToByteArray();

            string bstr = Convert.ToBase64String(b);
            ;
            // bstr = CgQKAgECEgQKAgME
        }

        
        [Fact]
        public void Deserialize()
        {
            string sdata = "CGU=";
            var data = ByteString.FromBase64(sdata);
            System.Diagnostics.Debug.WriteLine(UInt64Value.Parser.ParseFrom(data.ToByteArray()).Value);
        }
    }
}