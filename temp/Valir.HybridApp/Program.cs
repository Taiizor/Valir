using StackExchange.Redis;
using System.Text.Json;
using Valir.Core;
using Valir.Redis;

namespace Valir.HybridApp
{
    internal class Program
    {
        public record EmailRequest(string To, string Subject, string Body);

        static async Task Main()
        {
            ValirOptions options = new() { RedisConnectionString = "localhost:6379" };
            ConnectionMultiplexer redis = await ConnectionMultiplexer.ConnectAsync(options.RedisConnectionString);
            RedisJobQueue queue = new(redis, options);

            WorkerRuntime worker = new(queue, async (envelope, context) =>
            {
                EmailRequest? email = JsonSerializer.Deserialize<EmailRequest>(envelope.Payload);

                Console.WriteLine($"Sending email to {email?.To}...");
                Console.WriteLine($"Subject: {email?.Subject}");
                Console.WriteLine($"Body: {email?.Body}");
            }, options);

            await worker.StartAsync(CancellationToken.None);

            byte[] payload = JsonSerializer.SerializeToUtf8Bytes(new EmailRequest("taiizor@vegalya.com", "Hi", "Valir is awesome!"));
            Console.WriteLine("Enqueued job ID: {0}", await queue.EnqueueAsync("send-email", payload));

            Console.WriteLine("Press Enter to stop the worker...");
            Console.ReadKey();
        }
    }
}
