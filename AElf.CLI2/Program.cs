using System;
using System.Reflection;
using AElf.CLI2.Commands;
using AElf.CLI2.JS;
using AElf.CLI2.JS.IO;
using Autofac;
using CommandLine;
using Console = System.Console;

namespace AElf.CLI2
{
    class Program
    {
        [Verb("another", HelpText = "...")]
        class AnotherVerb : BaseOption
        {
        }

        static int Main(string[] args)
        {
            return Parser.Default.ParseArguments<CreateOption, InteractiveOption, DeployOption, AnotherVerb>(args)
                .MapResult(
                    (CreateOption opt) =>
                    {
//                        var sdk = IoCContainerBuilder.Build(opt).Resolve<IAElfSdk>();
//                        sdk.Chain().ConnectChain();
                        new CreateCommand(opt).Execute();
                        return 0;
                    },
                    (InteractiveOption opt) =>
                    {
                        new InteractiveCommand(opt).Execute();
                        return 0;
                    },
                    (DeployOption opt) =>
                    {
                        new DeployCommand(opt).Execute();
                        return 0;
                    },
                    (AnotherVerb opt) => 0,
                    errs => 1);
        }
    }
}