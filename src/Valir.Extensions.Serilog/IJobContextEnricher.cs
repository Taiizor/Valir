using Serilog.Core;
using Valir.Abstractions;

namespace Valir.Extensions.Serilog;

/// <summary>
/// Abstraction for enriching log events with job context.
/// </summary>
public interface IJobContextEnricher : ILogEventEnricher
{
    /// <summary>
    /// Sets the job context for the current async scope.
    /// </summary>
    /// <param name="context">The job context.</param>
    /// <param name="jobName">The job name/type.</param>
    /// <param name="attempt">The attempt number.</param>
    void SetContext(JobContext context, string jobName, int attempt);

    /// <summary>
    /// Clears the job context for the current async scope.
    /// </summary>
    void ClearContext();
}
