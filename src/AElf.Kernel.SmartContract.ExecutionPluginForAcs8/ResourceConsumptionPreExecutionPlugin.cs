using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Acs8;
using AElf.Contracts.MultiToken.Messages;
using AElf.Kernel.SmartContract.Application;
using AElf.Kernel.SmartContract.Sdk;
using AElf.Kernel.Token;
using AElf.Types;
using Google.Protobuf.Reflection;
using Google.Protobuf.WellKnownTypes;
using Volo.Abp.DependencyInjection;

namespace AElf.Kernel.SmartContract.ExecutionPluginForAcs8
{
    public class ResourceConsumptionPreExecutionPlugin : IPostExecutionPlugin, ISingletonDependency
    {
        private readonly IHostSmartContractBridgeContextService _contextService;
        private const string AcsSymbol = "acs8";

        public ResourceConsumptionPreExecutionPlugin(IHostSmartContractBridgeContextService contextService)
        {
            _contextService = contextService;
        }

        private static bool IsAcs8(IReadOnlyList<ServiceDescriptor> descriptors)
        {
            return descriptors.Any(service => service.File.GetIndentity() == AcsSymbol);
        }

        public async Task<IEnumerable<Transaction>> GetPostTransactionsAsync(
            IReadOnlyList<ServiceDescriptor> descriptors, ITransactionContext transactionContext)
        {
            if (!IsAcs8(descriptors))
            {
                return new List<Transaction>();
            }

            var context = _contextService.Create();
            context.TransactionContext = transactionContext;
            var selfStub = new ResourceConsumptionContractContainer.ResourceConsumptionContractStub()
            {
                __factory = new MethodStubFactory(context)
            };

            var resourceConsumptionAmount = await selfStub.GetResourceConsumptionAmount.CallAsync(new StringValue
            {
                Value = context.TransactionContext.Transaction.MethodName
            });

            // Generate token contract stub.
            var tokenContractAddress = context.GetContractAddressByName(TokenSmartContractAddressNameProvider.Name);
            if (tokenContractAddress == null)
            {
                return new List<Transaction>();
            }

            var tokenStub = new TokenContractContainer.TokenContractStub
            {
                __factory = new TransactionGeneratingOnlyMethodStubFactory
                {
                    Sender = transactionContext.Transaction.To,
                    ContractAddress = tokenContractAddress
                }
            };
            if (transactionContext.Transaction.To == tokenContractAddress &&
                transactionContext.Transaction.MethodName == nameof(tokenStub.ChargeResourceToken))
            {
                return new List<Transaction>();
            }

            // Transaction size related to NET Token.
            var transactionSize = transactionContext.Transaction.Size();
            // Transaction trace state set related to STO Token.
            var writesCount = transactionContext.Trace.StateSet.Writes.Count;
            // Transaction executing time related to CPU Token.
            var executingTime = Convert.ToInt32((transactionContext.Trace.EndTime - transactionContext.Trace.StartTime)
                .TotalMilliseconds);

            var chargeResourceTokenTransaction = (await tokenStub.ChargeResourceToken.SendAsync(new ChargeResourceTokenInput
            {
                
            })).Transaction;

            return new List<Transaction>
            {
                chargeResourceTokenTransaction
            };
        }
    }
}