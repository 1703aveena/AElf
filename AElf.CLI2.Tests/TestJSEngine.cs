using System;
using System.IO;
using System.Reflection;
using AElf.CLI2.Commands;
using AElf.CLI2.JS;
using AElf.CLI2.JS.IO;
using AElf.CLI2.Tests.Utils;
using Autofac;
using Xunit;
using Xunit.Abstractions;

namespace AElf.CLI2.Tests
{
    public class TestJSEngine
    {
        private class UnittestBridgeJSProvider : IBridgeJSProvider
        {
            public Stream GetBridgeJSStream()
            {
                var location = Assembly.GetAssembly(typeof(IoCContainerBuilder)).Location;
                // NOTE: here we could inject some unittest special javascript files.
                return Assembly.LoadFrom(location).GetManifestResourceStream(BridgeJSProvider.BridgeJSResourceName);
            }
        }

        private IJSEngine GetJSEngine()
        {
            var option = new AccountOption
            {
                ServerAddr = "",
                Password = "",
                Action = AccountAction.create,
                AccountFileName = "a.account"
            };
            return IoCContainerBuilder.Build(option, new UnittestBridgeJSProvider(),
                new UTLogModule(_output)).Resolve<IJSEngine>();
        }

        
        private readonly ITestOutputHelper _output;

        public TestJSEngine(ITestOutputHelper output)
        {
            this._output = output;
        }        
        [Fact]
        public void TestConsole()
        {
            var jsEngine = GetJSEngine();
            Assert.NotNull(jsEngine);
            jsEngine.RunScript(@"console.log(""hello"", ""world"");");
            jsEngine.RunScript(@"console.log(1, 1.2);");
            jsEngine.RunScript(@"ok = true");
            Assert.True(jsEngine.Get("ok").Value.ToBoolean());
        }

        [Fact]
        public void TestCrypto()
        {
            var jsEngine = GetJSEngine();
            jsEngine.RunScript(@"
var i8 = new Uint8Array(3);
crypto.getRandomValues(i8);
var i8_0 = i8[0];
var i8_1 = i8[1];
var i8_2 = i8[2];
");
            // TODO: Inject a mock random generator.
            for (var i = 0; i < 3; ++i)
            {
                Assert.True(jsEngine.Get($"i8_{i}").Value.ToInt32() < 256);
            }
        }

        [Fact]
        public void TestXMLHttpRequest()
        {
            var jsEngine = GetJSEngine();
            jsEngine.RunScript(@"
var request = new XMLHttpRequest()
request.open(""GET"", ""http://www.baidu.com"")
");
            Assert.Equal(jsEngine.Get("request").Get("readyState").Value.ToInt32(), 1);
        }

        [Fact]
        public void TestURL()
        {
            this._output.WriteLine(new Uri("http://www.baidu.com/abc").GetLeftPart(UriPartial.Authority));
        }
    }
}