using System.Threading.Tasks;
using Acs3;
using Acs7;
using AElf.Contracts.CrossChain;
using AElf.Contracts.MultiToken.Messages;
using AElf.Contracts.ParliamentAuth;
using AElf.Cryptography;
using AElf.Kernel;
using AElf.Sdk.CSharp;
using AElf.Types;
using Google.Protobuf;
using Google.Protobuf.Collections;
using Google.Protobuf.WellKnownTypes;
using Xunit;

namespace AElf.Contract.CrossChain.Tests
{
    public class SideChainLifeTimeManagementTest : CrossChainContractTestBase
    {
        [Fact]
        public async Task Create_SideChain()
        {
            await InitializeCrossChainContractAsync();
            long lockedTokenAmount = 10;
            await ApproveBalanceAsync(lockedTokenAmount);
            
            // Create proposal and approve
            var proposalId = await CreateSideChainProposalAsync(1, lockedTokenAmount, ByteString.CopyFromUtf8("Test"));
            await ApproveWithMinersAsync(proposalId);

            // release proposal
            var transactionResult = await ReleaseProposalAsync(proposalId);
            var chainId = CreationRequested.Parser.ParseFrom(transactionResult.Logs[0].NonIndexed).ChainId;
            var creator = CreationRequested.Parser.ParseFrom(transactionResult.Logs[0].NonIndexed).Creator;
            Assert.True(creator == Tester.GetCallOwnerAddress());

            var chainStatus = SInt32Value.Parser.ParseFrom(await CallContractMethodAsync(CrossChainContractAddress,
                nameof(CrossChainContractContainer.CrossChainContractStub.GetChainStatus),
                new SInt32Value {Value = chainId})).Value;
            Assert.True(chainStatus == (int) SideChainStatus.Active);
        }

        [Fact]
        public async Task Create_SideChainWithDiffSymbol()
        {
            await InitializeCrossChainContractAsync();
            const long lockedAmount = 10;
            await ApproveBalanceAsync(lockedAmount);
            await CreateAndIssueTokenAsync("CPU", "NET","RAM","STO");

            var resourceTypeBalancePairs = new RepeatedField<ResourceTypeBalancePair>
            {
                new ResourceTypeBalancePair {Type = "CPU", Amount = 4},
                new ResourceTypeBalancePair {Type = "NET", Amount = 9},
                new ResourceTypeBalancePair {Type = "RAM", Amount = 20},
                new ResourceTypeBalancePair {Type = "STO", Amount = 30}
            };

            var proposalId = await CreateSideChainProposalAsync(
                1,
                lockedAmount,
                ByteString.CopyFromUtf8("Test"),
                resourceTypeBalancePairs);
            await ApproveWithMinersAsync(proposalId);

            // release proposal
            var transactionResult = await ReleaseProposalAsync(proposalId);
            var chainId = CreationRequested.Parser.ParseFrom(transactionResult.Logs[0].NonIndexed).ChainId;
            var creator = CreationRequested.Parser.ParseFrom(transactionResult.Logs[0].NonIndexed).Creator;
            Assert.True(creator == Tester.GetCallOwnerAddress());

            var chainStatus = SInt32Value.Parser.ParseFrom(await CallContractMethodAsync(CrossChainContractAddress,
                nameof(CrossChainContractContainer.CrossChainContractStub.GetChainStatus),
                new SInt32Value {Value = chainId})).Value;
            Assert.True(chainStatus == (int) SideChainStatus.Active);

            var lockedResource = ResourceTypeBalancePairList.Parser.ParseFrom(await CallContractMethodAsync(CrossChainContractAddress,
                nameof(CrossChainContractContainer.CrossChainContractStub.LockedResource),
                new SInt32Value {Value = chainId}));
            var list = lockedResource.ResourcePairList;
            
            Assert.True(list[0].Type == "CPU");
            Assert.True(list[1].Type == "NET");
            Assert.True(list[2].Type == "RAM");
            Assert.True(list[3].Type == "STO");
            Assert.Equal(4, list[0].Amount);
            Assert.Equal(9, list[1].Amount);
            Assert.Equal(20, list[2].Amount);
            Assert.Equal(30, list[3].Amount);
        }

        [Fact]
        public async Task Create_SideChainWith_NotExisted_Symbol()
        {
            await InitializeCrossChainContractAsync();
            const long lockedAmount = 10;
            await ApproveBalanceAsync(lockedAmount);
            await CreateAndIssueTokenAsync("CPU");

            var resourceTypeBalanceList = new RepeatedField<ResourceTypeBalancePair>
            {
                new ResourceTypeBalancePair {Type = "CPU", Amount = 4},
                new ResourceTypeBalancePair {Type = "NET", Amount = 9},
                new ResourceTypeBalancePair {Type = "STO", Amount = 20}
            };

            var proposalId = await CreateSideChainProposalAsync(
                1,
                lockedAmount,
                ByteString.CopyFromUtf8("Test"),
                resourceTypeBalanceList);
            await ApproveWithMinersAsync(proposalId);

            // release proposal
            var transactionResult = await ReleaseProposalAsync(proposalId);
            Assert.Contains("Not in white", transactionResult.Error);
            Assert.True(transactionResult.Status == TransactionResultStatus.Failed);
            
        }

