using System.Linq;
using System.Threading.Tasks;
using AElf.Common;
using AElf.Contracts.TestBase;
using AElf.Cryptography;
using AElf.Cryptography.ECDSA;
using AElf.Kernel;
using Shouldly;
using Volo.Abp.Threading;
using Xunit;

namespace AElf.Contracts.Consensus.DPoS
{
    public class ElectionTest
    {
        public readonly ContractTester<DPoSContractTestAElfModule> Starter;
        public ElectionTest()
        {
            // The starter initial chain and tokens.
            Starter = new ContractTester<DPoSContractTestAElfModule>();
            AsyncHelper.RunSync(() => Starter.InitialChainAndTokenAsync());
         }
        
        [Fact]
        public async Task Announce_Election_Success()
        {
            var starterBalance = await Starter.GetBalanceAsync(Starter.GetCallOwnerAddress());
            Assert.Equal(DPoSContractConsts.LockTokenForElection * 100, starterBalance);
            
            // The starter transfer a specific amount of tokens to candidate for further testing.
            var candidateInfo = GenerateNewUser();
            await Starter.TransferTokenAsync(candidateInfo.Item2, DPoSContractConsts.LockTokenForElection);
            var balance = await Starter.GetBalanceAsync(candidateInfo.Item2);
            Assert.Equal(DPoSContractConsts.LockTokenForElection, balance);
            
            // The candidate announce election.
            var candidate = Starter.CreateNewContractTester(candidateInfo.Item1);
            await candidate.AnnounceElectionAsync("AElfin");
            var candidatesList = await candidate.GetCandidatesListAsync();

            // Check the candidates list.
            Assert.Contains(candidate.KeyPair.PublicKey.ToHex(), candidatesList.Values.ToList());
        }

        [Fact]
        public async Task Announce_Election_WithoutEnough_Token()
        {
            // The starter transfer not enough token 
            var candidateInfo = GenerateNewUser();
            await Starter.TransferTokenAsync(candidateInfo.Item2, 50_000UL);
            var balance = await Starter.GetBalanceAsync(candidateInfo.Item2);
            balance.ShouldBe(50_000UL);
            
            // The candidate announce election.
            var candidate = Starter.CreateNewContractTester(candidateInfo.Item1);
            var result = await candidate.AnnounceElectionAsync("AElfin");
            result.Status.ShouldBe(TransactionResultStatus.Failed);
            result.Error.Contains("Insufficient balance").ShouldBeTrue();
            var candidatesList = await candidate.GetCandidatesListAsync();
            candidatesList.Values.ToList().Contains(candidateInfo.Item3).ShouldBeFalse();
        }

        [Fact]
        public async Task Announce_Election_Twice()
        {
            // The starter transfer 200_000UL
            var candidateInfo = GenerateNewUser();
            await Starter.TransferTokenAsync(candidateInfo.Item2, DPoSContractConsts.LockTokenForElection*2);
            var balance = await Starter.GetBalanceAsync(candidateInfo.Item2);
            balance.ShouldBe(DPoSContractConsts.LockTokenForElection*2);
            
            var candidate = Starter.CreateNewContractTester(candidateInfo.Item1);
            //announce election 1
            var result = await candidate.AnnounceElectionAsync("AElfin");
            result.Status.ShouldBe(TransactionResultStatus.Mined);
            
            //announce election 2
            var result1 = await candidate.AnnounceElectionAsync("AElfinAgain");
            result.Status.ShouldBe(TransactionResultStatus.Failed);
            
            balance = await Starter.GetBalanceAsync(candidateInfo.Item2);
            balance.ShouldBe(DPoSContractConsts.LockTokenForElection);
            var candidatesList = await candidate.GetCandidatesListAsync();
            candidatesList.Values.ToList().Contains(candidateInfo.Item3).ShouldBeTrue();
        }

