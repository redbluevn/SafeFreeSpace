using SafeFreeSpace.Core.Interfaces;
using SafeFreeSpace.ElevatedWorker;
using SafeFreeSpace.ElevatedWorker.Executors;
using SafeFreeSpace.Infrastructure.Windows.Storage;

IProcessRunner runner = new ProcessRunner();
IVolumeInventory inventory = new WindowsVolumeInventory();

var host = new WorkerHost(new IOperationExecutor[]
{
    new CipherExecutor(runner, inventory),
    new RetrimExecutor(runner, inventory),
    new CancelOperationExecutor()
});

return await host.RunAsync(args);
