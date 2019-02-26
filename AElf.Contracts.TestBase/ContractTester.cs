using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AElf.Common;
using AElf.Cryptography;
using AElf.Cryptography.ECDSA;
using AElf.Database;
using AElf.Kernel;
using AElf.Kernel.Account.Application;
using AElf.Kernel.Blockchain.Application;
using AElf.Kernel.Blockchain.Domain;
using AElf.Kernel.Consensus.Application;
using AElf.Kernel.Infrastructure;
using AElf.Kernel.KernelAccount;
using AElf.Kernel.Miner.Application;
using AElf.Kernel.Node.Application;
using AElf.Kernel.Services;
using AElf.Kernel.SmartContractExecution.Application;
using AElf.Kernel.TransactionPool.Infrastructure;
using AElf.OS.Node.Application;
using AElf.Types.CSharp;
using Google.Protobuf;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Volo.Abp;
using Volo.Abp.DependencyInjection;

namespace AElf.Contracts.TestBase
{
    public class ContractTester : ITransientDependency
    {
        private readonly int _chainId;

        private readonly IBlockchainService _blockchainService;
        private readonly ITransactionExecutingService _transactionExecutingService;
        private readonly IBlockchainNodeContextService _blockchainNodeContextService;
        private readonly IBlockGenerationService _blockGenerationService;
        private ISystemTransactionGenerationService _systemTransactionGenerationService;
        private readonly IBlockExecutingService _blockExecutingService;
        private readonly IConsensusService _consensusService;
        private readonly IBlockchainExecutingService _blockchainExecutingService;
        private readonly IChainManager _chainManager;
        private readonly ITransactionResultManager _transactionResultManager;

        public Chain Chain => GetChainAsync().Result;

        public ECKeyPair KeyPair { get; set; }

        public List<Address> DeployedContractsAddresses { get; set; }

        public ContractTester(int chainId, ECKeyPair keyPair = null)
        {
            _chainId = chainId;

            var application =
                AbpApplicationFactory.Create<ContractTestAElfModule>(options =>
                {
                    options.UseAutofac();
                    options.Services.AddKeyValueDbContext<BlockchainKeyValueDbContext>(o => o.UseInMemoryDatabase());
                    options.Services.AddKeyValueDbContext<StateKeyValueDbContext>(o => o.UseInMemoryDatabase());
                });
            application.Initialize();

            _blockchainService = application.ServiceProvider.GetService<IBlockchainService>();
            _transactionExecutingService = application.ServiceProvider.GetService<ITransactionExecutingService>();
            _blockchainNodeContextService = application.ServiceProvider.GetService<IBlockchainNodeContextService>();
            _blockGenerationService = application.ServiceProvider.GetService<IBlockGenerationService>();
            _systemTransactionGenerationService =
                application.ServiceProvider.GetService<ISystemTransactionGenerationService>();
            _blockExecutingService = application.ServiceProvider.GetService<IBlockExecutingService>();
            _consensusService = application.ServiceProvider.GetService<IConsensusService>();
            _blockchainExecutingService = application.ServiceProvider.GetService<IBlockchainExecutingService>();
            _chainManager = application.ServiceProvider.GetService<IChainManager>();
            _transactionResultManager = application.ServiceProvider.GetService<ITransactionResultManager>();

            if (keyPair != null)
            {
                KeyPair = keyPair;
            }
        }

        /// <summary>
        /// Initial a chain with given chain id (passed to ctor),
        /// and produce the genesis block with provided contract types.
        /// </summary>
        /// <param name="contractTypes"></param>
        /// <returns>Return contract addresses as the param order.</returns>
        public async Task<List<Address>> InitialChainAsync(params Type[] contractTypes)
        {
            var transactions = GetGenesisTransactions(_chainId, contractTypes);
            var dto = new OsBlockchainNodeContextStartDto
            {
                BlockchainNodeContextStartDto = new BlockchainNodeContextStartDto
                {
                    ChainId = _chainId,
                    Transactions = transactions
                }
            };

            await _blockchainNodeContextService.StartAsync(dto.BlockchainNodeContextStartDto);

            var addresses = new List<Address>();
            for (var i = 0UL; i < (ulong) contractTypes.Length; i++)
            {
                addresses.Add(GetContractAddress(i));
            }

            DeployedContractsAddresses = addresses;

            return addresses;
        }

