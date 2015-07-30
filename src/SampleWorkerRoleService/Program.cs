namespace SampleWorkerRoleService
{
    using Microsoft.Azure;
    using Topshelf;
    using Topshelf.HostConfigurators;
    using Topshelf.Logging;


    public class Program :
        TopshelfRoleEntryPoint
    {
        readonly LogWriter _log = HostLogger.Get<Program>();

        protected override void Configure(HostConfigurator hostConfigurator)
        {
            // load azure settings here
            string testPhrase = CloudConfigurationManager.GetSetting("TestPhrase");

            hostConfigurator.Service(settings => new SampleService(testPhrase), x =>
            {
                x.BeforeStartingService(context => _log.Info("Before starting service!!"));
                x.AfterStoppingService(context => _log.Info("After stopping service!!"));
            });
        }

        static int Main()
        {
            return (int)HostFactory.Run(new Program().Configure);
        }
    }
}