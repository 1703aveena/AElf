using AElf.Contracts.Consensus.AEDPoS;
using AElf.Contracts.MultiToken.Messages;
using AElf.Contracts.Profit;
using AElf.Contracts.Vote;
using AElf.Sdk.CSharp.State;
using AElf.Types;

namespace AElf.Contracts.Election
{
    public partial class ElectionContractState
    {
        internal VoteContractContainer.VoteContractReferenceState VoteContract { get; set; }
        internal ProfitContractContainer.ProfitContractReferenceState ProfitContract { get; set; }
        internal TokenContractContainer.TokenContractReferenceState TokenContract { get; set; }
        internal Acs0.ACS0Container.ACS0ReferenceState BasicContractZero { get; set; }
        internal AEDPoSContractContainer.AEDPoSContractReferenceState AEDPoSContract { get; set; }

        public SingletonState<Hash> VoteContractSystemName { get; set; }
        public SingletonState<Hash> ProfitContractSystemName { get; set; }
        public SingletonState<Hash> TokenContractSystemName { get; set; }
        public SingletonState<Hash> ConsensusContractSystemName { get; set; }
    }
}