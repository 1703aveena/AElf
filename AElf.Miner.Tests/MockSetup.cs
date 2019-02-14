using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using AElf.ChainController;
using AElf.ChainController.CrossChain;
using AElf.Configuration;
using AElf.Configuration.Config.GRPC;
using AElf.Cryptography.Certificate;
using AElf.Kernel;
using AElf.Runtime.CSharp;
using AElf.SmartContract;
using Google.Protobuf;
using Moq;
using AElf.Common;
using AElf.Crosschain.Grpc.Client;
using AElf.Crosschain.Grpc.Server;
using AElf.Execution.Execution;
using AElf.Kernel.Account;
using AElf.Kernel.Consensus;
using AElf.Kernel.Managers;
using AElf.Kernel.Services;
using AElf.SmartContract.Consensus;
using AElf.SmartContract.Contexts;
using AElf.SmartContract.Proposal;
using AElf.Synchronization.BlockExecution;
using AElf.Synchronization.BlockSynchronization;
using AElf.TxPool;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Volo.Abp.DependencyInjection;

namespace AElf.Miner.Tests
{
    public class MockSetup : ITransientDependency
    {
        private List<IBlockHeader> _headers = new List<IBlockHeader>();
        private List<IBlockHeader> _sideChainHeaders = new List<IBlockHeader>();
        private List<IBlock> _blocks = new List<IBlock>();
        public ILogger<MockSetup> Logger { get; set; }
        private ulong _i = 0;
        private IChainCreationService _chainCreationService;
        private ITransactionManager _transactionManager;
        private ITransactionReceiptManager _transactionReceiptManager;
        private ITransactionResultManager _transactionResultManager;
        private IExecutingService _concurrencyExecutingService;
        private IChainService _chainService;
        private IBinaryMerkleTreeManager _binaryMerkleTreeManager;
        private ITxRefBlockValidator _refBlockValidator;
        private IAuthorizationInfoReader _authorizationInfoReader;
        private IElectionInfo _electionInfo;
        private IStateManager _stateManager;
        private readonly ITransactionFilter _transactionFilter;
        private readonly ConsensusDataProvider _consensusDataProvider;
        private readonly IAccountService _accountService;
        private readonly IOptionsSnapshot<ChainOptions> _chainOptions;
        private readonly IBlockchainStateManager _blockchainStateManager;
        private readonly ISystemTransactionGenerationService _systemTransactionGenerationService;
        private readonly IBlockGenerationService _blockGenerationService;
        private readonly ITransactionTypeIdentificationService _transactionTypeIdentificationService;

        public MockSetup(IStateManager stateManager,
            ITxRefBlockValidator refBlockValidator,
            ITransactionReceiptManager transactionReceiptManager, ITransactionResultManager transactionResultManager,
            ITransactionManager transactionManager, IBinaryMerkleTreeManager binaryMerkleTreeManager,
            IChainService chainService, IExecutingService executingService,
            IChainCreationService chainCreationService, ITransactionFilter transactionFilter, 
            ConsensusDataProvider consensusDataProvider, IAccountService accountService, 
            IBlockchainStateManager blockchainStateManager, 
            ISystemTransactionGenerationService systemTransactionGenerationService, 
            IBlockGenerationService blockGenerationService,
            IOptionsSnapshot<ChainOptions> options, ITransactionTypeIdentificationService transactionTypeIdentificationService)
        {
            Logger = NullLogger<MockSetup>.Instance;
            _stateManager = stateManager;
            _refBlockValidator = refBlockValidator;
            _transactionReceiptManager = transactionReceiptManager;
            _transactionResultManager = transactionResultManager;
            _transactionManager = transactionManager;
            _stateManager = stateManager;
            _binaryMerkleTreeManager = binaryMerkleTreeManager;
            _chainService = chainService;
            _concurrencyExecutingService = executingService;
            _chainCreationService = chainCreationService;
            _transactionFilter = transactionFilter;
            _consensusDataProvider = consensusDataProvider;
            _accountService = accountService;
            _chainOptions = options;
            _transactionTypeIdentificationService = transactionTypeIdentificationService;
            _blockchainStateManager = blockchainStateManager;
            _systemTransactionGenerationService = systemTransactionGenerationService;
            _blockGenerationService = blockGenerationService;
            Initialize();
        }

        private void Initialize()
        {
            _authorizationInfoReader = new AuthorizationInfoReader(_stateManager);
            _electionInfo = new ElectionInfo(_stateManager);
        }

        private byte[] SmartContractZeroCode => ContractCodes.TestContractZeroCode;

        public byte[] CrossChainCode
        {
            get
            {
                var filePath =
                    Path.GetFullPath(
                        "../../../../AElf.Contracts.CrossChain/bin/Debug/netstandard2.0/AElf.Contracts.CrossChain.dll");
                return File.ReadAllBytes(filePath);
            }
        }

