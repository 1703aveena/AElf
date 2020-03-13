using AElf.Kernel.SmartContract;
using AElf.Kernel.SmartContract.ExecutionPluginForMethodFee;
using Microsoft.AspNetCore.Builder;
using AElf.Kernel.TransactionPool.Infrastructure;
using AElf.OS;
using Microsoft.Extensions.DependencyInjection;
using Volo.Abp.Modularity.PlugIns;

namespace AElf.WebApp.Application
{
    public class WebAppTestStartup
    {
        public void ConfigureServices(IServiceCollection services)
        {
            services.AddSingleton<ITxHub, MockTxHub>();
            services.AddApplication<WebAppTestAElfModule>(options =>
                options.PlugInSources.AddTypes(typeof(ExecutionPluginForMethodFeeModule)));
            services.Configure<ContractOptions>(options => { options.IsTxExecutionTimeoutEnabled = false; });
        }

        public void Configure(IApplicationBuilder app)
        {
            app.UseRouting();
            app.InitializeApplication();
            app.UseCors(builder =>
                builder.AllowAnyHeader().AllowAnyOrigin().AllowAnyMethod());
        }
    }
}