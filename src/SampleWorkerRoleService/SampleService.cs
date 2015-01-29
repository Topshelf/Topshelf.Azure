namespace SampleWorkerRoleService
{
    using System.Threading;
    using System.Threading.Tasks;
    using Topshelf;
    using Topshelf.Logging;


    class SampleService :
        ServiceControl
    {
        readonly CancellationTokenSource _cancel = new CancellationTokenSource();
        readonly LogWriter _log = HostLogger.Get<SampleService>();
        readonly string _testPhrase;
        Task _task;

        public SampleService(string testPhrase)
        {
            _testPhrase = testPhrase;
        }

        public bool Start(HostControl hostControl)
        {
            _log.InfoFormat("Service starting up: {0}", _testPhrase);

            _task = Boring();

            return true;
        }

        public bool Stop(HostControl hostControl)
        {
            _cancel.Cancel();

            _task.Wait();

            return true;
        }

        async Task Boring()
        {
            if (_cancel.Token.IsCancellationRequested)
            {
                _log.Info("Goodbye, Cruel World.");
                return;
            }

            await Task.Yield();

            _log.Info("Hello, World.");

            await Task.Delay(1000);

            await Boring();
        }
    }
}