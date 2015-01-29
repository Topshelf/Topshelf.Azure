namespace SampleWorkerRoleService
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using Topshelf;


    class SampleService :
        ServiceControl
    {
        readonly CancellationTokenSource _cancel = new CancellationTokenSource();
        Task _task;

        public bool Start(HostControl hostControl)
        {
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
                Console.WriteLine("Goodbye, Cruel World.");
                return;
            }

            await Task.Yield();

            Console.WriteLine("Hello, World.");

            await Task.Delay(1000);

            await Boring();
        }
    }
}