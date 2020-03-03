using AElf.Contracts.MultiToken;
using AElf.Kernel.FeeCalculation.ResourceTokenFeeProvider.Impl;
using AElf.Kernel.SmartContract.Sdk;
using Volo.Abp.DependencyInjection;

namespace AElf.Kernel.FeeCalculation.Impl
{
    public class TrafficFeeProvider : TokenFeeProviderBase, IResourceTokenFeeProvider,ITransientDependency
    {
        public TrafficFeeProvider(ICoefficientsCacheProvider coefficientsCacheProvider) : base(
            coefficientsCacheProvider, (int)FeeTypeEnum.Traffic)
        {
        }
        
        public string TokenName { get; } = "TRAFFIC";
        
        protected override void InitializeFunction()
        {
            PieceCalculateFunction = new PieceCalculateFunction();
            PieceCalculateFunction.AddFunction(LinerFunction).AddFunction(PowerFunction);
        }
        
        protected override int GetCalculateCount(ITransactionContext transactionContext)
        {
            return transactionContext.Transaction.Size();
        }
    }
}