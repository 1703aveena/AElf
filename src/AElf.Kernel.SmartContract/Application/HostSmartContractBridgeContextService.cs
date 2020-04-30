using System;
using AElf.Kernel.SmartContract;
using Microsoft.Extensions.DependencyInjection;
using Volo.Abp.DependencyInjection;

namespace AElf.Kernel.SmartContract.Application
{
    public class HostSmartContractBridgeContextService : IHostSmartContractBridgeContextService, ISingletonDependency
    {
        private readonly IServiceProvider _serviceProvider;

        public HostSmartContractBridgeContextService(IServiceProvider serviceProvider)
        {
            _serviceProvider = serviceProvider;
        }


        public IHostSmartContractBridgeContext Create()
        {
            //Create a new context
            var context = _serviceProvider.GetService<IHostSmartContractBridgeContext>();
            return context;
        }
    }
}