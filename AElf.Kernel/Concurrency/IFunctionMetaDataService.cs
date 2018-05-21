﻿using System;
using System.Collections.Generic;

namespace AElf.Kernel.Concurrency
{
    
    public interface IFunctionMetaDataService
    {
        /// <summary>
        /// use Map to store the function's metadata
        /// </summary>
        Dictionary<string, FunctionMetadata> FunctionMetadataMap { get; }
        
        /// <summary>
        /// Called when deploy a new contract
        /// TODO: need to be async when this access datastore
        /// </summary>
        /// <param name="functionFullName">Function's full name: "[ChainName].[ContractName].[Function(ParameterType1,ParameterType2)]"</param>
        /// <param name="otherFunctionsCallByThis">List of function called by this function.</param>
        /// <param name="nonRecursivePathSet">The non-recursive path set of this function</param>
        /// <exception cref="InvalidOperationException">Throw when FunctionMetadataMap already contains a function with same fullname</exception>
        /// <returns>True when success, false when something is wrong (usually is cannot find record with respect to functionName in the parameter otherFunctionsCallByThis)</returns>
        bool SetNewFunctionMetadata(string functionFullName, HashSet<string> otherFunctionsCallByThis, HashSet<Hash> nonRecursivePathSet);
        
        /// <summary>
        /// Get a function's metadata, throw  if this function is not found in the map.
        /// TODO: need to be async when this access datastore
        /// </summary>
        /// <param name="functionFullName"></param>
        FunctionMetadata GetFunctionMetadata(string functionFullName);
        
        /// <summary>
        /// Update metadata of an existing function.
        /// This function should only be called when this contract legally update.
        /// </summary>
        /// <param name="functionFullName"></param>
        /// <param name="otherFunctionsCallByThis"></param>
        /// <param name="nonRecursivePathSet"></param>
        bool UpdataExistingMetadata(string functionFullName, HashSet<string> otherFunctionsCallByThis, HashSet<Hash> nonRecursivePathSet);
        
    }
}