        [Fact]
        public async Task Quit_Election_Success()
        {
            // The starter transfer a specific amount of tokens to candidate for further testing.
            var candidateInfo = GenerateNewUser();
            await Starter.TransferTokenAsync(candidateInfo.Item2, DPoSContractConsts.LockTokenForElection);
            var balance = await Starter.GetBalanceAsync(candidateInfo.Item2);
            Assert.Equal(DPoSContractConsts.LockTokenForElection, balance);
            
            // The candidate announce election.
            var candidate = Starter.CreateNewContractTester(candidateInfo.Item1);
            await candidate.AnnounceElectionAsync("AElfin");
            var candidatesList = await candidate.GetCandidatesListAsync();
            candidatesList.Values.ToList().Count.ShouldBeGreaterThanOrEqualTo(1);
            
            //Quit election
            var result = await candidate.QuitCancelElectionAsync();
            result.Status.ShouldBe(TransactionResultStatus.Mined);
            
            var candidatesList1 = await candidate.GetCandidatesListAsync();
            candidatesList1.Values.Contains(candidateInfo.Item3).ShouldBeFalse();
            
            var balance1 = await Starter.GetBalanceAsync(candidateInfo.Item2);
            balance1.ShouldBe(DPoSContractConsts.LockTokenForElection);
        }

        [Fact]
        public async Task Vote_Candidate_Success()
        {
            const ulong amount = 1000;
            var candidate = (await Starter.GenerateCandidatesAsync(1))[0];
            var voter = Starter.GenerateVoters(1)[0];
            await Starter.TransferTokenAsync(voter.GetCallOwnerAddress(), 10000);

            await voter.Vote(candidate.PublicKey, amount, 100);

            var ticketsOfCandidate = await candidate.GetTicketsInformationAsync();
            Assert.Equal(amount, ticketsOfCandidate.ObtainedTickets);
            
            var ticketsOfVoter = await voter.GetTicketsInformationAsync();
            Assert.Equal(amount, ticketsOfVoter.VotedTickets);
        }

        [Fact]
        public async Task Vote_Not_Candidate()
        {
            const ulong amount = 1000;
            var candidate = (await Starter.GenerateCandidatesAsync(1))[0];
            var voter = Starter.GenerateVoters(1)[0];
            await Starter.TransferTokenAsync(voter.GetCallOwnerAddress(), amount);

            var notCandidate = GenerateNewUser();
            var result = await voter.Vote(notCandidate.Item3, amount, 100);
            result.Status.ShouldBe(TransactionResultStatus.Failed);
            result.Error.Contains("Target didn't announce election.").ShouldBeTrue();
            
            var balance = await Starter.GetBalanceAsync(voter.GetCallOwnerAddress());
            balance.ShouldBe(amount);
        }

        [Fact]
        public async Task Vote_Candidate_Without_Enough_Token()
        {
            const ulong amount = 100;
            const ulong voteAmount = 200;
            var candidate = (await Starter.GenerateCandidatesAsync(1))[0];
            var voter = Starter.GenerateVoters(1)[0];
            await Starter.TransferTokenAsync(voter.GetCallOwnerAddress(), amount);

            var txResult = await voter.Vote(candidate.PublicKey, voteAmount, 100);
            txResult.Status.ShouldBe(TransactionResultStatus.Failed);
            txResult.Error.Contains("Insufficient balance.").ShouldBeTrue();
            
            var ticketsOfVoter = await voter.GetTicketsInformationAsync();
            ticketsOfVoter.VotedTickets.ShouldBe(0UL);
        }

        [Fact]
        public async Task Vote_Same_Candidate_MultipleTimes()
        {
            const ulong amount = 1000;
            const ulong voteAmount = 200;
            var candidate = (await Starter.GenerateCandidatesAsync(1))[0];
            var voter = Starter.GenerateVoters(1)[0];
            await Starter.TransferTokenAsync(voter.GetCallOwnerAddress(), amount);

            for (int i = 0; i < 5; i++)
            {
                var txResult = await voter.Vote(candidate.PublicKey, voteAmount, 100);
                txResult.Status.ShouldBe(TransactionResultStatus.Mined);
            }
            
            var ticketsOfVoter = await voter.GetTicketsInformationAsync();
            ticketsOfVoter.VotedTickets.ShouldBe(1000UL);

            var balance = await Starter.GetBalanceAsync(voter.GetCallOwnerAddress());
            balance.ShouldBe(0UL);
        }

        [Fact]
        public async Task Vote_Different_Candidates()
        {
            
        }
        
        private static (ECKeyPair, Address, string) GenerateNewUser()
        {
            var callKeyPair = CryptoHelpers.GenerateKeyPair();
            var callAddress = Address.FromPublicKey(callKeyPair.PublicKey);
            var callPublicKey = callKeyPair.PublicKey.ToHex();
            
            return (callKeyPair, callAddress, callPublicKey);
        }
    }
}