        public async Task<IChain> CreateChain()
        {
            var chainId = ChainHelpers.GetRandomChainId();

            var reg = new SmartContractRegistration
            {
                Category = 0,
                ContractBytes = ByteString.CopyFrom(SmartContractZeroCode),
                ContractHash = Hash.FromRawBytes(SmartContractZeroCode)
            };
            var reg1 = new SmartContractRegistration
            {
                Category = 0,
                ContractBytes = ByteString.CopyFrom(CrossChainCode),
                ContractHash = Hash.FromRawBytes(CrossChainCode),
                SerialNumber = GlobalConfig.CrossChainContract
            };

            var chain = await _chainCreationService.CreateNewChainAsync(chainId,
                new List<SmartContractRegistration> {reg, reg1});
            return chain;
        }

        internal IMinerService GetMiner(ITxHub hub, GrpcClientService clientService = null)
        {
            var miner = new MinerService(hub, _chainService, _concurrencyExecutingService,
                _transactionResultManager, _binaryMerkleTreeManager, null,
                _accountService, _blockGenerationService, _systemTransactionGenerationService, _blockchainStateManager);

            return miner;
        }

        internal IBlockExecutor GetBlockExecutor(GrpcClientService grpcClientService = null)
        {
            var blockExecutor = new BlockExecutor(_chainService, _concurrencyExecutingService,
                _transactionResultManager,  _binaryMerkleTreeManager,
                new TxHub(_transactionManager, _transactionReceiptManager, _chainService, _authorizationInfoReader, _refBlockValidator, _electionInfo, _transactionTypeIdentificationService), _stateManager,
                _consensusDataProvider);

            return blockExecutor;
        }

        internal IBlockChain GetBlockChain(int chainId)
        {
            return _chainService.GetBlockChain(chainId);
        }

        internal ITxHub CreateAndInitTxHub()
        {
            var hub = new TxHub(_transactionManager, _transactionReceiptManager, _chainService, _authorizationInfoReader, _refBlockValidator, _electionInfo, _transactionTypeIdentificationService);
            hub.Initialize(_chainOptions.Value.ChainId.ConvertBase58ToChainId());
            return hub;
        }

        private Mock<ILightChain> MockLightChain()
        {
            Mock<ILightChain> mock = new Mock<ILightChain>();
            mock.Setup(lc => lc.GetCurrentBlockHeightAsync())
                .Returns(Task.FromResult((ulong) _headers.Count - 1 + GlobalConfig.GenesisBlockHeight));
            mock.Setup(lc => lc.GetHeaderByHeightAsync(It.IsAny<ulong>()))
                .Returns<ulong>(p =>
                {
                    return (int) p > _sideChainHeaders.Count
                        ? null
                        : Task.FromResult(_sideChainHeaders[(int) p - 1]);
                });

            return mock;
        }

        private Mock<IBlockChain> MockBlockChain()
        {
            Mock<IBlockChain> mock = new Mock<IBlockChain>();
            mock.Setup(bc => bc.GetBlockByHeightAsync(It.IsAny<ulong>(), It.IsAny<bool>()))
                .Returns<ulong, bool>((p, w) => Task.FromResult(_blocks[(int) p - 1]));
            return mock;
        }

        private Mock<IChainService> MockChainService()
        {
            Mock<IChainService> mock = new Mock<IChainService>();
            mock.Setup(cs => cs.GetLightChain(It.IsAny<int>())).Returns(MockLightChain().Object);
            mock.Setup(cs => cs.GetBlockChain(It.IsAny<int>())).Returns(MockBlockChain().Object);
            return mock;
        }

        private IBlockHeader MockBlockHeader()
        {
            return new BlockHeader
            {
                MerkleTreeRootOfTransactions = Hash.Generate(),
                ChainId = ChainHelpers.GetRandomChainId(),
                PreviousBlockHash = Hash.Generate(),
                MerkleTreeRootOfWorldState = Hash.Generate()
            };
        }

        private IBlockBody MockBlockBody(ulong height, int? chainId = null)
        {
            return new BlockBody
            {
                //IndexedInfo = { MockSideChainBlockInfo(height, chainId)}
            };
        }

        private SideChainBlockData MockSideChainBlockInfo(ulong height, int? chainId = null)
        {
            return new SideChainBlockData
            {
                Height = height,
                ChainId = chainId ?? ChainHelpers.GetRandomChainId(),
                TransactionMKRoot = Hash.Generate(),
                BlockHeaderHash = Hash.Generate()
            };
        }

        public Mock<IBlock> MockBlock(IBlockHeader header, IBlockBody body)
        {
            Mock<IBlock> mock = new Mock<IBlock>();
            mock.Setup(b => b.Header).Returns((BlockHeader) header);
            mock.Setup(b => b.Body).Returns((BlockBody) body);
            return mock;
        }

