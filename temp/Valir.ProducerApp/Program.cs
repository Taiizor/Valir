using StackExchange.Redis;
using System.Text;
using System.Text.Json;
using Valir.Core;
using Valir.Redis;

Console.InputEncoding = Encoding.UTF8;
Console.OutputEncoding = Encoding.UTF8;

Console.WriteLine("🚀 Valir Producer starting...");

// Setup Redis and Valir
string redisConnectionString = "localhost:6379";
ValirOptions options = new()
{
    RedisConnectionString = redisConnectionString
};

ConnectionMultiplexer redisConnection = await ConnectionMultiplexer.ConnectAsync(redisConnectionString);
RedisJobQueue queue = new(redisConnection, options);

// --- Job 1: Email ---
EmailRequest emailReq = new("user@example.com", "Welcome to Valir!", "This is a test job.");
byte[] emailPayload = JsonSerializer.SerializeToUtf8Bytes(emailReq);

Console.WriteLine($"[Producer] Enqueuing Email job for: {emailReq.To}...");

await queue.EnqueueAsync(
    type: "send-email",
    payload: emailPayload,
    priority: 1,
    idempotencyKey: $"email-{Guid.NewGuid():N}"
);

// --- Job 2: Image Processing ---
ImageProcessRequest imageReq = new("photos/profile.jpg", "thumbnail", 200, 200);
byte[] imagePayload = JsonSerializer.SerializeToUtf8Bytes(imageReq);

Console.WriteLine($"[Producer] Enqueuing Image job for: {imageReq.Path}...");

await queue.EnqueueAsync(
    type: "process-image",
    payload: imagePayload,
    priority: 2 // Higher priority
);

Console.WriteLine("🚀 [Producer] All jobs enqueued successfully!");

// Records
public record EmailRequest(string To, string Subject, string Body);
public record ImageProcessRequest(string Path, string Action, int Width, int Height);
