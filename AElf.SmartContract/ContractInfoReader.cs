using AElf.Common;
using AElf.Kernel.Manager.Interfaces;
using Google.Protobuf;

namespace AElf.SmartContract
{
    public class ContractInfoReader
    {
        private readonly Hash _chainId;
        private readonly IStateManager _stateManager;

        public ContractInfoReader(Hash chainId, IStateManager stateManager)
        {
            _chainId = chainId;
            _stateManager = stateManager; 
        }

        /// <summary>
        /// Assert: Related value has surely exists in database.
        /// </summary>
        /// <param name="contractAddress"></param>
        /// <param name="keyHash"></param>
        /// <param name="resourceStr"></param>
        /// <returns></returns>
        public byte[] GetBytes<T>(Address contractAddress, Hash keyHash, string resourceStr = "") where T : IMessage, new()
        {
            //Console.WriteLine("resourceStr: {0}", dataPath.ResourcePathHash.ToHex());
            var dp = DataProvider.GetRootDataProvider(_chainId, contractAddress);
            dp.StateManager = _stateManager;
            
            return resourceStr != ""
                ? dp.GetChild(resourceStr).GetAsync<T>(keyHash).Result : dp.GetAsync<T>(keyHash).Result;
        }
    }
}