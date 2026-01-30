using StackExchange.Redis;
using System.Text;
using System.Text.Json;
using Valir.Abstractions;
using Valir.Core;
using Valir.Redis;

Console.InputEncoding = Encoding.UTF8;
Console.OutputEncoding = Encoding.UTF8;

// Setup Consumer
Console.WriteLine("👷 Valir Consumer (Worker) starting...");

string redisConnectionString = "localhost:6379";
ValirOptions options = new()
{
    RedisConnectionString = redisConnectionString,
    Concurrency = 4 // Increased concurrency to handle multiple types better
};

ConnectionMultiplexer redisConnection = await ConnectionMultiplexer.ConnectAsync(redisConnectionString);
RedisJobQueue queue = new(redisConnection, options);

// Handlers
EmailJobHandler emailHandler = new();
ImageProcessJobHandler imageHandler = new();

// Dispatcher logic
Func<JobEnvelope, JobContext, Task> dispatcher = async (envelope, context) =>
{
    switch (envelope.Type)
    {
        case "send-email":
            EmailRequest? emailReq = JsonSerializer.Deserialize<EmailRequest>(envelope.Payload);
            if (emailReq != null)
            {
                await emailHandler.HandleAsync(emailReq, context);
            }

            break;

        case "process-image":
            ImageProcessRequest? imageReq = JsonSerializer.Deserialize<ImageProcessRequest>(envelope.Payload);
            if (imageReq != null)
            {
                await imageHandler.HandleAsync(imageReq, context);
            }

            break;

        default:
            Console.WriteLine($"[Consumer] Unknown job type: {envelope.Type}");
            break;
    }
};

WorkerRuntime worker = new(queue, dispatcher, options);

Console.WriteLine("[Consumer] Worker started. Press Ctrl+C to stop.");
CancellationTokenSource cts = new();
Console.CancelKeyPress += (s, e) => { e.Cancel = true; cts.Cancel(); };

await worker.StartAsync(cts.Token);

try
{
    await Task.Delay(-1, cts.Token);
}
catch (OperationCanceledException)
{
    Console.WriteLine("[Consumer] Stopping worker...");
}

await worker.StopAsync(CancellationToken.None);
Console.WriteLine("[Consumer] Graceful shutdown complete.");

// --- Records & Handlers ---

public record EmailRequest(string To, string Subject, string Body);
public record ImageProcessRequest(string Path, string Action, int Width, int Height);

public class EmailJobHandler : IJobHandler<EmailRequest>
{
    public async Task HandleAsync(EmailRequest job, JobContext context)
    {
        Console.WriteLine($"[Consumer] 📧 Sending Email to {job.To} (Job: {context.JobId})");
        await Task.Delay(500, context.CancellationToken); // Simulate work
    }
}

public class ImageProcessJobHandler : IJobHandler<ImageProcessRequest>
{
    public async Task HandleAsync(ImageProcessRequest job, JobContext context)
    {
        Console.WriteLine($"[Consumer] 🖼️ Processing Image: {job.Path} -> {job.Action} ({job.Width}x{job.Height}) (Job: {context.JobId})");
        await Task.Delay(1500, context.CancellationToken); // Simulate heavy work
    }
}
