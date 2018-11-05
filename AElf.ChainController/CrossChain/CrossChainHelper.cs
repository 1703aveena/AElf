using AElf.Common;
using AElf.Configuration;
using AElf.Kernel;
using AElf.Kernel.Storages;
using AElf.SmartContract;
using Google.Protobuf;
using Google.Protobuf.WellKnownTypes;

namespace AElf.ChainController.CrossChain
{
    public class CrossChainHelper
    {
        private readonly Hash _chainId;

        private Address SideChainContractAddress =>
            AddressHelpers.GetSystemContractAddress(Hash.LoadHex(NodeConfig.Instance.ChainId),
                SmartContractType.SideChainContract.ToString());
        
        private DataProvider DataProvider { get; }
        public CrossChainHelper(Hash chainId, IStateStore stateStore)
        {
            _chainId = chainId;
            DataProvider = DataProvider.GetRootDataProvider(_chainId, SideChainContractAddress);
            DataProvider.StateStore = stateStore; 
        }

        /// <summary>
        /// Assert: Related value has surely exists in database.
        /// </summary>
        /// <param name="keyHash"></param>
        /// <param name="resourceStr"></param>
        /// <returns></returns>
        internal byte[] GetBytes<T>(Hash keyHash, string resourceStr = "") where T : IMessage, new()
        {
            //Console.WriteLine("resourceStr: {0}", dataPath.ResourcePathHash.ToHex());
            return resourceStr != ""
                ? DataProvider.GetChild(resourceStr).GetAsync<T>(keyHash).Result
                : DataProvider.GetAsync<T>(keyHash).Result;
        }
    }
}