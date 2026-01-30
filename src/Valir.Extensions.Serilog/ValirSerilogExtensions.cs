using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Serilog;
using Valir.Abstractions;

namespace Valir.Extensions.Serilog;

/// <summary>
/// Extension methods for configuring Serilog integration with Valir.
/// </summary>
public static class ValirSerilogExtensions
{
    /// <summary>
    /// Adds Serilog logging to Valir job processing with default configuration.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddValirSerilog(this IServiceCollection services)
    {
        return AddValirSerilog(services, _ => { });
    }

    /// <summary>
    /// Adds Serilog logging to Valir job processing with custom configuration.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configureOptions">Configuration action for Serilog options.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddValirSerilog(
        this IServiceCollection services,
        Action<SerilogOptions> configureOptions)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configureOptions);

        SerilogOptions options = new();
        configureOptions(options);

        services.TryAddSingleton(options);
        services.TryAddSingleton<JobContextEnricher>();
        services.TryAddSingleton<ValirSerilogLogger>(sp =>
        {
            ILogger logger = Log.Logger;
            SerilogOptions opts = sp.GetRequiredService<SerilogOptions>();
            JobContextEnricher enricher = sp.GetRequiredService<JobContextEnricher>();
            return new ValirSerilogLogger(logger, opts, enricher);
        });

        return services;
    }

    /// <summary>
    /// Adds Serilog logging to Valir job processing with a custom logger.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="logger">The Serilog logger instance.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddValirSerilog(
        this IServiceCollection services,
        ILogger logger)
    {
        return AddValirSerilog(services, logger, _ => { });
    }

    /// <summary>
    /// Adds Serilog logging to Valir job processing with a custom logger and configuration.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="logger">The Serilog logger instance.</param>
    /// <param name="configureOptions">Configuration action for Serilog options.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddValirSerilog(
        this IServiceCollection services,
        ILogger logger,
        Action<SerilogOptions> configureOptions)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(logger);
        ArgumentNullException.ThrowIfNull(configureOptions);

        SerilogOptions options = new();
        configureOptions(options);

        services.TryAddSingleton(options);
        services.TryAddSingleton<JobContextEnricher>();
        services.TryAddSingleton<ValirSerilogLogger>(sp =>
        {
            SerilogOptions opts = sp.GetRequiredService<SerilogOptions>();
            JobContextEnricher enricher = sp.GetRequiredService<JobContextEnricher>();
            return new ValirSerilogLogger(logger, opts, enricher);
        });

        return services;
    }

    /// <summary>
    /// Decorates all registered job handlers with Serilog logging.
    /// Call this after registering all job handlers.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection UseSerilogForJobs(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        // Get all registered job handler types
        var handlerTypes = services
            .Where(sd => sd.ServiceType.IsGenericType &&
                         sd.ServiceType.GetGenericTypeDefinition() == typeof(IJobHandler<>))
            .Select(sd => new
            {
                sd.ServiceType,
                sd.ImplementationType,
                sd.Lifetime
            })
            .ToList();

        foreach (var handler in handlerTypes)
        {
            // Remove the original registration
            ServiceDescriptor? descriptor = services.FirstOrDefault(sd =>
                sd.ServiceType == handler.ServiceType &&
                sd.ImplementationType == handler.ImplementationType);

            if (descriptor is not null)
            {
                services.Remove(descriptor);
            }

            // Register the decorator
            services.Add(new ServiceDescriptor(
                handler.ServiceType,
                sp =>
                {
                    Type jobType = handler.ServiceType.GetGenericArguments()[0];
                    Type decoratorType = typeof(LoggingJobHandlerDecorator<>).MakeGenericType(jobType);
                    Type innerType = handler.ImplementationType!;

                    object inner = ActivatorUtilities.CreateInstance(sp, innerType);
                    ValirSerilogLogger logger = sp.GetRequiredService<ValirSerilogLogger>();
                    SerilogOptions options = sp.GetRequiredService<SerilogOptions>();

                    return ActivatorUtilities.CreateInstance(sp, decoratorType, inner, logger, options);
                },
                handler.Lifetime));
        }

        return services;
    }

    /// <summary>
    /// Delegate for Serilog configuration with job context.
    /// </summary>
    /// <remarks>
    /// Initializes a new instance of the <see cref="SerilogConfigurationDelegate"/> class.
    /// </remarks>
    /// <param name="configure">The configuration action.</param>
    public sealed class SerilogConfigurationDelegate(Action<JobContext, ILogger> configure)
    {
        /// <summary>
        /// Gets the configuration action.
        /// </summary>
        public Action<JobContext, ILogger> Configure { get; } = configure;
    }
}
