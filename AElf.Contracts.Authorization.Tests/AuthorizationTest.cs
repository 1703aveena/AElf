using System;
using System.IO;
using AElf.Common;
using AElf.Cryptography.ECDSA;
using AElf.Kernel;
using AElf.Kernel.Types;
using AElf.Types.CSharp;
using Google.Protobuf;
using Google.Protobuf.WellKnownTypes;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;


namespace AElf.Contracts.Authorization.Tests
{
public class AuthorizationTest : AuthroizationContractTestBase
    {
        private AuthorizationContractShim _contract;
        public ILogger<AuthorizationTest> Logger {get;set;}
        private MockSetup Mock;

        //private static byte[] ChainId = ChainHelpers.GetRandomChainId();
        
        public AuthorizationTest()
        {
            
            Logger = NullLogger<AuthorizationTest>.Instance;
            Mock = GetRequiredService<MockSetup>();

        }

        [Fact]
        public void CreateMSigAccount()
        {
            _contract = new AuthorizationContractShim(Mock, ContractHelpers.GetAuthorizationContractAddress(Mock.ChainId), Mock.ChainId.DumpByteArray());
            Address msig = Address.Generate();
            var auth = new Kernel.Authorization
            {
                ExecutionThreshold = 2,
                MultiSigAccount = msig,
                ProposerThreshold = 1
            };
            var kp1 = new KeyPairGenerator().Generate();
            var kp2 = new KeyPairGenerator().Generate();
            var kp3 = new KeyPairGenerator().Generate();
            auth.Reviewers.AddRange(new[]
            {
                new Reviewer
                {
                    PubKey = ByteString.CopyFrom(kp1.PublicKey),
                    Weight = 1
                },
                new Reviewer
                {
                    PubKey = ByteString.CopyFrom(kp2.PublicKey),
                    Weight = 1
                },
                new Reviewer
                {
                    PubKey = ByteString.CopyFrom(kp3.PublicKey),
                    Weight = 0
                }
            });
            var addr = _contract.CreateMSigAccount(auth).Result;
            Assert.Equal(msig.DumpByteArray(), addr);
        }

        [Fact]
        public void ProposeInvalidProposal()
        {
            _contract = new AuthorizationContractShim(Mock, ContractHelpers.GetAuthorizationContractAddress(Mock.ChainId), Mock.ChainId.DumpByteArray());
            
            // todo review link a keypair to msig account, for now just to generate the address from pubkey
            var kpMsig = new KeyPairGenerator().Generate();
            Address msig = Address.FromPublicKey(kpMsig.PublicKey);
            
            var auth = new Kernel.Authorization
            {
                ExecutionThreshold = 2,
                MultiSigAccount = msig,
                ProposerThreshold = 1
            };
            
            var kp1 = new KeyPairGenerator().Generate();
            var kp2 = new KeyPairGenerator().Generate();
            var kp3 = new KeyPairGenerator().Generate();
            
            auth.Reviewers.AddRange(new[]
            {
                new Reviewer
                {
                    PubKey = ByteString.CopyFrom(kp1.PublicKey),
                    Weight = 1
                },
                new Reviewer
                {
                    PubKey = ByteString.CopyFrom(kp2.PublicKey),
                    Weight = 1
                },
                new Reviewer
                {
                    PubKey = ByteString.CopyFrom(kp3.PublicKey),
                    Weight = 0
                }
            });
            
            var addr = _contract.CreateMSigAccount(auth).Result;
            Assert.Equal(msig.DumpByteArray(), addr);
            
            var expiredProposal = new Proposal
            {
                ExpiredTime = TimerHelper.ConvertToUnixTimestamp(DateTime.UtcNow.AddSeconds(-10)),
                MultiSigAccount = msig,
                Name = "Propose",
                TxnData = CreateDemoTxn(msig).ToByteString()
            };
            
            var res = _contract.Propose(expiredProposal, kp1).Result;
            //Hash hash = Hash.LoadByteArray(res);
            Assert.Null(res);
            
            var notFoundAuthorizationProposal = new Proposal
            {
                ExpiredTime = TimerHelper.ConvertToUnixTimestamp(DateTime.UtcNow.AddSeconds(10)),
                MultiSigAccount = Address.Generate(),
                Name = "Propose",
                //ProposerPublicKey = ByteString.CopyFrom(kp1.PublicKey),
                TxnData = CreateDemoTxn(msig).ToByteString()
            };
            
            res = _contract.Propose(notFoundAuthorizationProposal, kp1).Result;
            Assert.Null(res);
            
            var notAuthorizedProposal = new Proposal
            {
                ExpiredTime = TimerHelper.ConvertToUnixTimestamp(DateTime.UtcNow.AddSeconds(10)),
                MultiSigAccount = msig,
                Name = "Propose",
                //ProposerPublicKey = ByteString.CopyFrom(kp3.PublicKey),
                TxnData = CreateDemoTxn(msig).ToByteString()
            };
            
            res = _contract.Propose(notAuthorizedProposal, kp3).Result;
            Assert.Null(res);
            
            var validProposal = new Proposal
            {
                ExpiredTime = TimerHelper.ConvertToUnixTimestamp(DateTime.UtcNow.AddSeconds(10)),
                MultiSigAccount = msig,
                Name = "Propose",
                TxnData = CreateDemoTxn(msig).ToByteString(),
                Proposer = Address.FromPublicKey(kp1.PublicKey),
                Status = ProposalStatus.ToBeDecided
            };
            
            res = _contract.Propose(validProposal, kp1).Result;
            Assert.NotEmpty(res);
        }

