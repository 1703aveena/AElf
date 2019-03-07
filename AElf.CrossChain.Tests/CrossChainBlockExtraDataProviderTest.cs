using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using AElf.Common;
using AElf.Kernel;
using AElf.Kernel.Types;
using AElf.Types.CSharp;
using Google.Protobuf;
using Xunit;

namespace AElf.CrossChain
{
    public class CrossChainBlockExtraDataProviderTest : CrossChainTestBase
    {
        private LogEvent CreateCrossChainLogEvent(byte[] topic, byte[] data)
        {
            return new LogEvent
            {
                Address = ContractHelpers.GetCrossChainContractAddress(0),
                Topics =
                {
                    ByteString.CopyFrom(topic)
                },
                Data = ByteString.CopyFrom(data)
            };
        }

        private TransactionResult CreateFakeTransactionResult(Hash txId, IEnumerable<LogEvent> logEvents)
        {
            return new TransactionResult
            {
                TransactionId = txId,
                Logs = { logEvents }
            };
        }
        // TODO: Recover test cases of cross chain extra data provider.
        /*
        [Fact]
        public async Task FillExtraData_NoEvent()
        {
            var block = new Block
            {
                Header = new BlockHeader(),
                Body = new BlockBody()
            };
            var fakeTransactionResultGettingService = TransactionResultQueryService;
            //    CrossChainTestHelper.FakeTransactionResultManager(new List<TransactionResult>());
            var crossChainBlockExtraDataProvider = new CrossChainBlockExtraDataProvider(fakeTransactionResultGettingService);
            await crossChainBlockExtraDataProvider.FillExtraDataAsync(block.Header);
            Assert.Null(block.Header.BlockExtraDatas);
        }
        
        [Fact]
        public async Task FillExtraData_NotFoundEvent()
        {            
            Hash txId1 = Hash.FromString("tx1");
            var txRes1 =
                CreateFakeTransactionResult(txId1, new []{CreateCrossChainLogEvent(new byte[0], new byte[0])});
            txRes1.UpdateBloom();
            
            var block = new Block
            {
                Header = new BlockHeader
                {
                    Bloom = ByteString.CopyFrom(Bloom.AndMultipleBloomBytes(new[] {txRes1.Bloom.ToByteArray()}))
                },
                Body = new BlockBody()
            };
            block.Body.Transactions.AddRange(new []{ txId1});
            await TransactionResultService.AddTransactionResultAsync(txRes1, block.Header);
            var crossChainBlockExtraDataProvider = new CrossChainBlockExtraDataProvider(TransactionResultQueryService);
            await crossChainBlockExtraDataProvider.FillExtraDataAsync(block.Header);
            Assert.Null(block.Header.BlockExtraDatas);
        }
        
        [Fact]
        public async Task FillExtraData_OneEvent()
        {

            var fakeMerkleTreeRoot = Hash.FromString("SideChainTransactionsMerkleTreeRoot");
            var publicKey = CrossChainTestHelper.GetPubicKey();
            var data = ParamsPacker.Pack(fakeMerkleTreeRoot, new CrossChainBlockData(),
                Address.FromPublicKey(publicKey));
            var logEvent =
                CreateCrossChainLogEvent(CrossChainConsts.CrossChainIndexingEventName.CalculateHash(), data);
            
            Hash txId1 = Hash.FromString("tx1");
            var txRes1 = CreateFakeTransactionResult(txId1, new []{logEvent});
            txRes1.UpdateBloom();
            Hash txId2 = Hash.FromString("tx2");
            var txRes2 = CreateFakeTransactionResult(txId2,
                new[] {CreateCrossChainLogEvent(new byte[0], new byte[0])});
            
            Hash txId3 = Hash.FromString("tx3");
            var txRes3 = CreateFakeTransactionResult(txId3,
                new[] {CreateCrossChainLogEvent(new byte[0], new byte[0])});
            
            var block = new Block
            {
                Header = new BlockHeader
                {
                    Bloom = ByteString.CopyFrom(Bloom.AndMultipleBloomBytes(new[] {txRes1.Bloom.ToByteArray()}))
                },
                Body = new BlockBody()
            };
            block.Body.Transactions.AddRange(new []{ txId1, txId2, txId3});
            block.Sign(publicKey, b => Task.FromResult(CrossChainTestHelper.Sign(b)));

            await TransactionResultService.AddTransactionResultAsync(txRes1, block.Header);
            await TransactionResultService.AddTransactionResultAsync(txRes2, block.Header);
            await TransactionResultService.AddTransactionResultAsync(txRes3, block.Header);
            var crossChainBlockExtraDataProvider = new CrossChainBlockExtraDataProvider(TransactionResultQueryService);
            await crossChainBlockExtraDataProvider.FillExtraDataAsync(block.Header);
            Assert.Equal(fakeMerkleTreeRoot, Hash.LoadByteArray(block.Header.BlockExtraDatas[0].ToByteArray()));
        }
        
        [Fact]
        public async Task FillExtraData_MultiEventsInOneTransaction()
        {            
            var fakeMerkleTreeRoot = Hash.FromString("SideChainTransactionsMerkleTreeRoot");
            var publicKey = CrossChainTestHelper.GetPubicKey();
            var data = ParamsPacker.Pack(fakeMerkleTreeRoot, new CrossChainBlockData(),
                Address.FromPublicKey(publicKey));
            var interestedLogEvent =
                CreateCrossChainLogEvent(CrossChainConsts.CrossChainIndexingEventName.CalculateHash(), data);
            
            Hash txId = Hash.FromString("tx1");
            var txRes = CreateFakeTransactionResult(txId,
                new[] {CreateCrossChainLogEvent(new byte[0], new byte[0]), interestedLogEvent});
            txRes.UpdateBloom();
            
            var fakeBlock = new Block
            {
                Header = new BlockHeader
                {
                    Bloom = ByteString.CopyFrom(Bloom.AndMultipleBloomBytes(new[] {txRes.Bloom.ToByteArray()}))
                },
                Body = new BlockBody()
            };
            
            fakeBlock.Body.Transactions.AddRange(new []{ txId});
            fakeBlock.Sign(publicKey, b => Task.FromResult(CrossChainTestHelper.Sign(b)));

            await TransactionResultService.AddTransactionResultAsync(txRes, fakeBlock.Header);
            var crossChainBlockExtraDataProvider = new CrossChainBlockExtraDataProvider(TransactionResultQueryService);
            await crossChainBlockExtraDataProvider.FillExtraDataAsync(fakeBlock.Header);
            Assert.Equal(fakeMerkleTreeRoot, Hash.LoadByteArray(fakeBlock.Header.BlockExtraDatas[0].ToByteArray()));
        }
        
        [Fact]
        public async Task FillExtraData_OneEvent_WithWrongData()
        {
            int wrongHash = 123; // which should be Hash type
            var data = ParamsPacker.Pack(wrongHash, new CrossChainBlockData());
            var logEvent =
                CreateCrossChainLogEvent(CrossChainConsts.CrossChainIndexingEventName.CalculateHash(), data);
            
            Hash txId1 = Hash.FromString("tx1");
            var txRes1 = CreateFakeTransactionResult(txId1, new []{logEvent});
            txRes1.UpdateBloom();
            
            var block = new Block
            {
                Header = new BlockHeader
                {
                    Bloom = ByteString.CopyFrom(Bloom.AndMultipleBloomBytes(new[] {txRes1.Bloom.ToByteArray()}))
                },
                Body = new BlockBody()
            };
            block.Body.Transactions.AddRange(new[] {txId1});
            await TransactionResultService.AddTransactionResultAsync(txRes1, block.Header);
            var crossChainBlockExtraDataProvider = new CrossChainBlockExtraDataProvider(TransactionResultQueryService);
            await crossChainBlockExtraDataProvider.FillExtraDataAsync(block.Header);
            Assert.Null(block.Header.BlockExtraDatas);
        }
        */
    }
}