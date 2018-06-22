﻿using System;
using System.IO;
using System.Linq;
using AElf.CLI.Parsing;
using AElf.CLI.RPC;
using Google.Protobuf;
using Google.Protobuf.WellKnownTypes;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using ProtoBuf;
using Method = AElf.CLI.Data.Protobuf.Method;

namespace AElf.CLI.Command
{
    public class LoadContractAbiCmd : CliCommandDefinition
    {
        public const string Name = "load-contract-abi";
        
        public LoadContractAbiCmd() : base(Name)
        {
            
        }

        public override string GetUsage()
        {
            return "load-contract-abi <contractAddress>";
        }

        public override string Validate(CmdParseResult parsedCmd)
        {
            if (parsedCmd.Args == null || parsedCmd.Args.Count != 1)
            {
                return "Invalid number of arguments.";
            }

            return null;
        }
        
        public override JObject BuildRequest(CmdParseResult parsedCmd)
        {
            var reqParams = new JObject { ["address"] = parsedCmd.Args.ElementAt(0) };
            var req = JsonRpcHelpers.CreateRequest(reqParams, "get_contract_abi", 1);

            return req;
        }

        public override string GetPrintString(JObject resp)
        {
            JToken ss = resp["abi"];
            byte[] aa = Convert.FromBase64String(ss.ToString());
            
            MemoryStream ms = new MemoryStream(aa);
            Method m = Serializer.Deserialize<Method>(ms);

            return JsonConvert.SerializeObject(m);
        }
    }
}