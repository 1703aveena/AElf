using AElf.Kernel;
using Google.Protobuf;
using AElf.SmartContract;
using AElf.Types.CSharp;
using AElf.Common;

namespace AElf.Contracts.Consensus.Tests
{
    public class ContractZeroShim
    {
        private MockSetup _mock;
        
        public IExecutive Executive { get; set; }

        public TransactionContext TransactionContext { get; private set; }

        public static Address Sender => Address.Zero;

        public Address ConsensusContractAddress => ContractHelpers.GetConsensusContractAddress(_mock.ChainId);

        public Address DividendsContractAddress => ContractHelpers.GetDividendsContractAddress(_mock.ChainId);

        public ContractZeroShim(MockSetup mock)
        {
            _mock = mock;
            Initialize();
        }

        private void Initialize()
        {
            var task = _mock.GetExecutiveAsync(ConsensusContractAddress);
            task.Wait();
            Executive = task.Result;
        }

        public byte[] DeploySmartContract(int category, byte[] code)
        {
            var tx = new Transaction
            {
                From = Sender,
                To = ConsensusContractAddress,
                IncrementId = MockSetup.NewIncrementId,
                MethodName = "DeploySmartContract",
                Params = ByteString.CopyFrom(ParamsPacker.Pack(category, code))
            };

            TransactionContext = new TransactionContext
            {
                Transaction = tx
            };
            Executive.SetTransactionContext(TransactionContext).Apply().Wait();
            TransactionContext.Trace.CommitChangesAsync(_mock.StateManager).Wait();
            return TransactionContext.Trace.RetVal?.Data.DeserializeToBytes();
        }

        public void ChangeContractOwner(Hash contractAddress, Hash newOwner)
        {
            var tx = new Transaction
            {
                From = Sender,
                To = ConsensusContractAddress,
                IncrementId = MockSetup.NewIncrementId,
                MethodName = "ChangeContractOwner",
                Params = ByteString.CopyFrom(ParamsPacker.Pack(contractAddress, newOwner))
            };

            TransactionContext = new TransactionContext
            {
                Transaction = tx
            };
            Executive.SetTransactionContext(TransactionContext).Apply().Wait();
            TransactionContext.Trace.CommitChangesAsync(_mock.StateManager).Wait();
        }
        
        public Address GetContractOwner(Address contractAddress)
        {
            var tx = new Transaction
            {
                From = Sender,
                To = ConsensusContractAddress,
                IncrementId = MockSetup.NewIncrementId,
                MethodName = "GetContractOwner",
                Params = ByteString.CopyFrom(ParamsPacker.Pack(contractAddress))
            };

            TransactionContext = new TransactionContext
            {
                Transaction = tx
            };
            Executive.SetTransactionContext(TransactionContext).Apply().Wait();
            TransactionContext.Trace.CommitChangesAsync(_mock.StateManager).Wait();
            return TransactionContext.Trace.RetVal?.Data.DeserializeToPbMessage<Address>();
        }
    }
}