using Autofac;

namespace AElf.Kernel.Modules.AutofacModule
{
    public class Module: Autofac.Module
    {
        protected override void Load(ContainerBuilder builder)
        {
            var assembly = typeof(AElf.Kernel.IAccount).Assembly;
            
            builder.RegisterAssemblyTypes(assembly).AsImplementedInterfaces();

            builder.RegisterType(typeof(Hash)).As(typeof(IHash));

            base.Load(builder);
        }
    }
}