using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using AElf.Common;
using AElf.SmartContract;
using AElf.Kernel;
using AElf.Kernel.Types.Proposal;
using AElf.Types.CSharp;
using Google.Protobuf;

namespace AElf.Contracts.Authorization.Tests
{
    public class AuthorizationContractShim
    {
        private MockSetup _mock;
        public IExecutive _executive;
        public IExecutive Executive {
            get
            {
                _executive?.SetDataCache(new Dictionary<DataPath, StateCache>());
                return _executive;
            }
      
            set => _executive = value;
        }

        public ITransactionContext TransactionContext { get; private set; }

        public Address Sender { get; } = Address.FromString("sender");
        public Address AuthorizationContractAddress { get; set; }

        public AuthorizationContractShim(MockSetup mock, Address authorizationContractAddress)
        {
            _mock = mock;
            AuthorizationContractAddress = authorizationContractAddress;
            Init();
        }

        private void Init()
        {
            var task = _mock.GetExecutiveAsync(AuthorizationContractAddress);
            task.Wait();
            Executive = task.Result;
        }
        
        private async Task CommitChangesAsync(TransactionTrace trace)
        {
            await trace.CommitChangesAsync(_mock.StateStore);
        }

        public async Task<Address> CreateMSigAccount(Kernel.Types.Proposal.Authorization authorization)
        {
            var tx = new Transaction
            {
                From = Sender,
                To = AuthorizationContractAddress,
                MethodName = "CreateMultiSigAccount",
                Params = ByteString.CopyFrom(ParamsPacker.Pack(authorization))
            };
            TransactionContext = new TransactionContext()
            {
                Transaction = tx
            };
            await Executive.SetTransactionContext(TransactionContext).Apply();
            await CommitChangesAsync(TransactionContext.Trace);
            return TransactionContext.Trace.RetVal?.Data.DeserializeToPbMessage<Address>();
        }

        public async Task<Hash> Propose(Proposal proposal, Address sender)
        {
            try
            {
                var tx = new Transaction
                {
                    From = sender,
                    To = AuthorizationContractAddress,
                    MethodName = "Propose",
                    Params = ByteString.CopyFrom(ParamsPacker.Pack(proposal))
                };
                TransactionContext = new TransactionContext
                {
                    Transaction = tx
                };
                await Executive.SetTransactionContext(TransactionContext).Apply();
                await CommitChangesAsync(TransactionContext.Trace);
                return TransactionContext.Trace.RetVal?.Data.DeserializeToPbMessage<Hash>();
            }
            catch (Exception)
            {
                return null;
            }
        }

        public async Task<bool> SayYes(Approval approval, Address sender)
        {
            try
            {
                var tx = new Transaction
                {
                    From = sender,
                    To = AuthorizationContractAddress,
                    MethodName = "SayYes",
                    Params = ByteString.CopyFrom(ParamsPacker.Pack(approval))
                };
                TransactionContext = new TransactionContext()
                {
                    Transaction = tx
                };
                await Executive.SetTransactionContext(TransactionContext).Apply();
                await CommitChangesAsync(TransactionContext.Trace);
                return TransactionContext.Trace.RetVal?.Data.DeserializeToBool() ?? false;
            }
            catch (Exception)
            {
                return false;
            }
        }

        public async Task<Transaction> Release(Hash proposalHash, Address sender)
        {
            try
            {
                var tx = new Transaction
                {
                    From = sender,
                    To = AuthorizationContractAddress,
                    MethodName = "Release",
                    Params = ByteString.CopyFrom(ParamsPacker.Pack(proposalHash))
                };
                TransactionContext = new TransactionContext()
                {
                    Transaction = tx
                };
                await Executive.SetTransactionContext(TransactionContext).Apply();
                await CommitChangesAsync(TransactionContext.Trace);
                return TransactionContext.Trace.DeferredTransaction != null
                    ? Transaction.Parser.ParseFrom(TransactionContext.Trace.DeferredTransaction)
                    : null;
            }
            catch (Exception)
            {
                return null;
            }
        }
    }
}