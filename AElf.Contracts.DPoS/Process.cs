﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using AElf.Kernel;
using AElf.Kernel.Extensions;
using AElf.Sdk.CSharp;
using AElf.Sdk.CSharp.Types;
using Akka.Util.Internal;
using Google.Protobuf;
using Google.Protobuf.WellKnownTypes;
using ServiceStack;
using SharpRepository.Repository.Configuration;
using Api = AElf.Sdk.CSharp.Api;

namespace AElf.Contracts.DPoS
{
    public class Process : CSharpSmartContract
    {
        // Set: Election.SetMiningNodes()
        public Map MiningNodes = new Map("MiningNodes");
        
        public Map TimeSlots = new Map("TimeSlots");
        
        public Map Signatures = new Map("Signatures");
        
        public Map RoundsCount = new Map("RoundsCount");
        
        public Map Ins = new Map("Ins");
        
        public Map Outs = new Map("Outs");

        public async Task<object> RandomizeOrderForFirstTwoRounds()
        {
            var miningNodes = (MiningNodes) await GetMiningNodes();
            var dict = new Dictionary<Hash, int>();
            
            // First round
            foreach (var node in miningNodes.Nodes)
            {
                dict.Add(node, new Random(GetTime().LastOrDefault()).Next(0, 1000));
            }

            var sortedMiningNodes =
                from obj in dict
                orderby obj.Value
                select obj.Key;

            var enumerable = sortedMiningNodes.ToList();
            for (var i = 0; i < enumerable.Count; i++)
            {
                Hash key = enumerable[i].CalculateHashWith(new Hash((new UInt64Value {Value = 1}).CalculateHash()));
                await TimeSlots.SetValueAsync(key, GetTime(i * 4 + 4));
            }
            
            // Second roud
            foreach (var node in miningNodes.Nodes)
            {
                dict[node] = new Random(GetTime().LastOrDefault()).Next(0, 1000);
            }
            
            sortedMiningNodes =
                from obj in dict
                orderby obj.Value
                select obj.Key;
            
            enumerable = sortedMiningNodes.ToList();
            for (var i = 0; i < enumerable.Count; i++)
            {
                Hash key = enumerable[i].CalculateHashWith(new Hash((new UInt64Value {Value = 1}).CalculateHash()));
                await TimeSlots.SetValueAsync(key, GetTime(i * 4 + miningNodes.Nodes.Count * 4 + 8));
            }

            return null;
        }

        public async Task<object> RandomizeSignaturesForFirstRound()
        {
            Hash roundsCountHash = ((UInt64Value) await GetRoundsCount()).CalculateHash();
            var miningNodes = ((MiningNodes) await GetMiningNodes()).Nodes.ToList();
            var miningNodesCount = miningNodes.Count;

            for (var i = 0; i < miningNodesCount; i++)
            {
                Hash key = miningNodes[i].CalculateHashWith(roundsCountHash);
                await Signatures.SetValueAsync(key, Hash.Generate().ToByteArray());
            }

            return null;
        }

        public async Task<object> GetTimeSlot(Hash accountHash)
        {
            var roundsCount = (UInt64Value) await GetRoundsCount();
            var key = accountHash.CalculateHashWith((Hash) roundsCount.CalculateHash());
            var timeSlot = await TimeSlots.GetValue(key);

            Api.Return(new BytesValue {Value = ByteString.CopyFrom(timeSlot)});
            
            return timeSlot;
        }

        public async Task<object> AbleToMine(Hash accountHash)
        {
            var assignedTimeSlot = (byte[]) await GetTimeSlot(accountHash);
            var timeSlotEnd = DateTime
                .Parse(Encoding.UTF8.GetString(assignedTimeSlot))
                .AddMinutes(4)
                .ToString("yyyy-MM-dd HH:mm:ss.ffffff")
                .ToUtf8Bytes();

            var can = CompareBytes(assignedTimeSlot, GetTime()) && CompareBytes(timeSlotEnd, assignedTimeSlot);
            
            Api.Return(new BoolValue {Value = can});
            
            return can;
        }
        
