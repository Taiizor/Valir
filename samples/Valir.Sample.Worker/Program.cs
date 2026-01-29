using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Spectre.Console;
using StackExchange.Redis;
using System.Text;
using Valir.Abstractions;
using Valir.Core;
using Valir.Redis;

Console.InputEncoding = Encoding.UTF8;
Console.OutputEncoding = Encoding.UTF8;

// Parse command line args simply
string redis = GetArg(args, "--redis", "localhost:6379");
string queues = GetArg(args, "--queues", "default");
int concurrency = int.Parse(GetArg(args, "--concurrency", "4"));
bool interactive = !args.Contains("--headless");

ValirOptions options = new()
{
    RedisConnectionString = redis,
    Concurrency = concurrency,
    Queues = queues.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
};

if (interactive)
{
    await RunWithTui(options);
}
else
{
    await RunHeadless(options);
}

static string GetArg(string[] args, string name, string defaultValue)
{
    for (int i = 0; i < args.Length - 1; i++)
    {
        if (args[i].Equals(name, StringComparison.OrdinalIgnoreCase))
        {
            return args[i + 1];
        }
    }
    return defaultValue;
}

static async Task RunWithTui(ValirOptions options)
{
    AnsiConsole.Write(new FigletText("Valir Worker").Color(Color.Cyan1));

    AnsiConsole.MarkupLine($"[grey]Redis:[/] {options.RedisConnectionString}");
    AnsiConsole.MarkupLine($"[grey]Queues:[/] {string.Join(", ", options.Queues)}");
    AnsiConsole.MarkupLine($"[grey]Concurrency:[/] {options.Concurrency}");
    AnsiConsole.WriteLine();

    await AnsiConsole.Status()
        .StartAsync("Connecting to Redis...", async ctx =>
        {
            ConnectionMultiplexer redis = await ConnectionMultiplexer.ConnectAsync(options.RedisConnectionString);
            ctx.Status("Redis connected!");

            RedisJobQueue queue = new(redis, options);
            CancellationTokenSource cts = new();

            Console.CancelKeyPress += (_, e) =>
            {
                e.Cancel = true;
                AnsiConsole.MarkupLine("[yellow]⏹ Shutdown requested, draining jobs...[/]");
                cts.Cancel();
            };

            ctx.Status($"[green]▶ Processing queues: {string.Join(", ", options.Queues)}[/]");

            WorkerRuntime worker = new(queue, DemoHandler, options);
            await worker.StartAsync(cts.Token);

            try
            {
                await Task.Delay(Timeout.Infinite, cts.Token);
            }
            catch (OperationCanceledException)
            {
                // Expected on shutdown
            }

            await worker.StopAsync(CancellationToken.None);
            AnsiConsole.MarkupLine("[green]✓ Graceful shutdown complete![/]");
        });
}

static async Task RunHeadless(ValirOptions options)
{
    IHost host = Host.CreateDefaultBuilder()
        .ConfigureServices(services =>
        {
            services.AddSingleton(options);
            services.AddSingleton<IConnectionMultiplexer>(sp =>
                ConnectionMultiplexer.Connect(options.RedisConnectionString));
            services.AddSingleton<IJobQueue, RedisJobQueue>();
            services.AddHostedService<WorkerHostedService>();
        })
        .Build();

    await host.RunAsync();
}

static Task DemoHandler(JobEnvelope job, JobContext ctx)
{
    AnsiConsole.MarkupLine($"[blue]→ Job:[/] {job.Id} [grey]({job.Type})[/]");
    return Task.CompletedTask;
}

internal sealed class WorkerHostedService(IJobQueue queue, ValirOptions options) : IHostedService
{
    private readonly WorkerRuntime _worker = new(queue, DemoHandler, options);

    public Task StartAsync(CancellationToken ct)
    {
        return _worker.StartAsync(ct);
    }

    public Task StopAsync(CancellationToken ct)
    {
        return _worker.StopAsync(ct);
    }

    private static Task DemoHandler(JobEnvelope job, JobContext ctx)
    {
        Console.WriteLine($"Processing job: {job.Id} ({job.Type})");
        return Task.CompletedTask;
    }
}
