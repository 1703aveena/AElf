using System.Collections.Generic;
using System.Threading.Tasks;
using AElf.Common;
using AElf.Kernel;
using Google.Protobuf;

namespace AElf.SmartContract.Proposal
{
    public interface IAuthorizationInfoReader
    {
        Task<bool> CheckAuthority(Address mSigAddress, IEnumerable<byte[]> pubKeys);
        Task<bool> CheckAuthority(Transaction transaction);
        Task<Kernel.Proposal> GetProposal(Hash proposalHash);
        Task<Authorization> GetAuthorization(Address msig);
    }
}