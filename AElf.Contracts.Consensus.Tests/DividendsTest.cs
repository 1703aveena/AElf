using System;
using System.Collections.Generic;
using System.Linq;
using AElf.Common;
using AElf.Cryptography.ECDSA;
using AElf.Kernel;
using AElf.Sdk.CSharp;
using Xunit;
using Xunit.Frameworks.Autofac;

namespace AElf.Contracts.Consensus.Tests
{
    [UseAutofacTestFramework]
    public class DividendsTest
    {
        private const int CandidatesCount = 18;
        private const int VotersCount = 2;
        
        private readonly ConsensusContractShim _consensusContract;

        private readonly MockSetup _mock;

        private readonly List<ECKeyPair> _initialMiners = new List<ECKeyPair>();
        private readonly List<ECKeyPair> _candidates = new List<ECKeyPair>();
        private readonly List<ECKeyPair> _voters = new List<ECKeyPair>();

        private int MiningInterval => 1;
        
        public DividendsTest(MockSetup mock)
        {
            _mock = mock;
            _consensusContract = new ConsensusContractShim(mock);

            const ulong totalSupply = 100_000_000_000;
            _consensusContract.Initialize("ELF", "AElf Token", totalSupply, 2);

            _consensusContract.Transfer(_consensusContract.DividendsContractAddress, (ulong) (totalSupply * 0.12 * 0.2));
        }
        
        //[Fact]
        [Fact(Skip = "Time consuming")]
        public void GetDividendsTest()
        {
            GlobalConfig.ElfTokenPerBlock = 1000;
            InitialMiners();
            InitialTerm(_initialMiners[0]);
            
            InitialCandidates();
            InitialVoters();

            var candidatesList = _consensusContract.GetCandidatesListToFriendlyString();
            Assert.Equal(string.Empty, _consensusContract.TransactionContext.Trace.StdErr);
            Assert.Contains(_candidates[1].PublicKey.ToHex(), candidatesList);
            
            var balanceOfConsensusContract = _consensusContract.BalanceOf(_consensusContract.ConsensusContractAddress);
            Assert.True(balanceOfConsensusContract > 0);

            ECKeyPair mustVotedVoter = null;
            // Vote to candidates randomized
            foreach (var voter in _voters)
            {
                foreach (var candidate in _candidates)
                {
                    if (new Random().Next(0, 10) < 5)
                    {
                        mustVotedVoter = voter;
                        _consensusContract.Vote(voter, candidate, (ulong) new Random().Next(1, 100), 90);
                    }
                }
            }
            Assert.NotNull(mustVotedVoter);

            var ticketsInformationInJson = _consensusContract.GetTicketsInfoToFriendlyString(mustVotedVoter);
            Assert.Equal(string.Empty, _consensusContract.TransactionContext.Trace.StdErr);

            var ticketsInformation = _consensusContract.GetTicketsInfo(mustVotedVoter);
            var votedTickets = ticketsInformation.TotalTickets;
            var balanceAfterVoting = _consensusContract.BalanceOf(GetAddress(mustVotedVoter));
            Assert.True(votedTickets + balanceAfterVoting == 100_000);

            // Get victories of first term of election, they are miners then.
            var victories = _consensusContract.GetCurrentVictories().Values;
            
            // Next term.
            var secondTerm = victories.ToMiners().GenerateNewTerm(MiningInterval, 2, 2);
            _consensusContract.NextTerm(_candidates.First(c => c.PublicKey.ToHex() == victories[1]), secondTerm);

            var secondRound = _consensusContract.GetRoundInfo(2);
            
            // New miners produce some blocks.
            var inValuesList = new Stack<Hash>();
            var outValuesList = new Stack<Hash>();
            for (var i = 0; i < GlobalConfig.BlockProducerNumber; i++)
            {
                var inValue = Hash.Generate();
                inValuesList.Push(inValue);
                outValuesList.Push(Hash.FromMessage(inValue));
            }

            foreach (var newMiner in victories)
            {
                _consensusContract.PackageOutValue(GetCandidateKeyPair(newMiner), new ToPackage
                {
                    OutValue = outValuesList.Pop(),
                    RoundId = secondRound.RoundId,
                    Signature = Hash.Default
                });
                
                _consensusContract.BroadcastInValue(GetCandidateKeyPair(newMiner), new ToBroadcast
                {
                    InValue = inValuesList.Pop(),
                    RoundId = secondRound.RoundId
                });
            }

            // Third item.
            var thirdTerm = victories.ToMiners().GenerateNewTerm(MiningInterval, 3, 3);
            _consensusContract.NextTerm(_candidates.First(c => c.PublicKey.ToHex() == victories[1]), thirdTerm);

            var snapshotOfSecondTerm = _consensusContract.GetTermSnapshot(2);
            Assert.True(snapshotOfSecondTerm.TotalBlocks == 18);

            var dividendsOfSecondTerm = _consensusContract.GetTermDividends(2);
            var shouldBe = (ulong) (18 * GlobalConfig.ElfTokenPerBlock * 0.2);
            Assert.True(dividendsOfSecondTerm == shouldBe);
            
            var balanceBefore = _consensusContract.BalanceOf(GetAddress(mustVotedVoter));
            _consensusContract.ReceiveAllDividends(mustVotedVoter);
            var balanceAfter = _consensusContract.BalanceOf(GetAddress(mustVotedVoter));
            Assert.Equal(string.Empty, _consensusContract.TransactionContext.Trace.StdErr);
            Assert.True(balanceAfter >= balanceBefore);
        }

        private ECKeyPair GetCandidateKeyPair(string publicKey)
        {
            return _candidates.First(c => c.PublicKey.ToHex() == publicKey);
        }
        
        private void InitialMiners()
        {
            for (var i = 0; i < GlobalConfig.BlockProducerNumber; i++)
            {
                _initialMiners.Add(new KeyPairGenerator().Generate());
            }
        }

        private void InitialCandidates()
        {
            for (var i = 0; i < CandidatesCount; i++)
            {
                var keyPair = new KeyPairGenerator().Generate();
                _candidates.Add(keyPair);
                // Enough for him to announce election
                _consensusContract.Transfer(GetAddress(keyPair), GlobalConfig.LockTokenForElection);
                _consensusContract.AnnounceElection(keyPair);
            }
        }

        private void InitialVoters()
        {
            for (var i = 0; i < VotersCount; i++)
            {
                var keyPair = new KeyPairGenerator().Generate();
                _voters.Add(keyPair);
                // Send them some tokens to vote.
                _consensusContract.Transfer(GetAddress(keyPair), 100_000);
            }
        }
        
        private void InitialTerm(ECKeyPair starterKeyPair)
        {
            var initialTerm =
                new Miners {PublicKeys = {_initialMiners.Select(m => m.PublicKey.ToHex())}}.GenerateNewTerm(MiningInterval);
            _consensusContract.InitialTerm(starterKeyPair, initialTerm);
        }

        private Address GetAddress(ECKeyPair keyPair)
        {
            return Address.FromPublicKey(keyPair.PublicKey);
        }
    }
}