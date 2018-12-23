using Autofac;
using Microsoft.Extensions.DependencyInjection;
using Volo.Abp;

namespace AElf.Database
{
    public static class DatabaseAbpApplicationCreationOptionsExtensions
    {
        public static void UseInMemoryDatabase(this AbpApplicationCreationOptions options)
        {
            options.Services.AddTransient<IKeyValueDatabase, InMemoryDatabase>();
        }
        public static void UseRedisDatabase(this AbpApplicationCreationOptions options)
        {
            options.Services.AddTransient<IKeyValueDatabase, RedisDatabase>();
        }
        public static void UseSsdbDatabase(this AbpApplicationCreationOptions options)
        {
            options.Services.AddTransient<IKeyValueDatabase, SsdbDatabase>();
        }
    }
}