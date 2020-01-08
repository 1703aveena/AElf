using System.Threading.Tasks;
using AElf.Contracts.Configuration;
using AElf.CSharp.CodeOps;
using AElf.Kernel.Blockchain.Application;
using AElf.Kernel.SmartContract.Application;
using AElf.Types;
using Microsoft.Extensions.Logging;
using Volo.Abp.DependencyInjection;

namespace AElf.Kernel.SmartContractExecution.Application
{
    public class CodeCheckService : ICodeCheckService, ISingletonDependency
    {
        private readonly ContractAuditor _contractAuditor = new ContractAuditor(null, null);
        
        private readonly IRequiredAcsInContractsProvider _requiredAcsInContractsProvider;
        
        public ILogger<CodeCheckService> Logger { get; set; }
        
        private volatile bool _isEnabled;
        
        public CodeCheckService(IRequiredAcsInContractsProvider requiredAcsInContractsProvider)
        {
            _requiredAcsInContractsProvider = requiredAcsInContractsProvider;
        }

        public void Enable()
        {
            _isEnabled = true;
        }

        public void Disable()
        {
            _isEnabled = false;
        }
        
        public async Task<bool> PerformCodeCheckAsync(byte[] code)
        {
            if (!_isEnabled)
                return false;

            var requiredAcs = await _requiredAcsInContractsProvider.GetRequiredAcsInContractsAsync();
            try
            {
                // Check contract code
                Logger.LogTrace("Start code check.");
                _contractAuditor.Audit(code, requiredAcs, true);
                Logger.LogTrace("Finish code check.");
                return true;
            }
            catch (InvalidCodeException e)
            {
                // May do something else to indicate that the contract has an issue
                Logger.LogWarning(e.Message);
            }
            
            return false;
        }
    }
}
