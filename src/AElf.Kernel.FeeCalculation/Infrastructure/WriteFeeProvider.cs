﻿using AElf.Contracts.MultiToken;
using AElf.Kernel.SmartContract;
using Volo.Abp.DependencyInjection;

namespace AElf.Kernel.FeeCalculation.Infrastructure
{
    internal class WriteFeeProvider : TokenFeeProviderBase, IResourceTokenFeeProvider, ITransientDependency
    {
        public WriteFeeProvider(ICoefficientsCacheProvider coefficientsCacheProvider,
            ICalculateFunctionProvider calculateFunctionProvider) : base(
            coefficientsCacheProvider, calculateFunctionProvider, (int) FeeTypeEnum.Write)
        {

        }

        public int[] PieceTypeArray { get; set; }

        public string TokenName => "WRITE";

        protected override int GetCalculateCount(ITransactionContext transactionContext)
        {
            return transactionContext.Trace.StateSet.Writes.Count;
        }
    }
}