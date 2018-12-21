using AElf.Common;
using AElf.Configuration.Config.Chain;
using AElf.Kernel;
using AElf.Kernel.Manager.Interfaces;
using AElf.SmartContract;
using Google.Protobuf.WellKnownTypes;

namespace AElf.ChainController.CrossChain
{
    public class CrossChainInfo : ICrossChainInfo
    {
        private readonly ContractInfoReader _contractInfoReader;
        private Address SideChainContractAddress =>
            ContractHelpers.GetCrossChainContractAddress(Hash.LoadBase58(ChainConfig.Instance.ChainId));
        public CrossChainInfo(IStateManager stateManager)
        {
            var chainId = Hash.LoadBase58(ChainConfig.Instance.ChainId);
            _contractInfoReader = new ContractInfoReader(chainId, stateManager);
        }

        /// <summary>
        /// Get merkle path of transaction root in parent chain.
        /// </summary>
        /// <param name="blockHeight">Child chain block height.</param>
        /// <returns></returns>
        public MerklePath GetTxRootMerklePathInParentChain(ulong blockHeight)
        {
            var bytes = _contractInfoReader.GetBytes<MerklePath>(SideChainContractAddress,
                            Hash.FromMessage(new UInt64Value {Value = blockHeight}),
                            GlobalConfig.AElfTxRootMerklePathInParentChain);
            return bytes == null ? null : MerklePath.Parser.ParseFrom(bytes);
        }

        /// <summary>
        /// Get height of parent chain block which indexed the local chain block at <see cref="localChainHeight"/>
        /// </summary>
        /// <param name="localChainHeight"></param>
        /// <returns></returns>
        public ulong GetBoundParentChainHeight(ulong localChainHeight)
        {
            var bytes = _contractInfoReader.GetBytes<UInt64Value>(SideChainContractAddress,
                            Hash.FromMessage(new UInt64Value {Value = localChainHeight}),
                            GlobalConfig.AElfBoundParentChainHeight);
            return bytes == null ? 0 : UInt64Value.Parser.ParseFrom(bytes).Value;
        }

        /// <summary>
        /// Get current height of parent chain block stored locally
        /// </summary>
        /// <returns></returns>
        public ulong GetParentChainCurrentHeight()
        {
            var bytes = _contractInfoReader.GetBytes<UInt64Value>(SideChainContractAddress,
                            Hash.FromString(GlobalConfig.AElfCurrentParentChainHeight));
            return bytes == null ? 0 : UInt64Value.Parser.ParseFrom(bytes).Value;
        }

        /// <summary>
        /// Get info of parent chain block which indexes the local chain block at <see cref="localChainHeight"/>
        /// </summary>
        /// <param name="localChainHeight">Local chain height</param>
        /// <returns></returns>
        public ParentChainBlockInfo GetBoundParentChainBlockInfo(ulong localChainHeight)
        {
            var bytes = _contractInfoReader.GetBytes<ParentChainBlockInfo>(SideChainContractAddress,
                            Hash.FromMessage(new UInt64Value {Value = localChainHeight}),
                            GlobalConfig.AElfParentChainBlockInfo);
            return bytes == null ? null : ParentChainBlockInfo.Parser.ParseFrom(bytes);
        }
    }
}