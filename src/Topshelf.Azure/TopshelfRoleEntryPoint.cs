namespace Topshelf
{
    using System;
    using System.IO;
    using System.Threading;
    using System.Threading.Tasks;
    using Builders;
    using HostConfigurators;
    using Logging;
    using Microsoft.WindowsAzure.ServiceRuntime;
    using Runtime;


    public abstract class TopshelfRoleEntryPoint :
        RoleEntryPoint,
        Host,
        HostControl
    {
        static readonly LogWriter _log = HostLogger.Get<TopshelfRoleEntryPoint>();

        int _deadThread;
        bool _disposed;
        HostEnvironment _environment;
        ManualResetEvent _exit;
        ManualResetEvent _exited;
        Host _host;
        ServiceHandle _serviceHandle;
        HostSettings _settings;

        TopshelfExitCode Host.Run()
        {
            Directory.SetCurrentDirectory(AppDomain.CurrentDomain.BaseDirectory);

            AppDomain.CurrentDomain.UnhandledException += CatchUnhandledException;

            if (_environment.IsServiceInstalled(_settings.ServiceName))
            {
                if (!_environment.IsServiceStopped(_settings.ServiceName))
                {
                    _log.ErrorFormat("The {0} service is running and must be stopped before running via the console",
                        _settings.ServiceName);

                    return TopshelfExitCode.ServiceAlreadyRunning;
                }
            }

            bool started = false;
            try
            {
                _log.Debug("Starting up as a console application");

                _exit = new ManualResetEvent(false);
                _exited = new ManualResetEvent(false);

                if (!_serviceHandle.Start(this))
                    throw new TopshelfException("The service failed to start (return false).");

                started = true;

                _log.InfoFormat("The {0} service is now running, press Control+C to exit.", _settings.ServiceName);

                _exit.WaitOne();
            }
            catch (Exception ex)
            {
                _log.Error("An exception occurred", ex);

                return TopshelfExitCode.AbnormalExit;
            }
            finally
            {
                if (started)
                    _serviceHandle.Stop(this);

                _exited.Set();

                _exit.Close();
                (_exit as IDisposable).Dispose();

                HostLogger.Shutdown();
            }

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


        protected abstract void Configure(HostConfigurator hostConfigurator);

        public override void Run()
        {
            _log.Debug("[Topshelf] Starting up as an Azure Worker role");

            _host = HostFactory.New(x =>
            {
                Configure(x);

                x.UseHostBuilder((environment, settings) => new Builder(this, environment, settings));
            });

            _host.Run();
        }

        public override void OnStop()
        {
            if (_exit != null)
            {
                _exit.Set();

                _exited.WaitOne();
            }

            if (_serviceHandle != null)
                _serviceHandle.Dispose();
        }

        void CatchUnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
            _log.Fatal("The service threw an unhandled exception", (Exception)e.ExceptionObject);

            HostLogger.Shutdown();

            //            // IF not terminating, then no reason to stop the service?
            //            if (!e.IsTerminating)
            //                return;
            //          This needs to be a configuration option to avoid breaking compatibility

            RoleEnvironment.RequestRecycle();

            // it isn't likely that a TPL thread should land here, but if it does let's no block it
            if (Task.CurrentId.HasValue)
                return;

            int deadThreadId = Interlocked.Increment(ref _deadThread);
            Thread.CurrentThread.IsBackground = true;
            Thread.CurrentThread.Name = "Unhandled Exception " + deadThreadId.ToString();
            while (true)
                Thread.Sleep(TimeSpan.FromHours(1));
        }


        class Builder :
            HostBuilder
        {
            readonly HostEnvironment _environment;
            readonly TopshelfRoleEntryPoint _host;
            readonly HostSettings _settings;


            public Builder(TopshelfRoleEntryPoint host, HostEnvironment environment, HostSettings settings)
            {
                _host = host;

                _host._settings = settings;
                _host._environment = environment;
                _environment = environment;
                _settings = settings;
            }

            public HostEnvironment Environment
            {
                get { return _environment; }
            }

            public HostSettings Settings
            {
                get { return _settings; }
            }

            public Host Build(ServiceBuilder serviceBuilder)
            {
                _host._serviceHandle = serviceBuilder.Build(_settings);

                return _host;
            }

            public void Match<T>(Action<T> callback)
                where T : class, HostBuilder
            {
            }
        }
    }
}