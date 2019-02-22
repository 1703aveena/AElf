using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using AElf.Common;
using AElf.Kernel.Infrastructure;
using Google.Protobuf.WellKnownTypes;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace AElf.Kernel.Domain
{
    public class MinersManager : IMinersManager
    {
        private readonly IMinersStore _minersStore;

        public ILogger<MinersManager> Logger { get; set; }

        public MinersManager(IMinersStore minersStore)
        {
            _minersStore = minersStore;
            Logger = NullLogger<MinersManager>.Instance;
        }

        public async Task<Miners> GetMiners(ulong termNumber)
        {
            var miners = await GetMiners(CalculateKey(termNumber));
            if (miners != null && miners.PublicKeys.Any())
                return miners;

            throw new InvalidDataException();
        }

        public async Task<bool> IsMinersInDatabase()
        {
            var miners = await GetMiners(CalculateKey(1));
            return miners != null && miners.PublicKeys.Any();
        }

        public async Task SetMiners(Miners miners, int chainId)
        {
            // Node: Sidechain cannot set miners.

            // Update MainchainLatestTermNumber of first term information
            // to inform sidechain latest version of miners list of mainchain.
            if (miners.TermNumber > 1)
            {
                var minersOfTerm1 = await GetMiners(1);
                minersOfTerm1.MainchainLatestTermNumber = miners.TermNumber;
                await SetMiners(CalculateKey(1), minersOfTerm1);
            }

            await SetMiners(CalculateKey(miners.TermNumber), miners);
        }

        private async Task<Miners> GetMiners(string key)
        {
            return await _minersStore.GetAsync<Miners>(key);
        }

        private async Task SetMiners(string key, Miners miners)
        {
            await _minersStore.SetAsync(key, miners);
        }

        private string CalculateKey(ulong termNumber)
        {
            return Hash.FromTwoHashes(Hash.FromRawBytes("DPoSMiners".CalculateHash()),
                Hash.FromMessage(new UInt64Value {Value = termNumber})).ToHex();
        }
    }
}