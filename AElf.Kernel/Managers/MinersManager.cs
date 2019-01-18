using System.Linq;
using System.Threading.Tasks;
using AElf.Common;
using AElf.Configuration;
using AElf.Configuration.Config.Chain;
using AElf.Kernel.Storages;
using NLog;

namespace AElf.Kernel.Managers
{
    public class MinersManager : IMinersManager
    {
        private readonly IMinersStore _minersStore;

        private readonly ILogger _logger = LogManager.GetLogger(nameof(MinersManager));

        public MinersManager(IMinersStore minersStore)
        {
            _minersStore = minersStore;
        }
        
        public async Task<Miners> GetMiners(ulong termNumber)
        {
            Miners miners;
            if (termNumber != 0)
            {
                miners = await GetMiners(CalculateKey(termNumber));
                if (miners != null && miners.PublicKeys.Any())
                    return miners;
            }

            var dict = MinersConfig.Instance.Producers;
            miners = new Miners();

            foreach (var bp in dict.Values)
            {
                miners.PublicKeys.Add(bp["public_key"]);
            }

            return miners;
        }

        public async Task<bool> IsMinersInDatabase()
        {
            var miners = await GetMiners(CalculateKey(1));
            return miners != null && !miners.IsEmpty();
        }

        public async Task SetMiners(Miners miners)
        {
            if (ChainConfig.Instance.ChainId != GlobalConfig.DefaultChainId)
            {
                return;
            }

            foreach (var publicKey in miners.PublicKeys)
            {
                _logger?.Trace($"Set miner {publicKey} to data store.");
            }

            if (miners.TermNumber > 1)
            {
                // To inform sidechain latest version of miners list of mainchain.
                _logger?.Trace($"BP-term for sidechain: {miners.TermNumber}");
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
            return Hash.FromTwoHashes(Hash.FromRawBytes(GlobalConfig.AElfDPoSMinersString.CalculateHash()),
                Hash.FromMessage(termNumber.ToUInt64Value())).ToHex();
        }
    }
}