        [Fact]
        public async Task Create_SideChain_Failed()
        {
            await InitializeCrossChainContractAsync();
            long lockedTokenAmount = 10;
            await ApproveBalanceAsync(lockedTokenAmount);

            {
                var proposalId =
                    await CreateSideChainProposalAsync(10, lockedTokenAmount, ByteString.CopyFromUtf8("Test"));
                await ApproveWithMinersAsync(proposalId);

                var transactionResult = await Tester.ExecuteContractWithMiningAsync(ParliamentAddress,
                    nameof(ParliamentAuthContractContainer.ParliamentAuthContractStub.Release), proposalId);
                var status = transactionResult.Status;
                Assert.True(status == TransactionResultStatus.Failed);
                Assert.Contains("Invalid chain creation request.", transactionResult.Error);
            }

            {
                var proposalId = await CreateSideChainProposalAsync(10, 0, ByteString.CopyFromUtf8("Test"));
                await ApproveWithMinersAsync(proposalId);

                var transactionResult = await Tester.ExecuteContractWithMiningAsync(ParliamentAddress,
                    nameof(ParliamentAuthContractContainer.ParliamentAuthContractStub.Release), proposalId);
                var status = transactionResult.Status;
                Assert.True(status == TransactionResultStatus.Failed);
                Assert.Contains("Invalid chain creation request.", transactionResult.Error);
            }
            {
                var proposalId = await CreateSideChainProposalAsync(1, lockedTokenAmount, ByteString.Empty);
                await ApproveWithMinersAsync(proposalId);

                var transactionResult = await Tester.ExecuteContractWithMiningAsync(ParliamentAddress,
                    nameof(ParliamentAuthContractContainer.ParliamentAuthContractStub.Release), proposalId);
                var status = transactionResult.Status;
                Assert.True(status == TransactionResultStatus.Failed);
                Assert.Contains("Invalid chain creation request.", transactionResult.Error);
            }
        }

        [Fact]
        public async Task Create_SideChain_NotAuthorized()
        {
            long lockedTokenAmount = 10;
            await InitializeCrossChainContractAsync();
            await ApproveBalanceAsync(lockedTokenAmount);

            var chainId = ChainHelper.GetChainId(5);
            var transactionResult =
                await ExecuteContractWithMiningAsync(CrossChainContractAddress,
                    nameof(CrossChainContractContainer.CrossChainContractStub.CreateSideChain),
                    new SInt32Value()
                    {
                        Value = chainId
                    });
            var status = transactionResult.Status;
            Assert.True(status == TransactionResultStatus.Failed);
            Assert.Contains("Not authorized to do this.", transactionResult.Error);
        }

        [Fact]
        public async Task CheckLockedBalance()
        {
            await InitializeCrossChainContractAsync();
            long lockedTokenAmount = 10;
            await ApproveBalanceAsync(lockedTokenAmount);

            var proposalId = await CreateSideChainProposalAsync(1, lockedTokenAmount, ByteString.CopyFromUtf8("Test"));
            await ApproveWithMinersAsync(proposalId);
            var transactionResult = await ReleaseProposalAsync(proposalId);
            var chainId = CreationRequested.Parser.ParseFrom(transactionResult.Logs[0].NonIndexed).ChainId;

            var balance = SInt64Value.Parser.ParseFrom(await CallContractMethodAsync(CrossChainContractAddress,
                nameof(CrossChainContractContainer.CrossChainContractStub.LockedBalance),
                new SInt32Value()
                {
                    Value = chainId
                })).Value;
            Assert.Equal(10, balance);
        }

        [Fact]
        public async Task CheckLockedBalance_NotExist()
        {
            var chainId = ChainHelper.GetChainId(1);
            var txResult = await Tester.ExecuteContractWithMiningAsync(CrossChainContractAddress,
                nameof(CrossChainContractContainer.CrossChainContractStub.LockedBalance),
                new SInt32Value()
                {
                    Value = chainId
                });
            var status = txResult.Status;
            Assert.True(status == TransactionResultStatus.Failed);
            Assert.Contains("Not existed side chain.", txResult.Error);
        }

