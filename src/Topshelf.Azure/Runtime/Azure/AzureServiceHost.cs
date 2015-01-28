namespace Topshelf.Runtime.Azure
{
    using System;
    using System.IO;
    using System.Reflection;
    using System.Threading;
    using System.Threading.Tasks;
    using Logging;
    using Microsoft.WindowsAzure.ServiceRuntime;


    public class AzureServiceHost :
        RoleEntryPoint,
        Host,
        HostControl
    {
        static readonly LogWriter _log = HostLogger.Get<AzureServiceHost>();
        readonly HostEnvironment _environment;
        readonly ServiceHandle _serviceHandle;
        readonly HostSettings _settings;
        int _deadThread;
        bool _disposed;
        Exception _unhandledException;

        public AzureServiceHost(HostEnvironment environment, HostSettings settings, ServiceHandle serviceHandle)
        {
            if (settings == null)
                throw new ArgumentNullException("settings");
            if (serviceHandle == null)
                throw new ArgumentNullException("serviceHandle");

            _settings = settings;
            _serviceHandle = serviceHandle;
            _environment = environment;
//
//            CanPauseAndContinue = settings.CanPauseAndContinue;
//            CanShutdown = settings.CanShutdown;
//            CanHandleSessionChangeEvent = settings.CanSessionChanged;
//            ServiceName = _settings.ServiceName;
        }

        TopshelfExitCode Host.Run()
        {
            Run();

            return TopshelfExitCode.Ok;
        }

        void HostControl.RequestAdditionalTime(TimeSpan timeRemaining)
        {
            _log.DebugFormat("Requesting additional time: {0}", timeRemaining);

//            RequestAdditionalTime((int)timeRemaining.TotalMilliseconds);
        }

        void HostControl.Restart()
        {
            _log.Fatal("Restart is not yet implemented");

            throw new NotImplementedException("This is not done yet, so I'm trying");
        }

        void HostControl.Stop()
        {
            //          if (CanStop)
            {
                _log.Debug("Stop requested by hosted service");

                // TODO
                // this should ask the role to restart
                //Stop();
            }
//            else
//            {
//                _log.Debug("Stop requested by hosted service, but service cannot be stopped at this time");
//                throw new ServiceControlException("The service cannot be stopped at this time");
//            }
        }

        public override void Run()
        {
            Directory.SetCurrentDirectory(AppDomain.CurrentDomain.BaseDirectory);

            AppDomain.CurrentDomain.UnhandledException += CatchUnhandledException;

//            ExitCode = (int)TopshelfExitCode.Ok;

            _log.Info("Starting as a Windows service");

            if (!_environment.IsServiceInstalled(_settings.ServiceName))
            {
                string message = string.Format("The {0} service has not been installed yet. Please run '{1} install'.",
                    _settings, Assembly.GetEntryAssembly().GetName());
                _log.Fatal(message);

//                ExitCode = (int)TopshelfExitCode.ServiceNotInstalled;
                throw new TopshelfException(message);
            }

            _log.Debug("[Topshelf] Starting up as a windows service application");

            base.Run();

//            return (TopshelfExitCode)Enum.ToObject(typeof(TopshelfExitCode), 0); //ExitCode);
        }

        public override bool OnStart()
        {
            try
            {
                _log.Info("[Topshelf] Starting");

                Directory.SetCurrentDirectory(AppDomain.CurrentDomain.BaseDirectory);

                _log.DebugFormat("[Topshelf] Current Directory: {0}", Directory.GetCurrentDirectory());

                if (!_serviceHandle.Start(this))
                    throw new TopshelfException("The service did not start successfully (returned false).");

                _log.Info("[Topshelf] Started");

                return true;
            }
            catch (Exception ex)
            {
                _log.Fatal("The service did not start successfully", ex);

               // ExitCode = (int)TopshelfExitCode.StartServiceFailed;
                throw;
            }
        }

        public override void OnStop()
        {
            try
            {
                _log.Info("[Topshelf] Stopping");

                if (!_serviceHandle.Stop(this))
                    throw new TopshelfException("The service did not stop successfully (return false).");

                _log.Info("[Topshelf] Stopped");
            }
            catch (Exception ex)
            {
                _log.Fatal("The service did not shut down gracefully", ex);
                //  ExitCode = (int)TopshelfExitCode.StopServiceFailed;
                throw;
            }

            if (_unhandledException != null)
            {
                //     ExitCode = (int)TopshelfExitCode.UnhandledServiceException;
                _log.Info("[Topshelf] Unhandled exception detected, rethrowing to cause application to restart.");
                throw new InvalidOperationException("An unhandled exception was detected", _unhandledException);
            }
        }


        protected void Dispose(bool disposing)
        {
            if (disposing && !_disposed)
            {
                if (_serviceHandle != null)
                    _serviceHandle.Dispose();

                _disposed = true;
            }

//            base.Dispose(disposing);
        }

        void CatchUnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
            _log.Fatal("The service threw an unhandled exception", (Exception)e.ExceptionObject);

            HostLogger.Shutdown();

            //            // IF not terminating, then no reason to stop the service?
            //            if (!e.IsTerminating)
            //                return;
            //          This needs to be a configuration option to avoid breaking compatibility

            //   ExitCode = (int)TopshelfExitCode.UnhandledServiceException;
            _unhandledException = e.ExceptionObject as Exception;

            // TODO request service Stop/Restart
//            Stop();


            // it isn't likely that a TPL thread should land here, but if it does let's no block it
            if (Task.CurrentId.HasValue)
                return;

            int deadThreadId = Interlocked.Increment(ref _deadThread);
            Thread.CurrentThread.IsBackground = true;
            Thread.CurrentThread.Name = "Unhandled Exception " + deadThreadId.ToString();
            while (true)
                Thread.Sleep(TimeSpan.FromHours(1));
        }
    }
}