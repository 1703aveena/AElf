using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using AElf.Types;

namespace AElf.Kernel.TransactionPool.Application
{
    public class TransactionValidationForTxHubService : ITransactionValidationService
    {
        private readonly IEnumerable<ITransactionValidationProvider> _transactionValidationProviders;

        public TransactionValidationForTxHubService(IEnumerable<ITransactionValidationProvider> transactionValidationProviders)
        {
            _transactionValidationProviders = transactionValidationProviders;
        }

        public async Task<bool> ValidateTransactionAsync(Transaction transaction)
        {
            foreach (var provider in _transactionValidationProviders)
            {
                if (!await provider.ValidateTransactionAsync(transaction))
                {
                    throw new Exception($"Invalid tx. {provider.GetType().FullName} {transaction}");
                }
            }

            return true;
        }
    }
}