        public ParentChainBlockInfoRpcServer MockParentChainBlockInfoRpcServer()
        {
            return new ParentChainBlockInfoRpcServer(MockChainService().Object);
        }

        public SideChainBlockInfoRpcServer MockSideChainBlockInfoRpcServer()
        {
            return new SideChainBlockInfoRpcServer(MockChainService().Object);
        }

        public ServerManager ServerManager(ParentChainBlockInfoRpcServer impl1, SideChainBlockInfoRpcServer impl2)
        {
            return new ServerManager(impl1, impl2);
        }

        public Mock<IChainManager> MockChainManager()
        {
            var mock = new Mock<IChainManager>();
            mock.Setup(cm => cm.GetCurrentBlockHeightAsync(It.IsAny<int>())).Returns(() =>
            {
                var k = _i;
                return Task.FromResult(k);
            });
            mock.Setup(cm => cm.UpdateCurrentBlockHeightAsync(It.IsAny<int>(), It.IsAny<ulong>()))
                .Returns<Hash, ulong>((h, u) =>
                {
                    _i = u;
                    return Task.CompletedTask;
                });
            return mock;
        }

        public GrpcClientService MinerClientService()
        {
            return new GrpcClientService();
        }

        public ulong GetTimes = 0;

        private Mock<ICrossChainInfoReader> MockCrossChainInfoReader()
        {
            var mock = new Mock<ICrossChainInfoReader>();
            mock.Setup(m => m.GetParentChainCurrentHeightAsync(It.IsAny<int>())).Returns(() => Task.FromResult
            (GetTimes));
            /*mock.Setup(m => m.GetMerkleTreeForSideChainTransactionRootAsync(It.IsAny<ulong>())).Returns<ulong>(u =>
            {
                var binaryMerkleTree = new BinaryMerkleTree();
                binaryMerkleTree.AddNodes(_blocks[(int) u - 1].Body.IndexedInfo.Select(info => info.TransactionMKRoot));
                Console.WriteLine($"merkle tree root for {u} : {binaryMerkleTree.ComputeRootHash()}");
                return Task.FromResult(binaryMerkleTree);
            });*/
            mock.Setup(m => m.GetSideChainCurrentHeightAsync(It.IsAny<int>(),It.IsAny<int>()))
                .Returns(() => Task.FromResult(GetTimes));
            return mock;
        }

        public void MockKeyPair(int chainId, string dir)
        {
            var certificateStore = new CertificateStore(dir);
            var name = chainId.DumpBase58();
            var keyPair = certificateStore.WriteKeyAndCertificate(name, "127.0.0.1");
        }

        public int MockSideChainServer(int port, string address, string dir)
        {
            _sideChainHeaders = new List<IBlockHeader>
            {
                MockBlockHeader(),
                MockBlockHeader(),
                MockBlockHeader()
            };

            var sideChainId = ChainHelpers.GetRandomChainId();

            MockKeyPair(sideChainId, dir);
            GrpcLocalConfig.Instance.LocalSideChainServerPort = port;
            GrpcLocalConfig.Instance.LocalServerIP = address;
            GrpcLocalConfig.Instance.SideChainServer = true;
            //start server, sidechain is server-side

            return sideChainId;
        }

        public int MockParentChainServer(int port, string address, string dir, int? chainId = 0)
        {
            chainId = chainId ?? ChainHelpers.GetRandomChainId();

            _headers = new List<IBlockHeader>
            {
                MockBlockHeader(),
                MockBlockHeader(),
                MockBlockHeader()
            };
            //IBlockHeader blockHeader = Headers[0];
            _blocks = new List<IBlock>
            {
                MockBlock(_headers[0], MockBlockBody(GlobalConfig.GenesisBlockHeight, chainId)).Object,
                MockBlock(_headers[1], MockBlockBody(GlobalConfig.GenesisBlockHeight + 1, chainId)).Object,
                MockBlock(_headers[2], MockBlockBody(GlobalConfig.GenesisBlockHeight + 2, chainId)).Object
            };

            MockKeyPair(chainId.Value, dir);
            GrpcLocalConfig.Instance.LocalParentChainServerPort = port;
            GrpcLocalConfig.Instance.LocalServerIP = address;
            GrpcLocalConfig.Instance.ParentChainServer = true;

            return chainId.Value;
        }

        private Mock<IBlockValidationService> MockBlockValidationService()
        {
            var mock = new Mock<IBlockValidationService>();
            mock.Setup(bvs => bvs.ValidateBlockAsync(It.IsAny<IBlock>()))
                .Returns(() => Task.FromResult(BlockValidationResult.Success));
            return mock;
        }

        public void ClearDirectory(string dir)
        {
            if (Directory.Exists(Path.Combine(dir, "certs")))
                Directory.Delete(Path.Combine(dir, "certs"), true);
        }
    }
}