        public async Task<object> CalculateSignature(Hash accountHash)
        {
            // Get signatures of last round.
            var currentRoundCount = (UInt64Value) await GetRoundsCount();
            var lastRoundCount = new UInt64Value {Value = currentRoundCount.Value - 1};
            Hash roundCountHash = lastRoundCount.CalculateHash();

            var add = Hash.Zero;
            var miningNodes = (MiningNodes) await GetMiningNodes();
            foreach (var node in miningNodes.Nodes)
            {
                Hash key = node.CalculateHashWith(roundCountHash);
                Hash lastSignature = await Signatures.GetValue(key);
                add = add.CalculateHashWith(lastSignature);
            }

            var inValue = (Hash) await Ins.GetValue(accountHash);
            Hash signature = inValue.CalculateHashWith(add);
            
            Api.Return(signature);

            return signature;
        }
        
        public async Task<object> GetMiningNodes()
        {
            // Should be set before
            var miningNodes = AElf.Kernel.MiningNodes.Parser.ParseFrom(
                await MiningNodes.GetValue(Hash.Zero));

            if (miningNodes.Nodes.Count < 1)
            {
                throw new ConfigurationErrorsException("No mining nodes.");
            }
            
            Api.Return(miningNodes);

            return miningNodes;
        }

        public async Task<object> SetRoundsCount(ulong count)
        {
            await RoundsCount.SetValueAsync(Hash.Zero, new UInt64Value {Value = count}.ToByteArray());

            return null;
        }

        public async Task<object> GetRoundsCount()
        {
            var count = UInt64Value.Parser.ParseFrom(await RoundsCount.GetValue(Hash.Zero));
            
            Api.Return(count);
            
            return count;
        }

        public async Task<object> GenerateExtraBlockTransactions()
        {
            throw new NotImplementedException();
        }

        public async Task<object> SetOutValue(Hash accountHash, Hash outValue)
        {
            var roundsCount = (UInt64Value) await GetRoundsCount();
            Hash key = accountHash.CalculateHashWith(roundsCount);
            await Outs.SetValueAsync(key, outValue.ToByteArray());

            return null;
        }

        public async Task<object> PublishInValue(Hash accountHash, Hash inValue)
        {
            var roundsCount = (UInt64Value) await GetRoundsCount();
            var key = accountHash.CalculateHashWith((Hash) roundsCount.CalculateHash());
            var timeSlot = await TimeSlots.GetValue(key);

            if (CompareBytes(GetTime(-4), timeSlot))
            {
                await Ins.SetValueAsync(key, inValue.ToByteArray());
                return true;
            }

            return false;
        }
        
        public override async Task InvokeAsync()
        {
            var tx = Api.GetTransaction();

            var methodname = tx.MethodName;
            var type = GetType();
            var member = type.GetMethod(methodname);
            var parameters = Parameters.Parser.ParseFrom(tx.Params).Params.Select(p => p.Value()).ToArray();

            if (member != null) await (Task<object>) member.Invoke(this, parameters);
        }

        private byte[] GetTime()
        {
            return DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.ffffff").ToUtf8Bytes();
        }

        private byte[] GetTime(int offset)
        {
            return DateTime.Now.AddMinutes(offset).ToString("yyyy-MM-dd HH:mm:ss.ffffff").ToUtf8Bytes();
        }

        private bool CompareBytes(byte[] bytes1, byte[] bytes2)
        {
            //Caonnot compare
            if (bytes1.Length != bytes2.Length)
            {
                return false;
            }

            var length = bytes1.Length;
            for (var i = 0; i < length; i++)
            {
                if (bytes1[i] > bytes2[i])
                {
                    return true;
                }
            }

            return false;
        }
    }
}