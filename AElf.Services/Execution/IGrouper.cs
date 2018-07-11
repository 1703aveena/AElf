﻿using System;
using System.Collections.Generic;
using AElf.Kernel;

namespace AElf.Services.Execution
{
	public interface IGrouper
    {
		List<List<ITransaction>> ProcessNaive(Hash chainId, List<ITransaction> transactions, out Dictionary<ITransaction, Exception> failedTxs);

	    List<List<ITransaction>> ProcessWithCoreCount(GroupStrategy strategy, int totalCores, Hash chainId,
		    List<ITransaction> transactions, out Dictionary<ITransaction, Exception> failedTxs);
    }
}
