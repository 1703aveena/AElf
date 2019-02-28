(function () {
    deployCommand = function (category, code) {
        var txHash = chain.contractZero.DeploySmartContract(category, code).TransactionId;
        console.log("Deploying contract ...");
        console.log("TransactionId is: " + txHash);
        // _repeatedCalls(function () {
        //     var res = aelf.chain.getTxResult(txHash);
        //     if (res.tx_status !== "Pending") {
        //         console.log("TxStatus is: " + res.tx_status);
        //     }
        //     if (res.tx_status === "Mined") {
        //         console.log("Address is: " + res.return);
        //     }
        //     return res.tx_status !== "Pending";
        // }, 3000);
    };

    updateCommand = function (address, code) {
        var txHash = chain.contractZero.UpdateSmartContract(address, code).hash;
        console.log("Updating contract ...");
        console.log("TxHash is: " + txHash);
        _repeatedCalls(function () {
            var res = aelf.chain.getTxResult(txHash).result;
            if (res.tx_status !== "Pending") {
                console.log("TxStatus is: " + res.tx_status);
            }
            if (res.tx_status === "Mined") {
                console.log("Result is: " + res.return);
            }
            return res.tx_status !== "Pending";
        }, 3000);
    };
    
    changeOwnerCommand = function (contractAddress, newOwner) {
        var txHash = chain.contractZero.ChangeContractOwner(contractAddress, newOwner).hash;
        console.log("Changing contract owner ...");
        console.log("TxHash is: " + txHash);
        _repeatedCalls(function () {
            var res = aelf.chain.getTxResult(txHash).result;
            if (res.tx_status !== "Pending") {
                console.log("TxStatus is: " + res.tx_status);
            }
            if (res.tx_status === "Mined") {
                console.log("Success");
            }
            return res.tx_status !== "Pending";
        }, 3000);
    };
})();