        /// <summary>
        /// Generate a transaction and sign it.
        /// </summary>
        /// <param name="contractAddress"></param>
        /// <param name="methodName"></param>
        /// <param name="callerKeyPair"></param>
        /// <param name="objects"></param>
        /// <returns></returns>
        public Transaction GenerateTransaction(Address contractAddress, string methodName,
            ECKeyPair callerKeyPair, params object[] objects)
        {
            var tx = new Transaction
            {
                From = GetAddress(callerKeyPair),
                To = contractAddress,
                MethodName = methodName,
                Params = ByteString.CopyFrom(ParamsPacker.Pack(objects))
            };

            var signature = CryptoHelpers.SignWithPrivateKey(callerKeyPair.PrivateKey, tx.GetHash().DumpByteArray());
            tx.Sigs.Add(ByteString.CopyFrom(signature));

            return tx;
        }

        /// <summary>
        /// Mine a block with given normal txs and system txs.
        /// Normal txs will use tx pool while system txs not.
        /// </summary>
        /// <param name="txs"></param>
        /// <param name="systemTxs"></param>
        /// <returns></returns>
        public async Task<Block> MineABlockAsync(List<Transaction> txs, List<Transaction> systemTxs = null)
        {
            var preBlock = await _blockchainService.GetBestChainLastBlock(_chainId);
            var minerService = BuildMinerService(txs, systemTxs);
            return await minerService.MineAsync(_chainId, preBlock.GetHash(), preBlock.Height,
                DateTime.UtcNow.AddMilliseconds(4000));
        }

        /// <summary>
        /// Generate a tx then package the new tx to a new block.
        /// </summary>
        /// <param name="contractAddress"></param>
        /// <param name="methodName"></param>
        /// <param name="callerKeyPair"></param>
        /// <param name="objects"></param>
        /// <returns></returns>
        public async Task<Block> ExecuteContractWithMiningAsync(Address contractAddress, string methodName,
            ECKeyPair callerKeyPair, params object[] objects)
        {
            var tx = GenerateTransaction(contractAddress, methodName, callerKeyPair, objects);
            return await MineABlockAsync(new List<Transaction> {tx});
        }

        /// <summary>
        /// Using tx to call a method without mining.
        /// The state database won't change.
        /// </summary>
        /// <param name="contractAddress"></param>
        /// <param name="methodName"></param>
        /// <param name="callerKeyPair"></param>
        /// <param name="objects"></param>
        /// <returns></returns>
        public async Task<ByteString> CallContractMethodAsync(Address contractAddress, string methodName,
            ECKeyPair callerKeyPair, params object[] objects)
        {
            var tx = GenerateTransaction(contractAddress, methodName, callerKeyPair, objects);
            var preBlock = await _blockchainService.GetBestChainLastBlock(_chainId);
            var executionReturnSets = await _transactionExecutingService.ExecuteAsync(new ChainContext
                {
                    ChainId = _chainId,
                    BlockHash = preBlock.GetHash(),
                    BlockHeight = preBlock.Height
                },
                new List<Transaction> {tx},
                DateTime.UtcNow, new CancellationToken());

            return executionReturnSets.Any() ? executionReturnSets.Last().ReturnValue : null;
        }

        public void SignTransaction(ref Transaction transaction, ECKeyPair callerKeyPair)
        {
            var signature =
                CryptoHelpers.SignWithPrivateKey(callerKeyPair.PrivateKey, transaction.GetHash().DumpByteArray());
            transaction.Sigs.Add(ByteString.CopyFrom(signature));
        }

        public void SignTransaction(ref List<Transaction> transactions, ECKeyPair callerKeyPair)
        {
            foreach (var transaction in transactions)
            {
                var signature =
                    CryptoHelpers.SignWithPrivateKey(callerKeyPair.PrivateKey, transaction.GetHash().DumpByteArray());
                transaction.Sigs.Add(ByteString.CopyFrom(signature));
            }
        }

        public async Task<Chain> GetChainAsync()
        {
            return await _blockchainService.GetChainAsync(_chainId);
        }

