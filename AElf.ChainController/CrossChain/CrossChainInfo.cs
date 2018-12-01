using System;
using System.Threading.Tasks;
using AElf.Common;
using AElf.Configuration;
using AElf.Configuration.Config.Chain;
using AElf.Kernel;
using AElf.Kernel.Storages;
using AElf.SmartContract;
using Google.Protobuf.WellKnownTypes;

namespace AElf.ChainController.CrossChain
{
    public class CrossChainInfo : ICrossChainInfo
    {
        private readonly ContractInfoReader _contractInfoReader;
        private Address SideChainContractAddress =>
            ContractHelpers.GetCrossChainContractAddress(Hash.LoadHex(ChainConfig.Instance.ChainId));
        public CrossChainInfo(IStateStore stateStore)
        {
            var chainId = Hash.LoadHex(ChainConfig.Instance.ChainId);
            _contractInfoReader = new ContractInfoReader(chainId, stateStore);
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
                            GlobalConfig.AElfTxRootMerklePathInParentChain) ?? new byte[0];
            return MerklePath.Parser.ParseFrom(bytes);
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
                            GlobalConfig.AElfBoundParentChainHeight) ?? new byte[0];
            return UInt64Value.Parser.ParseFrom(bytes).Value;
        }

        /// <summary>
        /// Get current height of parent chain block stored locally
        /// </summary>
        /// <returns></returns>
        public ulong GetParentChainCurrentHeight()
        {
            var bytes = _contractInfoReader.GetBytes<UInt64Value>(SideChainContractAddress,
                            Hash.FromString(GlobalConfig.AElfCurrentParentChainHeight)) ?? new byte[0];
            return UInt64Value.Parser.ParseFrom(bytes).Value;
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
                            GlobalConfig.AElfParentChainBlockInfo) ?? new byte[0];
            return ParentChainBlockInfo.Parser.ParseFrom(bytes);
        }
    }
}