        [Fact]
        public void ProposeValidProposal()
        {
            _contract = new AuthorizationContractShim(Mock, ContractHelpers.GetAuthorizationContractAddress(Mock.ChainId), Mock.ChainId.DumpByteArray());
            
            // todo review link a keypair to msig account, for now just to generate the address from pubkey
            var kpMsig = new KeyPairGenerator().Generate();
            Address msig = Address.FromPublicKey(kpMsig.PublicKey);
            
            var auth = new Kernel.Authorization
            {
                ExecutionThreshold = 2,
                MultiSigAccount = msig,
                ProposerThreshold = 1
            };
            
            var kp1 = new KeyPairGenerator().Generate();
            var kp2 = new KeyPairGenerator().Generate();
            var kp3 = new KeyPairGenerator().Generate();
            
            auth.Reviewers.AddRange(new[]
            {
                new Reviewer
                {
                    PubKey = ByteString.CopyFrom(kp1.PublicKey),
                    Weight = 1
                },
                new Reviewer
                {
                    PubKey = ByteString.CopyFrom(kp2.PublicKey),
                    Weight = 1
                },
                new Reviewer
                {
                    PubKey = ByteString.CopyFrom(kp3.PublicKey),
                    Weight = 0
                }
            });
            
            var addr = _contract.CreateMSigAccount(auth).Result;
            Assert.Equal(msig.DumpByteArray(), addr);

            var validProposal = new Proposal
            {
                ExpiredTime = TimerHelper.ConvertToUnixTimestamp(DateTime.UtcNow.AddSeconds(10)),
                MultiSigAccount = msig,
                Name = "Propose",
                Proposer = Address.FromPublicKey(kp1.PublicKey),
                TxnData = CreateDemoTxn(msig).ToByteString()
            };
            
            var res = _contract.Propose(validProposal, kp1).Result;
            
            Assert.NotEmpty(res);
        }

