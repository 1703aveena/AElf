using System;
using System.Linq;
using System.Threading.Tasks;
using AElf.ChainController;
using AElf.Common;
using AElf.Kernel;
using AElf.Miner.TxMemPool.RefBlockExceptions;
using Google.Protobuf;
using Volo.Abp.DependencyInjection;

namespace AElf.Miner.TxMemPool
{
    public class TxRefBlockValidator : ITxRefBlockValidator, ISingletonDependency
    {
        private IChainService _chainService;
        private IBlockChain _blockChain;
        private CanonicalBlockHashCache _canonicalBlockHashCache;

        public TxRefBlockValidator(IChainService chainService)
        {
            _chainService = chainService;
        }

        public async Task ValidateAsync(int chainId, Transaction tx)
        {
            if (_blockChain == null)
            {
                _blockChain = _chainService.GetBlockChain(chainId);
            }

            if (_canonicalBlockHashCache == null)
            {
                _canonicalBlockHashCache = new CanonicalBlockHashCache(_blockChain);
            }

            if (tx.RefBlockNumber < GlobalConfig.GenesisBlockHeight && CheckPrefix(Hash.Genesis, tx.RefBlockPrefix))
            {
                return;
            }

            var curHeight = _canonicalBlockHashCache.CurrentHeight;
            if (tx.RefBlockNumber > curHeight && curHeight > GlobalConfig.GenesisBlockHeight)
            {
                throw  new FutureRefBlockException();
            }

            if (curHeight > GlobalConfig.ReferenceBlockValidPeriod + GlobalConfig.GenesisBlockHeight &&
                curHeight - tx.RefBlockNumber > GlobalConfig.ReferenceBlockValidPeriod)
            {
                throw new RefBlockExpiredException();
            }

            Hash canonicalHash;
            if (curHeight == 0)
            {
                canonicalHash = await _blockChain.GetCurrentBlockHashAsync();
            }
            else
            {
                canonicalHash = _canonicalBlockHashCache.GetHashByHeight(tx.RefBlockNumber);
            }

            if (canonicalHash == null)
            {
                canonicalHash = (await _blockChain.GetBlockByHeightAsync(tx.RefBlockNumber)).GetHash();
            }

            if (canonicalHash == null)
            {
                throw new Exception(
                    $"Unable to get canonical hash for height {tx.RefBlockNumber} - current height: {curHeight}");
            }

            // TODO: figure out why do we need this
            if (GlobalConfig.BlockProducerNumber == 1)
            {
                return;
            }

            if (CheckPrefix(canonicalHash, tx.RefBlockPrefix))
            {
                return;
            }
            throw new RefBlockInvalidException();
        }

        private static bool CheckPrefix(Hash blockHash, ByteString prefix)
        {
            if (prefix.Length > blockHash.Value.Length)
            {
                return false;
            }

            return !prefix.Where((t, i) => t != blockHash.Value[i]).Any();
        }
    }
}