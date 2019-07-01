﻿using Google.Protobuf;

// ReSharper disable once CheckNamespace
namespace AElf.Kernel
{
    public partial class TransactionResult
    {
        // Done: use Bloom.Empty to init
        partial void OnConstruction()
        {
            Bloom = ByteString.CopyFrom(new byte[256]);
        }

        // TODO: remove
        public byte[] Serialize()
        {
            return this.ToByteArray();
        }
    }
}