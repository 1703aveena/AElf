﻿using AElf.Kernel.SmartContract;
using Volo.Abp.DependencyInjection;

namespace AElf.Kernel.FeeCalculation.Implementation
{
    public class WriteFeeProvider : TokenFeeProviderBase, IResourceTokenFeeProvider, ITransientDependency
    {
        private readonly ICalculateFunctionProvider _calculateFunctionProvider;

        public WriteFeeProvider(ICoefficientsCacheProvider coefficientsCacheProvider,
            ICalculateFunctionProvider calculateFunctionProvider) : base(
            coefficientsCacheProvider, 2)
        {
            _calculateFunctionProvider = calculateFunctionProvider;
        }

        public string TokenName { get; } = "WRITE";

        protected override void InitializeFunction()
        {
            PieceCalculateFunction = new PieceCalculateFunction();
            PieceCalculateFunction.AddFunction(_calculateFunctionProvider.LinerFunction)
                .AddFunction(_calculateFunctionProvider.LinerFunction)
                .AddFunction(_calculateFunctionProvider.PowerFunction);
        }

        protected override int GetCalculateCount(ITransactionContext transactionContext)
        {
            return transactionContext.Trace.StateSet.Writes.Count;
        }
    }
}