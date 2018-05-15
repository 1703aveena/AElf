﻿using System;
using System.IO;
using System.Threading.Tasks;
using System.Collections.Generic;
using Xunit;
using Xunit.Frameworks.Autofac;
using Akka.Actor;
using Akka.TestKit;
using Akka.TestKit.Xunit;
using AElf.Kernel;
using AElf.Kernel.Concurrency.Execution;
using AElf.Kernel.Concurrency.Execution.Messages;
using AElf.Kernel.KernelAccount;
using AElf.Kernel.Managers;
using AElf.Kernel.Services;
using Google.Protobuf;
using Google.Protobuf.WellKnownTypes;

namespace AElf.Kernel.Tests.Concurrency.Execution
{

	public class SmartContractZeroWithTransfer : ISmartContractZero
	{
		public class InsufficientBalanceException : Exception { }

		private Dictionary<Hash, ulong> _cryptoAccounts = new Dictionary<Hash, ulong>();

		public async Task InvokeAsync(IHash caller, string methodname, ByteString bytes)
		{
			var type = typeof(SmartContractZeroWithTransfer);
			var member = type.GetMethod(methodname);

			var p = member.GetParameters()[0]; //first parameters
			ProtobufSerializer serializer = new ProtobufSerializer();
			// TODO: Compare with SmartContractZero
			#region Not Same As SmartContractZero
			var obj = serializer.Deserialize(bytes.ToByteArray(), p.ParameterType);

			await (Task)member.Invoke(this, new object[] { obj });
			#endregion Not Same As Smart Contract Zero
		}

		public void SetBalance(Hash account, ulong balance)
		{
			_cryptoAccounts[account] = balance;
		}

		public ulong GetBalance(Hash account)
		{
			if (!_cryptoAccounts.TryGetValue(account, out var bal))
			{
				bal = 0;
			}
			return bal;
		}

		public async Task Transfer(TransferArgs transfer)
		{
			var qty = transfer.Quantity;
			var from = transfer.From;
			var to = transfer.To;

			var fromBal = GetBalance(transfer.From);
			var toBal = GetBalance(transfer.To);


			if (fromBal <= transfer.Quantity)
			{
				throw new InsufficientMemoryException();
			}

			SetBalance(transfer.From, fromBal - qty);
			SetBalance(transfer.To, toBal + qty);

			await Task.CompletedTask;
		}

		#region ISmartContractZero
		// All are dummies
		public Task InitializeAsync(IAccountDataProvider dataProvider)
		{
			return Task.CompletedTask;
		}

		public Task RegisterSmartContract(SmartContractRegistration reg)
		{
			return Task.CompletedTask;
		}

		public Task DeploySmartContract(SmartContractDeployment smartContractRegister)
		{
			return Task.CompletedTask;
		}

		public Task<ISmartContract> GetSmartContractAsync(Hash hash)
		{
			return Task.FromResult<ISmartContract>(this);
		}
		#endregion
	}

	public class ChainContextWithSmartContractZeroWithTransfer : IChainContext
	{
		public ISmartContractZero SmartContractZero { get; }
		public Hash ChainId { get; }
		public ChainContextWithSmartContractZeroWithTransfer(SmartContractZeroWithTransfer smartContractZero, Hash chainId)
		{
			SmartContractZero = smartContractZero;
			ChainId = chainId;
		}
	}

	[UseAutofacTestFramework]
	public class ParallelExecutionTransactionExecutorTest : TestKitBase
	{
		private ActorSystem sys = ActorSystem.Create("test");
		private IChainContext _chainContext;
		private AccountContextService _accountContextService;
		private ProtobufSerializer _serializer = new ProtobufSerializer();

		public ParallelExecutionTransactionExecutorTest(ChainContextWithSmartContractZeroWithTransfer chainContext, AccountContextService accountContextService) : base(new XunitAssertions())
		{
			Console.Write(chainContext);
			_chainContext = chainContext;
			_accountContextService = accountContextService;

		}

		private Transaction GetTransaction(Hash from, Hash to, ulong qty)
		{
			// TODO: Test with IncrementId
			TransferArgs args = new TransferArgs()
			{
				From = from,
				To = to,
				Quantity = qty
			};

			ByteString argsBS = ByteString.CopyFrom(_serializer.Serialize(args));

			Transaction tx = new Transaction()
			{
				IncrementId = 0,
				From = from,
				To = to,
				MethodName = "Transfer",
				Params = argsBS
			};
         
			return tx;
		}

		[Fact]
		public void RequestTransactionExecutionTest()
		{
			ProtobufSerializer serializer = new ProtobufSerializer();
			Hash from = Hash.Generate();
			Hash to = Hash.Generate();
			var executor = sys.ActorOf(ParallelExecutionTransactionExecutor.Props(_chainContext));
            
			SmartContractZeroWithTransfer smartContractZero = (_chainContext.SmartContractZero as SmartContractZeroWithTransfer);
			smartContractZero.SetBalance(from, 100);
			smartContractZero.SetBalance(to, 0);
            
			// Normal transfer
			var tx = GetTransaction(from, to, 10);
			executor.Tell(new RequestTransactionExecution(42, tx));
			var result = ExpectMsg<RespondTransactionExecution>();
			Assert.Equal(42, result.RequestId);
			Assert.Equal(tx.GetHash(), result.TransactionResult.TransactionId);
			Assert.Equal(Status.Mined, result.TransactionResult.Status);
			Assert.Equal((ulong)90, smartContractZero.GetBalance(from));
			Assert.Equal((ulong)10, smartContractZero.GetBalance(to));

			// Insufficient balance
			tx = GetTransaction(from, to, 100);
			executor.Tell(new RequestTransactionExecution(43, tx));
			result = ExpectMsg<RespondTransactionExecution>();
			Assert.Equal(43, result.RequestId);
			Assert.Equal(tx.GetHash(), result.TransactionResult.TransactionId);
			Assert.Equal(Status.ExecutedFailed, result.TransactionResult.Status);
			Assert.Equal((ulong)90, smartContractZero.GetBalance(from));
			Assert.Equal((ulong)10, smartContractZero.GetBalance(to));
		}

	}
}