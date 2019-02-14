﻿using AElf.Common;
using Google.Protobuf;
using Volo.Abp.DependencyInjection;

namespace AElf.Miner.Miner
{
    public class MinerConfig : IMinerConfig
    {
        public Address CoinBase { get; set; }
        public bool IsParallel { get; } = true;
        public int ChainId { get; set; }
        public bool IsMergeMining { get; set; }
        public string ParentAddress { get; set; }
        public string ParentPort { get; set; }

        public static readonly MinerConfig Default = new MinerConfig
        {
            CoinBase = Address.Generate()
        };
    }
}