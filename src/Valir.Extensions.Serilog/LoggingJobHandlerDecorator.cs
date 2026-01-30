using System.Diagnostics;
using Valir.Abstractions;

namespace Valir.Extensions.Serilog;

/// <summary>
/// Decorator that adds Serilog logging to job handlers.
/// </summary>
/// <typeparam name="TJob">The job type.</typeparam>
/// <remarks>
/// Initializes a new instance of the <see cref="LoggingJobHandlerDecorator{TJob}"/> class.
/// </remarks>
/// <param name="inner">The inner job handler.</param>
/// <param name="logger">The Valir Serilog logger.</param>
/// <param name="options">The Serilog options.</param>
public sealed class LoggingJobHandlerDecorator<TJob>(
    IJobHandler<TJob> inner,
    ValirSerilogLogger logger,
    SerilogOptions options) : IJobHandler<TJob> where TJob : notnull
{
    private readonly IJobHandler<TJob> _inner = inner ?? throw new ArgumentNullException(nameof(inner));
    private readonly ValirSerilogLogger _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    private readonly SerilogOptions _options = options ?? throw new ArgumentNullException(nameof(options));

    /// <inheritdoc />
    public async Task HandleAsync(TJob job, JobContext context)
    {
        ArgumentNullException.ThrowIfNull(job);
        ArgumentNullException.ThrowIfNull(context);

        string jobName = typeof(TJob).FullName ?? typeof(TJob).Name;
        int attempt = GetAttemptFromContext(context);
        Stopwatch? stopwatch = _options.IncludeTiming ? Stopwatch.StartNew() : null;

        try
        {
            _logger.LogJobStart(jobName, context, attempt);
            _logger.LogJobPayload(jobName, context, job);

            await _inner.HandleAsync(job, context);

            _logger.LogJobComplete(jobName, context, attempt, stopwatch ?? Stopwatch.StartNew());
        }
        catch (Exception ex)
        {
            _logger.LogJobFailure(jobName, context, attempt, ex, stopwatch);
            throw;
        }
    }

    private static int GetAttemptFromContext(JobContext context)
    {
        // Attempt is tracked via the JobContext enrichment if available
        // Default to 1 if not explicitly set
        return 1;
    }
}
