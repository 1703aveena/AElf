using Acs5;
using Google.Protobuf.WellKnownTypes;

namespace AElf.Contracts.TestContract.ProfitSharing
{
    public class ProfitSharingContract : ProfitSharingContractContainer.ProfitSharingContractBase
    {
        public override Empty InitializeProfitSharingContract(InitializeProfitSharingContractInput input)
        {
            Token
        }

        public override Empty CreateProfitItem(CreateProfitItemInput input)
        {
            return base.CreateProfitItem(input);
        }
    }
}