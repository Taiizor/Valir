using Serilog.Core;
using Serilog.Events;
using Valir.Abstractions;

namespace Valir.Extensions.Serilog;

/// <summary>
/// Serilog enricher that adds Valir job context properties to log events.
/// </summary>
/// <remarks>
/// Initializes a new instance of the <see cref="JobContextEnricher"/> class.
/// </remarks>
/// <param name="options">The Serilog options.</param>
public sealed class JobContextEnricher(SerilogOptions options) : ILogEventEnricher
{
    private readonly SerilogOptions _options = options ?? throw new ArgumentNullException(nameof(options));
    private readonly AsyncLocal<JobContext?> _currentContext = new();
    private readonly AsyncLocal<string?> _currentJobName = new();
    private readonly AsyncLocal<int> _currentAttempt = new();

    /// <summary>
    /// Gets or sets the current job context for the async local scope.
    /// </summary>
    public JobContext? CurrentContext
    {
        get => _currentContext.Value;
        set => _currentContext.Value = value;
    }

    /// <summary>
    /// Gets or sets the current job name for the async local scope.
    /// </summary>
    public string? CurrentJobName
    {
        get => _currentJobName.Value;
        set => _currentJobName.Value = value;
    }

    /// <summary>
    /// Gets or sets the current attempt number for the async local scope.
    /// </summary>
    public int CurrentAttempt
    {
        get => _currentAttempt.Value;
        set => _currentAttempt.Value = value;
    }

    /// <inheritdoc />
    public void Enrich(LogEvent logEvent, ILogEventPropertyFactory propertyFactory)
    {
        if (!_options.EnrichWithJobContext)
        {
            return;
        }

        JobContext? context = _currentContext.Value;
        if (context is null)
        {
            return;
        }

        logEvent.AddPropertyIfAbsent(
            propertyFactory.CreateProperty(_options.JobIdPropertyName, context.JobId));

        logEvent.AddPropertyIfAbsent(
            propertyFactory.CreateProperty(_options.WorkerIdPropertyName, context.WorkerId));

        if (!string.IsNullOrEmpty(_currentJobName.Value))
        {
            logEvent.AddPropertyIfAbsent(
                propertyFactory.CreateProperty(_options.JobNamePropertyName, _currentJobName.Value));
        }

        if (_currentAttempt.Value > 0)
        {
            logEvent.AddPropertyIfAbsent(
                propertyFactory.CreateProperty(_options.AttemptPropertyName, _currentAttempt.Value));
        }
    }

    /// <summary>
    /// Sets the job context for the current async scope.
    /// </summary>
    /// <param name="context">The job context.</param>
    /// <param name="jobName">The job name/type.</param>
    /// <param name="attempt">The attempt number.</param>
    public void SetContext(JobContext context, string jobName, int attempt)
    {
        _currentContext.Value = context;
        _currentJobName.Value = jobName;
        _currentAttempt.Value = attempt;
    }

    /// <summary>
    /// Clears the job context for the current async scope.
    /// </summary>
    public void ClearContext()
    {
        _currentContext.Value = null;
        _currentJobName.Value = null;
        _currentAttempt.Value = 0;
    }
}
