﻿using System.Linq;
using Acs5;
using AElf.Contracts.Profit;
using AElf.Sdk.CSharp;
using Google.Protobuf.WellKnownTypes;

namespace AElf.Kernel.SmartContract.ExecutionPluginForAcs5.Tests.TestContract
{
    public class Contract : ContractContainer.ContractBase
    {
        public override Empty SetProfitReceivers(ProfitReceivers input)
        {
            AssertPerformedByContractOwner();

            var profitItems = State.ProfitContract.GetContractProfitItem.Call(Context.Self);
            Assert(profitItems.IsTreasuryProfitItem, "Invalid profit item.");

            State.ProfitId.Value = profitItems.ProfitId;
            State.ProfitContract.AddWeights.Send(new AddWeightsInput
            {
                ProfitId = profitItems.ProfitId,
                EndPeriod = long.MaxValue,
                Weights = {input.Value.Select(i => new WeightMap {Receiver = i.Address, Weight = i.Weight})}
            });

            return new Empty();
        }

        public override Empty SetMethodProfitFee(SetMethodProfitFeeInput input)
        {
            AssertPerformedByContractOwner();

            State.MethodProfitFees[input.Method] = new MethodProfitFee {SymbolToAmount = {input.SymbolToAmount}};
            return new Empty();
        }

        public override Empty SetMethodProfitFees(SetMethodProfitFeesInput input)
        {
            AssertPerformedByContractOwner();

            foreach (var methodProfitFee in input.MethodProfitFees)
            {
                State.MethodProfitFees[methodProfitFee.Key] = methodProfitFee.Value;
            }

            return new Empty();
        }

        public override MethodProfitFee GetMethodProfitFee(StringValue input)
        {
            return State.MethodProfitFees[input.Value] ?? new MethodProfitFee();
        }

        public override Empty ReceiveProfits(Empty input)
        {
            State.ProfitContract.ReleaseProfit.Send(new ReleaseProfitInput
            {
                ProfitId = State.ProfitId.Value,
                Period = State.ReleasedTimes.Value.Add(1)
            });

            State.ReleasedTimes.Value = State.ReleasedTimes.Value.Add(1);

            return new Empty();
        }

        public override Empty DummyMethod(Empty input)
        {
            return new Empty();
        }

        private void AssertPerformedByContractOwner()
        {
            var contractInfo = State.ASC0Contract.GetContractInfo.Call(Context.Self);
            Assert(Context.Sender == contractInfo.Owner, "Only owner are permitted to call this method.");
        }
    }
}