﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using AElf.ChainController.TxMemPool;
using AElf.Common.ByteArrayHelpers;
using AElf.Configuration;
using AElf.Cryptography.ECDSA;
using AElf.Kernel;
using AElf.Kernel.Managers;
using AElf.Types.CSharp;
using Easy.MessageHub;
using Google.Protobuf.WellKnownTypes;
using NLog;

namespace AElf.ChainController.TxMemPoolBM
{
    public class TxValidator : ITxValidator
    {
        private readonly ITxPoolConfig _config;
        private readonly IChainService _chainService;
        private IBlockChain _blockChain;
        private readonly ILogger _logger;

        private readonly CanonicalBlockHashCache _canonicalBlockHashCache;

        private IBlockChain BlockChain
        {
            get
            {
                if (_blockChain == null)
                {
                    _blockChain =
                        _chainService.GetBlockChain(ByteArrayHelpers.FromHexString(NodeConfig.Instance.ChainId));
                }

                return _blockChain;
            }
        }

        public TxValidator(ITxPoolConfig config, IChainService chainService, ILogger logger)
        {
            _config = config;
            _chainService = chainService;
            _logger = logger;
            _canonicalBlockHashCache = new CanonicalBlockHashCache(BlockChain, logger);
        }

        /// <summary>
        /// validate a tx size, signature, account format
        /// </summary>
        /// <param name="pool"></param>
        /// <param name="tx"></param>
        /// <returns></returns>
        public TxValidation.TxInsertionAndBroadcastingError ValidateTx(Transaction tx)
        {
            // Basically the same as TxValidation.ValidateTx but without TransactionType
            if (tx.From == Hash.Zero || tx.MethodName == "")
            {
                return TxValidation.TxInsertionAndBroadcastingError.InvalidTxFormat;
            }

            // size validation
            if (tx.Size() > _config.TxLimitSize)
            {
                return TxValidation.TxInsertionAndBroadcastingError.TooBigSize;
            }

            // TODO: signature validation
            if (!tx.VerifySignature())
            {
                return TxValidation.TxInsertionAndBroadcastingError.InvalidSignature;
            }

            if (!tx.CheckAccountAddress())
            {
                return TxValidation.TxInsertionAndBroadcastingError.WrongAddress;
            }

            // TODO: check block reference

            /*// fee validation
            if (tx.Fee < pool.MinimalFee)
            {
                // TODO: log errors, not enough Fee error 
                return false;
            }*/

            // TODO : more validations
            return TxValidation.TxInsertionAndBroadcastingError.Valid;
        }

        public async Task<TxValidation.TxInsertionAndBroadcastingError> ValidateReferenceBlockAsync(Transaction tx)
        {
            if (tx.RefBlockNumber == 0 && Hash.Genesis.CheckPrefix(tx.RefBlockPrefix))
            {
                return TxValidation.TxInsertionAndBroadcastingError.Valid;
            }

            var curHeight = _canonicalBlockHashCache.CurrentHeight;
            if (tx.RefBlockNumber > curHeight && curHeight != 0)
            {
                _logger?.Trace($"tx.RefBlockNumber({tx.RefBlockNumber}) > curHeight({curHeight})");
                return TxValidation.TxInsertionAndBroadcastingError.InvalidReferenceBlock;
            }

            if (curHeight > Globals.ReferenceBlockValidPeriod &&
                tx.RefBlockNumber < curHeight - Globals.ReferenceBlockValidPeriod)
            {
                return TxValidation.TxInsertionAndBroadcastingError.ExpiredReferenceBlock;
            }

            Hash canonicalHash;
            if (curHeight == 0)
            {
                canonicalHash = await BlockChain.GetCurrentBlockHashAsync();
            }
            else
            {
                canonicalHash = _canonicalBlockHashCache.GetHashByHeight(tx.RefBlockNumber);
            }

            if (canonicalHash == null)
            {
                canonicalHash = (await BlockChain.GetBlockByHeightAsync(tx.RefBlockNumber)).GetHash();
            }

            if (canonicalHash == null)
            {
                throw new Exception(
                    $"Unable to get canonical hash for height {tx.RefBlockNumber} - current height: {curHeight}");
            }

            if (Globals.BlockProducerNumber == 1)
            {
                return TxValidation.TxInsertionAndBroadcastingError.Valid;
            }

            var res = canonicalHash.CheckPrefix(tx.RefBlockPrefix)
                ? TxValidation.TxInsertionAndBroadcastingError.Valid
                : TxValidation.TxInsertionAndBroadcastingError.InvalidReferenceBlock;
            return res;
        }

