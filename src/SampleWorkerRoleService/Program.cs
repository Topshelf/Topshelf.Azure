namespace SampleWorkerRoleService
{
    using Topshelf;
    using Topshelf.HostConfigurators;


    public class Program :
        TophelfServiceConfigurator
    {
        public void Configure(HostConfigurator hostConfigurator)
        {
            hostConfigurator.Service<SampleService>();
        }

        static int Main()
        {
            return (int)HostFactory.Run(new Program().Configure);
        }
    }
}