using Microsoft.Extensions.DependencyInjection;

namespace ScreepsDotNet.Engine.Validation;

/// <summary>
/// Dependency injection registration for intent validation pipeline and validators.
/// </summary>
public static class ValidationServiceCollectionExtensions
{
    /// <summary>
    /// Register intent validation services.
    /// Registers the pipeline and all 5 validators in the correct order.
    /// </summary>
    /// <remarks>
    /// Order matters: Schema → State → Range → Permission → Resource.
    /// Validators are registered in order and will be executed in that order by the pipeline.
    /// </remarks>
    public static IServiceCollection AddIntentValidation(this IServiceCollection services)
    {
        // Register validators in order (Schema → State → Range → Permission → Resource)
        services.AddSingleton<IIntentValidator, Validators.SchemaValidator>();
        services.AddSingleton<IIntentValidator, Validators.StateValidator>();
        services.AddSingleton<IIntentValidator, Validators.RangeValidator>();
        services.AddSingleton<IIntentValidator, Validators.PermissionValidator>();
        services.AddSingleton<IIntentValidator, Validators.ResourceValidator>();

        services.AddSingleton<IValidationStatisticsSink, ValidationStatisticsSink>();

        services.AddSingleton<IIntentPipeline, IntentValidationPipeline>();

        return services;
    }
}
