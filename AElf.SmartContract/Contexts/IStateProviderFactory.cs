using AElf.Kernel.SmartContractExecution.Domain;

namespace AElf.SmartContract.Contexts
{
    public interface IStateProviderFactory
    {
        IStateProvider CreateStateProvider();

        // Temporarily put here, delete after migration
        IStateManager CreateStateManager();
    }
}