        [Fact]
        public async Task CheckLockedResource_NotExist()
        {
            var chainId = ChainHelper.GetChainId(5);
            var txResult = await Tester.ExecuteContractWithMiningAsync(CrossChainContractAddress,
                nameof(CrossChainContractContainer.CrossChainContractStub.LockedResource),
                new SInt32Value() {Value = chainId});
            var status = txResult.Status;
            Assert.True(status == TransactionResultStatus.Failed);
            Assert.Contains("Not existed side chain.", txResult.Error);
        }

        [Fact]
        public async Task CheckLockedBalance_NotAuthorized()
        {
            await InitializeCrossChainContractAsync();
            long lockedTokenAmount = 10;
            await ApproveBalanceAsync(lockedTokenAmount);

            var proposalId = await CreateSideChainProposalAsync(1, lockedTokenAmount, ByteString.CopyFromUtf8("Test"));
            await ApproveWithMinersAsync(proposalId);
            var transactionResult = await ReleaseProposalAsync(proposalId);
            var chainId = CreationRequested.Parser.ParseFrom(transactionResult.Logs[0].NonIndexed).ChainId;

            var ecKeyPair = CryptoHelper.GenerateKeyPair();
            var other = Tester.CreateNewContractTester(ecKeyPair);
            var txResult = await other.ExecuteContractWithMiningAsync(CrossChainContractAddress,
                nameof(CrossChainContractContainer.CrossChainContractStub.LockedBalance),
                new SInt32Value()
                {
                    Value = chainId
                });
            var status = txResult.Status;
            Assert.True(status == TransactionResultStatus.Failed);
            Assert.Contains("Unable to check balance.", txResult.Error);
        }

        [Fact]
        public async Task Disposal_SideChain()
        {
            long lockedTokenAmount = 10;
            await InitializeCrossChainContractAsync();
            await ApproveBalanceAsync(lockedTokenAmount);
            var chainId = await InitAndCreateSideChainAsync();
            var proposalId = await DisposalSideChainProposalAsync(new SInt32Value
            {
                Value = chainId
            });
            await ApproveWithMinersAsync(proposalId);
            var transactionResult = await ReleaseProposalAsync(proposalId);
            var status = transactionResult.Status;
            Assert.True(status == TransactionResultStatus.Mined);

            var chainStatus = SInt32Value.Parser.ParseFrom(await CallContractMethodAsync(CrossChainContractAddress,
                nameof(CrossChainContractContainer.CrossChainContractStub.GetChainStatus),
                new SInt32Value {Value = chainId})).Value;
            Assert.True(chainStatus == (int) SideChainStatus.Terminated);
        }

        [Fact]
        public async Task Disposal_SideChainWithResource()
        {
            long lockedTokenAmount = 10;
            await InitializeCrossChainContractAsync();
            await ApproveBalanceAsync(lockedTokenAmount);
            await CreateAndIssueTokenAsync( "CPU", "NET");
            
            var resourceTypeBalance = new RepeatedField<ResourceTypeBalancePair>
            {
                new ResourceTypeBalancePair {Type = "CPU", Amount = 4},
                new ResourceTypeBalancePair {Type = "NET", Amount = 2}
            };
            var chainId = await InitAndCreateSideChainAsync(resourceTypeBalancePairs:resourceTypeBalance);
            var proposalId = await DisposalSideChainProposalAsync(new SInt32Value
            {
                Value = chainId
            });
            await ApproveWithMinersAsync(proposalId);
            var transactionResult = await ReleaseProposalAsync(proposalId);
            var status = transactionResult.Status;
            Assert.True(status == TransactionResultStatus.Mined);
            
            var chainStatus = SInt32Value.Parser.ParseFrom(await CallContractMethodAsync(CrossChainContractAddress,
                nameof(CrossChainContractContainer.CrossChainContractStub.GetChainStatus),
                new SInt32Value {Value = chainId})).Value;
            Assert.True(chainStatus == (int) SideChainStatus.Terminated);
        }

        [Fact]
        public async Task Disposal_SideChain_NotAuthorized()
        {
            long lockedTokenAmount = 10;
            await InitializeCrossChainContractAsync();
            await ApproveBalanceAsync(lockedTokenAmount);
            var chainId = await InitAndCreateSideChainAsync();
            var disposalInput = new SInt32Value
            {
                Value = chainId
            };

            var ecKeyPair = CryptoHelper.GenerateKeyPair();
            var other = Tester.CreateNewContractTester(ecKeyPair);
            var organizationAddress = Address.Parser.ParseFrom((await Tester.ExecuteContractWithMiningAsync(
                    ParliamentAddress,
                    nameof(ParliamentAuthContractContainer.ParliamentAuthContractStub.GetGenesisOwnerAddress),
                    new Empty()))
                .ReturnValue);
            var proposal = await other.ExecuteContractWithMiningAsync(ParliamentAddress,
                nameof(ParliamentAuthContractContainer.ParliamentAuthContractStub.CreateProposal),
                new CreateProposalInput
                {
                    ContractMethodName = "DisposeSideChain",
                    ExpiredTime = TimestampHelper.GetUtcNow().AddDays(1),
                    Params = disposalInput.ToByteString(),
                    ToAddress = CrossChainContractAddress,
                    OrganizationAddress = organizationAddress
                });
            var proposalId = Hash.Parser.ParseFrom(proposal.ReturnValue);
            await ApproveWithMinersAsync(proposalId);
            var transactionResult = await other.ExecuteContractWithMiningAsync(ParliamentAddress,
                nameof(ParliamentAuthContractContainer.ParliamentAuthContractStub.Release), proposalId);
            var status = transactionResult.Status;
            Assert.True(status == TransactionResultStatus.Failed);
            Assert.Contains("Not authorized to dispose.", transactionResult.Error);
        }

