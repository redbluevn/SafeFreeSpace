namespace SafeFreeSpace.ElevatedWorker;

using System.Text;
using System.Threading.Channels;
using SafeFreeSpace.Contracts;
using SafeFreeSpace.ElevatedWorker.Executors;
using SafeFreeSpace.Infrastructure.Windows.Pipes;

public sealed class WorkerHost
{
    private readonly IReadOnlyDictionary<WorkerOperationType, IOperationExecutor> _executors;

    public WorkerHost(IEnumerable<IOperationExecutor> executors)
    {
        _executors = executors.ToDictionary(e => e.OperationType);
    }

    public async Task<int> RunAsync(string[] args)
    {
        if (args.Length != 4)
        {
            Console.Error.WriteLine("Usage: SafeFreeSpace.ElevatedWorker <protocolVersion> <base64PipeName> <base64Nonce> <base64OperationId>");
            return 1;
        }

        if (!int.TryParse(args[0], out int protocolVersion))
        {
            Console.Error.WriteLine("Invalid protocol version.");
            return 1;
        }

        string pipeName;
        string nonce;
        string operationId;
        try
        {
            pipeName = FromBase64(args[1]);
            nonce = FromBase64(args[2]);
            operationId = FromBase64(args[3]);
        }
        catch (FormatException)
        {
            Console.Error.WriteLine("Invalid base64 argument.");
            return 1;
        }

        using var client = new WorkerPipeClient(pipeName);
        await client.ConnectAsync();
        await client.SendHandshakeAsync(new ProtocolHandshake(protocolVersion, nonce, operationId));

        using var cts = new CancellationTokenSource();

        // Single writer keeps progress ordering; a broken pipe (UI died)
        // cancels the running operation as a dead-man switch.
        var progressChannel = Channel.CreateUnbounded<OperationProgress>();
        Task writerTask = Task.Run(async () =>
        {
            try
            {
                await foreach (OperationProgress p in progressChannel.Reader.ReadAllAsync())
                {
                    await client.SendProgressAsync(p);
                }
            }
            catch (Exception)
            {
                cts.Cancel();
            }
        });

        var progressReporter = new Progress<OperationProgress>(p =>
        {
            progressChannel.Writer.TryWrite(p);
        });

        try
        {
            WorkerRequest request = await client.ReadRequestAsync();
            if (request.ProtocolVersion != protocolVersion ||
                !string.Equals(request.Nonce, nonce, StringComparison.Ordinal) ||
                !string.Equals(request.OperationId, operationId, StringComparison.Ordinal))
            {
                return await SendFinalAsync(new WorkerResponse(false, OperationResult.Failed, "PipeAuthenticationFailed", "Authentication failed."));
            }

            if (!_executors.TryGetValue(request.OperationType, out var executor))
            {
                return await SendFinalAsync(new WorkerResponse(false, OperationResult.Failed, "UnsupportedVolume", $"Operation {request.OperationType} is not allowed."));
            }

            WorkerResponse response;
            try
            {
                response = await executor.ExecuteAsync(request, progressReporter, cts.Token);
            }
            catch (OperationCanceledException)
            {
                response = new WorkerResponse(false, OperationResult.Interrupted, "Cancelled", "Operation was cancelled.");
            }

            return await SendFinalAsync(response);
        }
        catch (Exception ex)
        {
            return await SendFinalAsync(new WorkerResponse(false, OperationResult.Failed, "Unexpected", ex.Message));
        }

        // Drains queued progress before the final response so the response
        // can never overtake a pending progress message on the pipe.
        async Task<int> SendFinalAsync(WorkerResponse finalResponse)
        {
            progressChannel.Writer.TryComplete();
            await writerTask;
            try
            {
                await client.SendResponseAsync(finalResponse);
            }
            catch (Exception)
            {
                cts.Cancel();
                return 1;
            }

            return finalResponse.Success ? 0 : 1;
        }
    }

    private static string FromBase64(string value)
    {
        return Encoding.UTF8.GetString(Convert.FromBase64String(value));
    }
}
