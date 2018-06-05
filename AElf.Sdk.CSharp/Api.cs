﻿using System.Collections.Generic;
using System.Threading.Tasks;
using AElf.Kernel;
using Google.Protobuf;

namespace AElf.Sdk.CSharp
{
    /// <summary>
    /// Singleton that holds the smart contract API for interacting with the chain via the injected context.
    /// </summary>
    public class Api
    {
        private static Dictionary<string, IDataProvider> _dataProviders;
        private static ISmartContractContext _smartContractContext;
        private static ITransactionContext _transactionContext;

        #region Setters used by runner and executor

        public static void SetSmartContractContext(ISmartContractContext contractContext)
        {
            _smartContractContext = contractContext;
            _dataProviders = new Dictionary<string, IDataProvider>()
            {
                {"", _smartContractContext.DataProvider}
            };
        }

        public static void SetTransactionContext(ITransactionContext transactionContext)
        {
            _transactionContext = transactionContext;
        }

        #endregion Setters used by runner and executor

        #region Getters used by contract

        #region Privileged API
        public static async Task DeployContractAsync(Hash address, SmartContractRegistration registration)
        {
            await _smartContractContext.SmartContractService.DeployContractAsync(address, registration);
        }

        #endregion Privileged API

        public static Hash GetChainId()
        {
            return _smartContractContext.ChainId;
        }

        public static Hash GetContractAddress()
        {
            return _smartContractContext.ContractAddress;
        }

        public static IDataProvider GetDataProvider(string name)
        {
            if (!_dataProviders.TryGetValue(name, out var dp))
            {
                dp = _smartContractContext.DataProvider.GetDataProvider(name);
                _dataProviders.Add(name, dp);
            }

            return dp;
        }

        public static ITransaction GetTransaction()
        {
            return _transactionContext.Transaction;
        }

        public static void LogToResult(byte[] log)
        {
            // TODO: Improve
            _transactionContext.TransactionResult.Logs = ByteString.CopyFrom(log);
        }

        #endregion Getters used by contract

        #region Transaction API
        public static void Call(Hash contractAddress, string methodName, byte[] args)
        {
            throw new System.NotImplementedException();
        }
        #endregion

    }
}