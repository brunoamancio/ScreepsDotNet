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
    /// TODO (E3.2): Uncomment registrations after implementing validators.
    /// Order matters: Schema → State → Range → Permission → Resource.
    /// </remarks>
    public static IServiceCollection AddIntentValidation(this IServiceCollection services)
    {
        services.AddSingleton<IIntentValidator, Validators.RangeValidator>();

        services.AddSingleton<IIntentValidator, Validators.ResourceValidator>();

        services.AddSingleton<IIntentValidator, Validators.PermissionValidator>();

        services.AddSingleton<IIntentValidator, Validators.StateValidator>();

        services.AddSingleton<IIntentValidator, Validators.SchemaValidator>();

        // TODO (E3.3): Uncomment after implementing IntentValidationPipeline
        // services.AddSingleton<IIntentPipeline, IntentValidationPipeline>();

        return services;
    }
}