        [Fact]
        public void SayYes()
        {
            _contract = new AuthorizationContractShim(Mock, ContractHelpers.GetAuthorizationContractAddress(Mock.ChainId), Mock.ChainId.DumpByteArray());
            
            // todo review link a keypair to msig account, for now just to generate the address from pubkey
            var kpMsig = new KeyPairGenerator().Generate();
            Address msig = Address.FromPublicKey(kpMsig.PublicKey);
            
            var auth = new Kernel.Authorization
            {
                ExecutionThreshold = 2,
                MultiSigAccount = msig,
                ProposerThreshold = 1
            };
            
            var kp1 = new KeyPairGenerator().Generate();
            var kp2 = new KeyPairGenerator().Generate();
            var kp3 = new KeyPairGenerator().Generate();
            
            auth.Reviewers.AddRange(new[]
            {
                new Reviewer
                {
                    PubKey = ByteString.CopyFrom(kp1.PublicKey),
                    Weight = 1
                },
                new Reviewer
                {
                    PubKey = ByteString.CopyFrom(kp2.PublicKey),
                    Weight = 1
                },
                new Reviewer
                {
                    PubKey = ByteString.CopyFrom(kp3.PublicKey),
                    Weight = 0
                }
            });
            
            var addr = _contract.CreateMSigAccount(auth).Result;
            Assert.Equal(msig.DumpByteArray(), addr);

            var tx = CreateDemoTxn(msig);
            
            var validProposal = new Proposal
            {
                ExpiredTime = TimerHelper.ConvertToUnixTimestamp(DateTime.UtcNow.AddSeconds(10)),
                MultiSigAccount = msig,
                Name = "Propose",
                Proposer = Address.FromPublicKey(kp1.PublicKey),
                TxnData = tx.ToByteString()
            };
            
            var res = _contract.Propose(validProposal, kp1).Result;
            Assert.NotEmpty(res);
            
            var newKp = new KeyPairGenerator().Generate();
            
            ECSigner signer = new ECSigner();
            ECSignature signature = signer.Sign(newKp, tx.GetHash().DumpByteArray());
            
            var notAuthorizedApproval = new Approval
            {
                ProposalHash = Hash.LoadByteArray(res),
                Signature = ByteString.CopyFrom(signature.SigBytes)
            };
            
            var sayYesRes = _contract.SayYes(notAuthorizedApproval, newKp).Result;
            Assert.False(sayYesRes);
            
            ECSignature signatureKp1 = signer.Sign(kp1, tx.GetHash().DumpByteArray());
            var validApproval = new Approval
            {
                ProposalHash = Hash.LoadByteArray(res),
                Signature = ByteString.CopyFrom(signatureKp1.SigBytes) 
            };
            
            // todo review print
            //Console.WriteLine(Hash.FromRawBytes(validApproval.Signature.P.ToByteArray()));
            
            sayYesRes = _contract.SayYes(validApproval, kp1).Result;
            Assert.True(sayYesRes);
            
            // say yes again
            sayYesRes = _contract.SayYes(validApproval, kp2).Result;
            Assert.False(sayYesRes);
        }
        
        
        [Fact]
        public void Release()
        {
            _contract = new AuthorizationContractShim(Mock, ContractHelpers.GetAuthorizationContractAddress(Mock.ChainId), Mock.ChainId.DumpByteArray());
            
            // todo review link a keypair to msig account, for now just to generate the address from pubkey
            var kpMsig = new KeyPairGenerator().Generate();
            Address msig = Address.FromPublicKey(kpMsig.PublicKey);
            
            var auth = new Kernel.Authorization
            {
                ExecutionThreshold = 2,
                MultiSigAccount = msig,
                ProposerThreshold = 1
            };
            
            var kp1 = new KeyPairGenerator().Generate();
            var kp2 = new KeyPairGenerator().Generate();
            var kp3 = new KeyPairGenerator().Generate();
            
            auth.Reviewers.AddRange(new[]
            {
                new Reviewer
                {
                    PubKey = ByteString.CopyFrom(kp1.PublicKey),
                    Weight = 1
                },
                new Reviewer
                {
                    PubKey = ByteString.CopyFrom(kp2.PublicKey),
                    Weight = 1
                },
                new Reviewer
                {
                    PubKey = ByteString.CopyFrom(kp3.PublicKey),
                    Weight = 0
                }
            });
            
            var addr = _contract.CreateMSigAccount(auth).Result;
            Assert.Equal(msig.DumpByteArray(), addr);

            var tx = CreateDemoTxn(msig);
            
            var validProposal = new Proposal
            {
                ExpiredTime = TimerHelper.ConvertToUnixTimestamp(DateTime.UtcNow.AddSeconds(10)),
                MultiSigAccount = msig,
                Name = "Propose",
                Proposer = Address.FromPublicKey(kp1.PublicKey),
                TxnData = tx.ToByteString()
            };
            
            var res = _contract.Propose(validProposal, kp1).Result;
            Assert.NotEmpty(res);
            
            ECSigner signer = new ECSigner();
            
            // first approval
            ECSignature signature = signer.Sign(kp1, tx.GetHash().DumpByteArray());
            var validApproval1 = new Approval
            {
                ProposalHash = Hash.LoadByteArray(res),
                Signature = ByteString.CopyFrom(signature.SigBytes)
            };
            
            var res1 = _contract.SayYes(validApproval1, kp1).Result;
            Assert.True(res1);
            
            // second approval 
            ECSignature signatureKp3 = signer.Sign(kp3, tx.GetHash().DumpByteArray());
            var validApproval2 = new Approval
            {
                ProposalHash = Hash.LoadByteArray(res),
                Signature = ByteString.CopyFrom(signatureKp3.SigBytes)
            };
            
            var res2 = _contract.SayYes(validApproval2, kp3).Result;
            Assert.True(res2);

            // not enough authorization
            var txnHash = _contract.Release(Hash.LoadByteArray(res), new KeyPairGenerator().Generate()).Result;
            Assert.Null(txnHash);
            
            // third approval 
            ECSignature signatureKp2 = signer.Sign(kp2, tx.GetHash().DumpByteArray());
            var validApproval3 = new Approval
            {
                ProposalHash = Hash.LoadByteArray(res),
                Signature = ByteString.CopyFrom(signatureKp2.SigBytes)
            };
            var res3 = _contract.SayYes(validApproval3, kp2).Result;
            Assert.True(res3);
            
            txnHash = _contract.Release(Hash.LoadByteArray(res), new KeyPairGenerator().Generate()).Result;
            
            Assert.NotNull(txnHash);
            Assert.Equal(msig, txnHash.From);
            Assert.True(txnHash.VerifySignature());
        }
        
        
        private Transaction CreateDemoTxn(Address msig)
        {
            byte[] code = File.ReadAllBytes(Path.GetFullPath("../../../../AElf.Contracts.Token/bin/Debug/netstandard2.0/AElf.Contracts.Token.dll"));
            var tx = new Transaction
            {
                From = msig,
                To = ContractHelpers.GetGenesisBasicContractAddress(Mock.ChainId),
                MethodName = "DeploySmartContract",
                Params = ByteString.CopyFrom(ParamsPacker.Pack(1, code)),
                Type = TransactionType.MsigTransaction
            };
            return tx;
        }
    }
}