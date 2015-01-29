namespace Topshelf.Runtime.Azure
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Reflection;
    using System.Threading;
    using Hosts;
    using Logging;
    using Microsoft.WindowsAzure.ServiceRuntime;


    public class AzureServiceHost :
        RoleEntryPoint,
        Host,
        HostControl
    {
        static readonly LogWriter _log = HostLogger.Get<AzureServiceHost>();
        Host _host;

        public AzureServiceHost()
        {
        }

        TopshelfExitCode Host.Run()
        {
            Run();

            return TopshelfExitCode.Ok;
        }

        void HostControl.RequestAdditionalTime(TimeSpan timeRemaining)
        {
            _log.DebugFormat("Requesting additional time: {0}", timeRemaining);
        }

        void HostControl.Restart()
        {
            _log.Debug("Restart requested by service");

            RoleEnvironment.RequestRecycle();
        }

        void HostControl.Stop()
        {
            _log.Debug("Stop requested by hosted service");

            RoleEnvironment.RequestRecycle();
        }

        public override void Run()
        {
            _log.Debug("[Topshelf] Starting up as an Azure Worker role");

            List<TophelfServiceConfigurator> configurators = AppDomain.CurrentDomain.GetAssemblies()
                .SelectMany(x => x.GetTypes())
                .Where(x => x.IsClass && typeof(TophelfServiceConfigurator).IsAssignableFrom(x))
                .Select(x => (TophelfServiceConfigurator)Activator.CreateInstance(x))
                .ToList();

            if (configurators.Count == 0)
                throw new TopshelfException("No service configurators were found.");

            _host = HostFactory.New(x => configurators[0].Configure(x));
        }


        public override void OnStop()
        {
            if (_host != null)
            {
                var consoleHost = _host as ConsoleRunHost;
                if (consoleHost != null)
                {
                    FieldInfo fieldInfo = typeof(ConsoleRunHost).GetField("_exit");
                    var @event = (ManualResetEvent)fieldInfo.GetValue(consoleHost);

                    @event.Set();
                }
            }
        }
    }
}