        public List<Transaction> RemoveDirtyDPoSTxs(List<Transaction> readyTxs, Hash blockProducerAddress, Round currentRoundInfo)
        {
            if (Globals.BlockProducerNumber == 1 && readyTxs.Count == 1 && readyTxs.Any(tx => tx.MethodName == "UpdateAElfDPoS"))
            {
                return null;
            }
            
            const string inValueTxName = "PublishInValue";
            
            var toRemove = new List<Transaction>();
            
            foreach (var transaction in readyTxs)
            {
                if (transaction.From == blockProducerAddress)
                {
                    continue;
                }
                
                if (transaction.Type == TransactionType.CrossChainBlockInfoTransaction || 
                    transaction.Type == TransactionType.DposTransaction && transaction.MethodName != inValueTxName)
                {
                    toRemove.Add(transaction);
                }
                else
                {
                    if (currentRoundInfo == null || transaction.From == blockProducerAddress)
                    {
                        continue;
                    }
                    var inValue = ParamsPacker.Unpack(transaction.Params.ToByteArray(),
                        new[] {typeof(UInt64Value), typeof(StringValue), typeof(Hash)})[2] as Hash;
                    var outValue = currentRoundInfo.BlockProducers[transaction.From.ToHex().RemoveHexPrefix()].OutValue;
                    if (outValue == inValue.CalculateHash())
                    {
                        toRemove.Add(transaction);
                    }
                }
            }

            // No one will publish in value if I won't do this in current block.
            if (!readyTxs.Any(tx => tx.MethodName == inValueTxName && tx.From == blockProducerAddress))
            {
                toRemove.AddRange(readyTxs.FindAll(tx => tx.MethodName == inValueTxName));
            }
            else
            {
                // One BP can only publish in value once in one block.
                toRemove.AddRange(readyTxs.FindAll(tx => tx.MethodName == inValueTxName).GroupBy(tx => tx.From)
                    .Where(g => g.Count() > 1).SelectMany(g => g));
            }
            
            if (readyTxs.Any(tx => tx.MethodName == "UpdateAElfDPoS"))
            {
                toRemove.AddRange(readyTxs.Where(tx => tx.MethodName != inValueTxName && tx.MethodName != "UpdateAElfDPoS"));
            }

            var count = readyTxs.Count(tx => tx.MethodName == "UpdateAElfDPoS");
            if (count > 1)
            {
                toRemove.AddRange(readyTxs.Where(tx => tx.MethodName == "UpdateAElfDPoS").Take(count - 1));
            }

            foreach (var transaction in toRemove)
            {
                readyTxs.Remove(transaction);
            }
            
            PrintTxList(readyTxs);

            if (readyTxs.Count != 1 && readyTxs.Count != Globals.BlockProducerNumber + 1)
            {
                throw new Exception("Incorrect number of DPoS txs of new block: " + readyTxs.Count);
            }

            return toRemove;
        }
        
        private void PrintTxList(IEnumerable<Transaction> txs)
        {
            _logger?.Trace("Txs list:");
            foreach (var transaction in txs)
            {
                _logger?.Trace($"{transaction.GetHash().ToHex()} - {transaction.MethodName}");
            }
        }
    }
}