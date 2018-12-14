using System;
using System.IO;
using System.Reflection;
using AElf.CLI2.JS;
using Alba.CsConsoleFormat.Fluent;
using CommandLine;

namespace AElf.CLI2.Commands.CrossChain
{
    [Verb("request-chain-creation", HelpText = "Request chain creation.")]
    public class ChainCreationRequestOption : BaseOption
    {
        [Value(0, HelpText = "Token will be locked for this chain creation. ", Required = true)]
        public int LockedToken { get; set; }
        
        [Value(1, HelpText = "Indexing price for this chain.", Required = true)]
        public int IndexingPrice { get; set; }
        
        [Value(2, MetaName = "contract", HelpText = "The compiled contract code file of the contract to be deployed when chain is created.",
            Required = true)]
        public string ContractName { get; set; }
        
        [Value(3, HelpText = "Resource to be locked for chain creation, in json format.", Required = true)]
        public string Resource { get; set; }
    }
    
    public class ChainCreationRequestCommand : Command
    {
        private readonly ChainCreationRequestOption _option;
        public ChainCreationRequestCommand(ChainCreationRequestOption option) : base(option)
        {
            _option = option;
        }

        public override void Execute()
        {
            InitChain();
            var name = _option.ContractName.EndsWith(@".dll")? _option.ContractName :string.Concat(_option.ContractName, ".dll");
            string path = Path.Combine(_option.ContractDir, name);
            if (!File.Exists(path))
            {
                Colors.WriteLine($@"Code file ""{_option.ContractName}"" doesn't exist.".DarkRed());
            }

            Console.WriteLine($"resource - {_option.Resource}");
            _engine.RunScript(Assembly.LoadFrom(Assembly.GetAssembly(typeof(JSEngine)).Location)
                .GetManifestResourceStream("AElf.CLI2.Scripts.cross-chain.js"));
            _engine.GlobalObject.CallMethod("request_chain_creation", _option.LockedToken, _option.IndexingPrice, _option.Resource, _option.Account,
                GetCode(path));
        }
    }
}