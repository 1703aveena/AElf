﻿using AElf.Contracts.MultiToken;
using AElf.Kernel.SmartContract;
using Volo.Abp.DependencyInjection;

namespace AElf.Kernel.FeeCalculation.Infrastructure
{
    public class TxFeeProvider : TokenFeeProviderBase, IPrimaryTokenFeeProvider
    {
        private readonly ICalculateFunctionProvider _calculateFunctionProvider;

        public TxFeeProvider(ICoefficientsCacheProvider coefficientsCacheProvider,
            ICalculateFunctionProvider calculateFunctionProvider) : base(
            coefficientsCacheProvider, (int) FeeTypeEnum.Tx)
        {
            _calculateFunctionProvider = calculateFunctionProvider;
        }

        protected override void InitializeFunction()
        {
            PieceCalculateFunction = new PieceCalculateFunction();
            PieceCalculateFunction.AddFunction(_calculateFunctionProvider.LinerFunction)
                .AddFunction(_calculateFunctionProvider.PowerFunction);
        }

        protected override int GetCalculateCount(ITransactionContext transactionContext)
        {
            return transactionContext.Transaction.Size();
        }
    }
}