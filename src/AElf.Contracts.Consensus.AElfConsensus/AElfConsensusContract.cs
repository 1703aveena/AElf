﻿using System;
using AElf.Consensus.AElfConsensus;
using AElf.Contracts.Consensus.AElfConsensus;
using Google.Protobuf.WellKnownTypes;

namespace AElf.Contracts.Consensus.AElfConsensus
{
    public partial class AElfConsensusContract : AElfConsensusContractContainer.AElfConsensusContractBase
    {
        public override Empty InitialAElfConsensusContract(InitialAElfConsensusContractInput input)
        {
            Assert(!State.Initialized.Value, "Already initialized.");

            State.ElectionContractSystemName.Value = input.ElectionContractSystemName;
            State.LockTokenForElection.Value = input.LockTokenForElection;
            State.IsTermChangeable.Value = input.IsTermChangeable;
            State.IsSideChain.Value = input.IsSideChain;

            State.DaysEachTerm.Value = input.IsSideChain ? int.MaxValue : input.DaysEachTerm;

            return new Empty();
        }
    }
}