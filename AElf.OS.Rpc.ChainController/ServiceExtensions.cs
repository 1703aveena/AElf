﻿using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using AElf.Common;
using AElf.Kernel;
using AElf.Kernel.SmartContract;
using AElf.Kernel.SmartContract.Infrastructure;
using Anemonis.AspNetCore.JsonRpc;
using Anemonis.JsonRpc;
using Google.Protobuf;

namespace AElf.OS.Rpc.ChainController
{
    internal static class ServiceExtensions
    {
        internal static IDictionary<string, (JsonRpcRequestContract, MethodInfo, ParameterInfo[], string[])>
            GetRpcMethodContracts(this ChainControllerRpcService s)
        {
            var methods = s.GetType().GetMethods(BindingFlags.Instance | BindingFlags.Public);
            IDictionary<string, (JsonRpcRequestContract, MethodInfo, ParameterInfo[], string[])> contracts =
                new ConcurrentDictionary<string, (JsonRpcRequestContract, MethodInfo, ParameterInfo[], string[])>();

            foreach (var method in methods)
            {
                var attribute = method.GetCustomAttribute<JsonRpcMethodAttribute>();
                if (attribute == null)
                {
                    continue;
                }

                if (!(method.ReturnType == typeof(Task)) &&
                    !(method.ReturnType.IsGenericType && (method.ReturnType.GetGenericTypeDefinition() == typeof(Task<>))))
                {
                    continue;
                }

                var contract = default(JsonRpcRequestContract);
                var parameters = method.GetParameters();
                var parametersBindings = default(string[]);

                JsonRpcParametersType ParametersType() =>
                    // ReSharper disable once PossibleNullReferenceException
                    (JsonRpcParametersType) typeof(JsonRpcMethodAttribute).GetProperty("ParametersType",
                            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                        ?.GetValue(attribute, null);

                int[] ParameterPositions() =>
                    (int[]) typeof(JsonRpcMethodAttribute).GetProperty("ParameterPositions",
                            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                        ?.GetValue(attribute, null);

                string[] ParameterNames() =>
                    (string[]) typeof(JsonRpcMethodAttribute).GetProperty("ParameterNames",
                            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                        ?.GetValue(attribute, null);

                string MethodName() =>
                    (string) typeof(JsonRpcMethodAttribute).GetProperty("MethodName",
                            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                        ?.GetValue(attribute, null);

                switch (ParametersType())
                {
                    case JsonRpcParametersType.ByPosition:
                    {
                        var parameterPositions = ParameterPositions();

                        if (parameterPositions.Length != parameters.Length)
                        {
                            continue;
                        }

                        if (!Enumerable.Range(0, parameterPositions.Length).All(i => parameterPositions.Contains(i)))
                        {
                            continue;
                        }

                        var parametersContract = new Type[parameters.Length];
                        for (var i = 0; i < parameters.Length; i++)
                        {
                            parametersContract[i] = parameters[i].ParameterType;
                        }

                        contract = new JsonRpcRequestContract(parametersContract);
                    }
                        break;
                    case JsonRpcParametersType.ByName:
                    {
                        var parameterNames = ParameterNames();

                        if (parameterNames.Length != parameters.Length)
                        {
                            continue;
                        }

                        if (parameterNames.Length != parameterNames.Distinct(StringComparer.Ordinal).Count())
                        {
                            continue;
                        }

                        var parametersContract = new Dictionary<string, Type>(parameters.Length, StringComparer.Ordinal);

                        parametersBindings = new string[parameters.Length];

                        for (var i = 0; i < parameters.Length; i++)
                        {
                            parametersContract[parameterNames[i]] = parameters[i].ParameterType;
                            parametersBindings[i] = parameterNames[i];
                        }

                        contract = new JsonRpcRequestContract(parametersContract);
                    }
                        break;
                    default:
                    {
                        if (parameters.Length != 0)
                        {
                            continue;
                        }

                        contract = new JsonRpcRequestContract();
                    }
                        break;
                }

                contracts[MethodName()] = (contract, method, parameters, parametersBindings);
            }

            return contracts;
        }

        internal static async Task<IMessage> GetContractAbi(this ChainControllerRpcService s, int chainId, Address address)
        {
            var chain = await s.BlockchainService.GetChainAsync(chainId);
            var chainContext = new ChainContext()
            {
                ChainId = chainId,
                BlockHash = chain.BestChainHash,
                BlockHeight = chain.BestChainHeight
            };

            return await s.SmartContractExecutiveService.GetAbiAsync(chainId, chainContext, address);
        }

//        internal static async Task<TransactionReceipt> GetTransactionReceipt(this ChainControllerRpcService s, Hash txId)
//        {
//            return await s.TxHub.GetReceiptAsync(txId);
//        }

        internal static async Task<TransactionResult> GetTransactionResult(this ChainControllerRpcService s, Hash txHash)
        {
            // in storage
            var res = await s.TransactionResultManager.GetTransactionResultAsync(txHash);
            if (res != null)
            {
                return res;
            }

            // in tx pool
            var receipt = await s.TxHub.GetTransactionReceiptAsync(txHash);
            if (receipt != null)
            {
                return new TransactionResult
                {
                    TransactionId = receipt.TransactionId,
                    Status = TransactionResultStatus.Pending
                };
            }

            // not existed
            return new TransactionResult
            {
                TransactionId = txHash,
                Status = TransactionResultStatus.NotExisted
            };
        }

        internal static async Task<TransactionTrace> GetTransactionTrace(this ChainControllerRpcService s, int chainId, Hash txHash, ulong height)
        {
            var b = await s.GetBlockAtHeight(chainId, height);
            if (b == null)
            {
                return null;
            }

            var prodAddr = Hash.FromRawBytes(b.Header.P.ToByteArray());
            var res = await s.TransactionTraceManager.GetTransactionTraceAsync(txHash,
                HashHelpers.GetDisambiguationHash(height, prodAddr));
            return res;
        }

        internal static async Task<string> GetTransactionParameters(this ChainControllerRpcService s, int chainId, Transaction tx)
        {
            var address = tx.To;
            IExecutive executive = null;
            string output;
            try
            {
                var chain = await s.BlockchainService.GetChainAsync(chainId);
                var chainContext = new ChainContext()
                {
                    ChainId = chainId,
                    BlockHash = chain.BestChainHash,
                    BlockHeight = chain.BestChainHeight
                };

                executive = await s.SmartContractExecutiveService.GetExecutiveAsync(chainId, chainContext, address,
                    new Dictionary<StatePath, StateCache>());
                output = executive.GetJsonStringOfParameters(tx.MethodName, tx.Params.ToByteArray());
            }
            finally
            {
                if (executive != null)
                {
                    await s.SmartContractExecutiveService.PutExecutiveAsync(chainId, address, executive);
                }
            }

            return output;
        }

        internal static async Task<ulong> GetCurrentChainHeight(this ChainControllerRpcService s, int chainId)
        {
            var chainContext = await s.BlockchainService.GetChainAsync(chainId);
            return chainContext.BestChainHeight;
        }

        internal static async Task<Block> GetBlockAtHeight(this ChainControllerRpcService s, int chainId, ulong height)
        {
            return await s.BlockchainService.GetBlockByHeightAsync(chainId, height);
        }

//        internal static async Task<ulong> GetTransactionPoolSize(this ChainControllerRpcService s)
//        {
//            return (ulong) (await s.TxHub.GetExecutableTransactionSetAsync()).Count;
//        }

        internal static async Task<BinaryMerkleTree> GetBinaryMerkleTreeByHeight(this ChainControllerRpcService s, int chainId, ulong height)
        {
            return await s.BinaryMerkleTreeManager.GetTransactionsMerkleTreeByHeightAsync(chainId, height);
        }

        internal static async Task<byte[]> CallReadOnly(this ChainControllerRpcService s, int chainId, Transaction tx)
        {
            var trace = new TransactionTrace
            {
                TransactionId = tx.GetHash()
            };

            var chain = await s.BlockchainService.GetChainAsync(chainId);
            var chainContext = new ChainContext()
            {
                ChainId = chainId,
                BlockHash = chain.BestChainHash,
                BlockHeight = chain.BestChainHeight
            };

            var txContext = new TransactionContext
            {
                PreviousBlockHash = chain.BestChainHash,
                Transaction = tx,
                Trace = trace,
                BlockHeight = chain.BestChainHeight
            };

            var executive = await s.SmartContractExecutiveService.GetExecutiveAsync(chainId, chainContext, tx.To,
                new Dictionary<StatePath, StateCache>());

            try
            {
                await executive.SetTransactionContext(txContext).Apply();
            }
            finally
            {
                await s.SmartContractExecutiveService.PutExecutiveAsync(chainId, tx.To, executive);
            }

            if (!string.IsNullOrEmpty(trace.StdErr))
                throw new Exception(trace.StdErr);
            return trace.RetVal.ToFriendlyBytes();
        }

        internal static async Task<Block> GetBlock(this ChainControllerRpcService s, int chainId, Hash blockHash)
        {
            return (Block) await s.BlockchainService.GetBlockByHashAsync(chainId, blockHash);
        }

        #region Cross chain

        /*
        internal static async Task<MerklePath> GetTxRootMerklePathInParentChain(this Svc s, int chainId, ulong height)
        {
            var merklePath = await s.CrossChainInfoReader.GetTxRootMerklePathInParentChainAsync(chainId, height);
            if (merklePath != null)
                return merklePath;
            throw new Exception();
        }

        internal static async Task<JObject> GetIndexedSideChainBlockInfo(this Svc s, int chainId, ulong height)
        {
            var res = new JObject();
            var indexedSideChainBlockInfoResult = await s.CrossChainInfoReader.GetIndexedSideChainBlockInfoResult(chainId, height);
            if (indexedSideChainBlockInfoResult == null)
                return res;
            foreach (var sideChainIndexedInfo in indexedSideChainBlockInfoResult.SideChainBlockData)
            {
                res.Add(sideChainIndexedInfo.ChainId.DumpBase58(), new JObject
                {
                    {"Height", sideChainIndexedInfo.Height},
                    {"BlockHash", sideChainIndexedInfo.BlockHeaderHash.ToHex()},
                    {"TransactionMerkleTreeRoot", sideChainIndexedInfo.TransactionMKRoot.ToHex()}
                });
            }

            return res;
        }

        internal static async Task<ParentChainBlockData> GetParentChainBlockInfo(this Svc s, int chainId, ulong height)
        {
            var parentChainBlockInfo = await s.CrossChainInfoReader.GetBoundParentChainBlockInfoAsync(chainId, height);
            if (parentChainBlockInfo != null)
                return parentChainBlockInfo;
            throw new Exception();
        }

        internal static async Task<ulong> GetBoundParentChainHeight(this Svc s, int chainId, ulong height)
        {
            var parentHeight = await s.CrossChainInfoReader.GetBoundParentChainHeightAsync(chainId, height);
            if (parentHeight != 0)
                return parentHeight;
            throw new Exception();
        }
        */

        #endregion

        #region Proposal

        /*
        internal static async Task<Proposal> GetProposal(this Svc s, int chainId, Hash proposalHash)
        {
            return await s.AuthorizationInfoReader.GetProposal(chainId, proposalHash);
        }

        internal static async Task<Authorization> GetAuthorization(this Svc s, int chainId, Address msig)
        {
            return await s.AuthorizationInfoReader.GetAuthorization(chainId, msig);
        }
        */

        #endregion

        /*
        internal static async Task<int> GetRollBackTimesAsync(this Svc s)
        {
            return await Task.FromResult(s.BlockSynchronizer.RollBackTimes);
        }
        */
    }
}