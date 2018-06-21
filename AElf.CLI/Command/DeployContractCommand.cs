﻿using System.Linq;
using AElf.CLI.Parsing;
using AElf.CLI.RPC;
using Newtonsoft.Json.Linq;

namespace AElf.CLI.Command
{
    public class DeployContractCommand : CliCommandDefinition
    {
        private const string Name = "deploy-contract";
        
        public DeployContractCommand() : base(Name)
        {
        }

        public override string GetUsage()
        {
            return "deploy-contract ";
        }

        public override string Validate(CmdParseResult parsedCmd)
        {
            return null;
        }
        
        public override JObject BuildRequest(CmdParseResult parsedCmd)
        {
            /*var reqParams = new JObject { ["address"] = parsedCmd.Args.ElementAt(0) };
            var req = JsonRpcHelpers.CreateRequest(reqParams, "get_increment", 1);*/

            return null;
        }
    }
}