        [Fact]
        public async Task Disposal_SideChain__NotFound()
        {
            await InitializeCrossChainContractAsync();
            var chainId = ChainHelper.GetChainId(1);
            var proposalId = await DisposalSideChainProposalAsync(new SInt32Value
            {
                Value = chainId
            });
            await ApproveWithMinersAsync(proposalId);
            var transactionResult = await ReleaseProposalAsync(proposalId);
            var status = transactionResult.Status;

            Assert.True(status == TransactionResultStatus.Failed);
            Assert.Contains("Not existed side chain.", transactionResult.Error);
        }

        [Fact]
        public async Task Disposal_SideChain_WrongStatus()
        {
            long lockedTokenAmount = 10;
            await InitializeCrossChainContractAsync();
            await ApproveBalanceAsync(lockedTokenAmount);
            var chainId = await InitAndCreateSideChainAsync();
            var proposalId1 = await DisposalSideChainProposalAsync(new SInt32Value
            {
                Value = chainId
            });
            await ApproveWithMinersAsync(proposalId1);
            var transactionResult1 = await ReleaseProposalAsync(proposalId1);
            var status1 = transactionResult1.Status;
            Assert.True(status1 == TransactionResultStatus.Mined);

            var proposalId2 = await DisposalSideChainProposalAsync(new SInt32Value
            {
                Value = chainId
            });
            await ApproveWithMinersAsync(proposalId2);
            var transactionResult2 = await ReleaseProposalAsync(proposalId2);
            var status2 = transactionResult2.Status;
            Assert.True(status2 == TransactionResultStatus.Failed);
            Assert.Contains("Unable to dispose this side chain.", transactionResult2.Error);
        }

        [Fact]
        public async Task GetChainStatus_NotExist()
        {
            var chainId = ChainHelper.GetChainId(1);
            var txResult = await Tester.ExecuteContractWithMiningAsync(CrossChainContractAddress,
                nameof(CrossChainContractContainer.CrossChainContractStub.GetChainStatus),
                new SInt32Value()
                {
                    Value = chainId
                });
            var status = txResult.Status;
            Assert.True(status == TransactionResultStatus.Failed);
            Assert.Contains("Not existed side chain.", txResult.Error);
        }

        [Fact]
        public async Task Get_SideChain_Height()
        {
            await InitializeCrossChainContractAsync();
            long lockedTokenAmount = 10;
            await ApproveBalanceAsync(lockedTokenAmount);

            var proposalId = await CreateSideChainProposalAsync(1, lockedTokenAmount, ByteString.CopyFromUtf8("Test"));
            await ApproveWithMinersAsync(proposalId);
            var transactionResult = await ReleaseProposalAsync(proposalId);
            var chainId = CreationRequested.Parser.ParseFrom(transactionResult.Logs[0].NonIndexed).ChainId;

            var transactionResult1 = await Tester.ExecuteContractWithMiningAsync(CrossChainContractAddress,
                nameof(CrossChainContractContainer.CrossChainContractStub.GetSideChainHeight),
                new SInt32Value()
                {
                    Value = chainId
                });
            var status = transactionResult1.Status;
            Assert.True(status == TransactionResultStatus.Mined);
            var actual = new SInt32Value();
            actual.MergeFrom(transactionResult.ReturnValue);
            Assert.Equal(0, actual.Value);
        }

        [Fact]
        public async Task Get_SideChain_Height_NotExist()
        {
            await InitializeCrossChainContractAsync();
            long lockedTokenAmount = 10;
            await ApproveBalanceAsync(lockedTokenAmount);

            var chainId = ChainHelper.GetChainId(1);
            var transactionResult = await Tester.ExecuteContractWithMiningAsync(CrossChainContractAddress,
                nameof(CrossChainContractContainer.CrossChainContractStub.GetSideChainHeight),
                new SInt32Value()
                {
                    Value = chainId
                });
            var status = transactionResult.Status;
            Assert.True(status == TransactionResultStatus.Failed);
            Assert.Contains("Side chain not found.", transactionResult.Error);
        }
    }
}