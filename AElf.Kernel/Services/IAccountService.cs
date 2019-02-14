using System.Threading.Tasks;
using AElf.Common;

namespace AElf.Kernel.Services
{
    public interface IAccountService
    {
        Task<byte[]> SignAsync(byte[] data);
        Task<bool> VerifySignatureAsync(byte[] signature, byte[] data, byte[] publicKey);
        Task<byte[]> GetPublicKeyAsync();
        Task<Address> GetAccountAsync();
    }
}