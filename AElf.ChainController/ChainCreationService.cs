﻿using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using AElf.Common;
using AElf.Kernel;
using AElf.SmartContract;
using Google.Protobuf;
namespace AElf.ChainController
{
    public class ChainCreationService : IChainCreationService
    {
        private readonly IChainService _chainService;
        private readonly ISmartContractService _smartContractService;
        public ILogger<T> Logger {get;set;}

        public ChainCreationService(IChainService chainService, ISmartContractService smartContractService)
        {
            _chainService = chainService;
            _smartContractService = smartContractService;
            Logger = NullLogger<TAAAAAA>.Instance;
        }

        /// <summary>
        /// Creates a new chain with the provided ChainId and Smart Contract Zero.
        /// </summary>
        /// <returns>The new chain async.</returns>
        /// <param name="chainId">The new chain id which will be derived from the creator address.</param>
        /// <param name="smartContractRegistration">The smart contract registration containing the code of the SmartContractZero.</param>
        public async Task<IChain> CreateNewChainAsync(Hash chainId, List<SmartContractRegistration> smartContractRegistration)
        {
            try
            {
                // TODO: Centralize this function in Hash class
                // SmartContractZero address can be derived from ChainId
                foreach (var reg in smartContractRegistration)
                {
                    var contractAddress = ContractHelpers.GetSystemContractAddress(chainId, reg.SerialNumber);
                    await _smartContractService.DeployContractAsync(chainId, contractAddress, reg, true);
                }
                
                var builder = new GenesisBlockBuilder();
                builder.Build(chainId);

                // add block to storage
                var blockchain = _chainService.GetBlockChain(chainId);
                await blockchain.AddBlocksAsync(new List<IBlock> {builder.Block});
                var chain = new Chain
                {
                    GenesisBlockHash = await blockchain.GetCurrentBlockHashAsync(),
                    Id = chainId
                };
                return chain;
            }
            catch (Exception e)
            {
                _logger.Error("CreateNewChainAsync Error: " + e);
                return null;
            }
        }
    }
}