        /// <summary>
        /// Execute a block and add it to chain database.
        /// </summary>
        /// <param name="block"></param>
        /// <param name="txs"></param>
        /// <param name="systemTxs"></param>
        /// <returns></returns>
        public async Task AddABlockAsync(Block block, List<Transaction> txs, List<Transaction> systemTxs)
        {
            await _blockExecutingService.ExecuteBlockAsync(_chainId, block.Header, systemTxs, txs,
                new CancellationToken());
            await _blockchainService.AddBlockAsync(_chainId, block);
            var chain = await _blockchainService.GetChainAsync(_chainId);
            await _blockchainExecutingService.AttachBlockToChainAsync(chain, block);
        }

        public async Task SetIrreversibleBlock(Hash libHash)
        {
            var chain = await _blockchainService.GetChainAsync(_chainId);
            await _chainManager.SetIrreversibleBlockAsync(chain, libHash);
        }

        public async Task SetIrreversibleBlock(ulong libHeight)
        {
            var chain = await _blockchainService.GetChainAsync(_chainId);
            var libHash = (await _blockchainService.GetBlockByHeightAsync(_chainId, libHeight)).GetHash();
            chain.LastIrreversibleBlockHash = libHash;
            chain.LastIrreversibleBlockHeight = libHeight;
            await _chainManager.SetIrreversibleBlockAsync(chain, libHash);
        }

        /// <summary>
        /// Get the execution result of a tx by its tx id.
        /// </summary>
        /// <param name="txId"></param>
        /// <returns></returns>
        public async Task<TransactionResult> GetTransactionResult(Hash txId)
        {
            return await _transactionResultManager.GetTransactionResultAsync(txId);
        }

        private MinerService BuildMinerService(List<Transaction> txs, List<Transaction> systemTxs = null)
        {
            var trs = new List<TransactionReceipt>();

            foreach (var transaction in txs)
            {
                var tr = new TransactionReceipt(transaction)
                {
                    SignatureStatus = SignatureStatus.SignatureValid, RefBlockStatus = RefBlockStatus.RefBlockValid
                };
                trs.Add(tr);
            }

            var mockTxHub = new Mock<ITxHub>();
            mockTxHub.Setup(h => h.GetReceiptsOfExecutablesAsync()).ReturnsAsync(trs);

            if (systemTxs != null)
            {
                var mockSystemTransactionGenerationService = new Mock<ISystemTransactionGenerationService>();
                mockSystemTransactionGenerationService.Setup(s =>
                    s.GenerateSystemTransactions(It.IsAny<Address>(), It.IsAny<ulong>(), It.IsAny<byte[]>(),
                        It.IsAny<int>())).Returns(systemTxs);
                _systemTransactionGenerationService = mockSystemTransactionGenerationService.Object;
            }

            var mockAccountService = new Mock<IAccountService>();
            mockAccountService.Setup(s => s.GetPublicKeyAsync())
                .ReturnsAsync(CryptoHelpers.GenerateKeyPair().PublicKey);
            return new MinerService(mockTxHub.Object, mockAccountService.Object, _blockGenerationService,
                _systemTransactionGenerationService, _blockchainService, _blockExecutingService, _consensusService,
                _blockchainExecutingService);
        }

        private Address GetAddress(ECKeyPair keyPair)
        {
            return Address.FromPublicKey(keyPair.PublicKey);
        }

        private Transaction[] GetGenesisTransactions(int chainId, params Type[] contractTypes)
        {
            return contractTypes.Select(contractType => GetTransactionForDeployment(chainId, contractType)).ToArray();
        }

        private Transaction GetTransactionForDeployment(int chainId, Type contractType)
        {
            var zeroAddress = Address.BuildContractAddress(chainId, 0);

            var code = File.ReadAllBytes(contractType.Assembly.Location);
            return new Transaction
            {
                From = zeroAddress,
                To = zeroAddress,
                MethodName = nameof(ISmartContractZero.DeploySmartContract),
                Params = ByteString.CopyFrom(ParamsPacker.Pack(2, code))
            };
        }

        private Address GetContractAddress(ulong serialNumber)
        {
            return Address.BuildContractAddress(ChainHelpers.ConvertBase58ToChainId("AELF"), serialNumber);
        }
    }
}