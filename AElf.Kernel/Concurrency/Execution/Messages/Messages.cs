﻿using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace AElf.Kernel.Concurrency.Execution.Messages
{
	#region AccountDataContext
	public sealed class RequestAccountDataContext
	{
		public RequestAccountDataContext(long requestId, Hash hash)
		{
			RequestId = requestId;
			AccountHash = hash;
		}

		public long RequestId { get; }
		public Hash AccountHash { get; }
	}

	public sealed class RespondAccountDataContext
	{
		public RespondAccountDataContext(long requestId, IAccountDataContext accountDataContext)
		{
			RequestId = requestId;
			AccountDataContext = accountDataContext;
		}

		public long RequestId { get; }
		public IAccountDataContext AccountDataContext { get; }
	}
	#endregion AccountDataContext

	#region ExecuteTransactions
	public sealed class RequestExecuteTransactions
	{
		public RequestExecuteTransactions(long requestId, List<Transaction> transactions)
		{
			RequestId = requestId;
			Transactions = transactions;
		}

		public long RequestId { get; }
		public List<Transaction> Transactions { get; }
	}

	public sealed class RespondExecuteTransactions
	{
		public enum RequestStatus
		{
			Rejected,
			Executed
		}
		public RespondExecuteTransactions(long requestId, RequestStatus status, List<TransactionResult> transactionResults)
		{
			RequestId = requestId;
			Status = status;
			TransactionResults = transactionResults;
		}

		public long RequestId { get; }
		public RequestStatus Status { get; }
		public List<TransactionResult> TransactionResults { get; }
	}
	#endregion ExecuteTransactions

	/// <summary>
	/// Message sent to local requestor for transaction execution.
	/// </summary>
	public sealed class LocalExecuteTransactionsMessage
	{
		public LocalExecuteTransactionsMessage(Hash chainId, List<Transaction> transactions, TaskCompletionSource<List<TransactionResult>> taskCompletionSource)
		{
			ChainId = chainId;
			Transactions = transactions;
			TaskCompletionSource = taskCompletionSource;
		}

		public Hash ChainId { get; }
		public List<Transaction> Transactions { get; }
		public TaskCompletionSource<List<TransactionResult>> TaskCompletionSource { get; }
	}

	public sealed class TransactionResultMessage
	{
		public TransactionResultMessage(TransactionResult transactionResult)
		{
			TransactionResult = transactionResult;
		}

		public TransactionResult TransactionResult { get; }
	}

	#region Singleton Messages
	/// <summary>
	/// Short-lived executor actors require a <see cref="StartExecutionMessage"/> to start execution.
	/// </summary>
	public sealed class StartExecutionMessage
	{
		private StartExecutionMessage() { }

		/// <summary>
		/// The singleton instance of StartExecutionMessage.
		/// </summary>
		public static StartExecutionMessage Instance { get; } = new StartExecutionMessage();

		/// <inheritdoc/>
		public override string ToString()
		{
			return "<StartExecutionMessage>";
		}
	}

	/// <summary>
	/// <see cref="StartGroupingMessage"/> is automatically sent to the actor itself upon starting so that grouping will start.
	/// </summary>
	public sealed class StartGroupingMessage
	{
		private StartGroupingMessage() { }

		/// <summary>
		/// The singleton instance of StartGroupingMessage.
		/// </summary>
		public static StartGroupingMessage Instance { get; } = new StartGroupingMessage();

		/// <inheritdoc/>
		public override string ToString()
		{
			return "<StartGroupingMessage>";
		}
	}

	/// <summary>
	/// <see cref="StartBatchingMessage"/> is automatically sent to the actor itself upon starting so that batching will start.
	/// </summary>
	public sealed class StartBatchingMessage
	{
		private StartBatchingMessage() { }

		/// <summary>
		/// The singleton instance of StartBatchingMessage.
		/// </summary>
		public static StartBatchingMessage Instance { get; } = new StartBatchingMessage();

		/// <inheritdoc/>
		public override string ToString()
		{
			return "<StartBatchingMessage>";
		}
	}
	#endregion Singleton Messages
}