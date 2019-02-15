using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using AElf.Common;
using AElf.Kernel;
using AElf.Kernel.SmartContractExecution.Domain;
using Google.Protobuf;

namespace AElf.Kernel.SmartContract.Contexts
{
    internal class StateProvider : IStateProvider
    {
        public IBlockchainStateManager BlockchainStateManager { get; set; }

        // TODO: Combine SmartContractContext and TransactionContext
        public ITransactionContext TransactionContext { get; set; }
        public Dictionary<StatePath, StateCache> Cache { get; set; }

        public async Task<byte[]> GetAsync(StatePath path)
        {
            // TODO: StatePath (string)
            var byteString = await BlockchainStateManager.GetStateAsync(
                string.Join("/", path.Path.Select(x => x.ToStringUtf8())),
                TransactionContext.BlockHeight,
                TransactionContext.PreviousBlockHash
            );
            byteString = byteString ?? ByteString.Empty;
            return byteString.ToByteArray();
        }
    }
}