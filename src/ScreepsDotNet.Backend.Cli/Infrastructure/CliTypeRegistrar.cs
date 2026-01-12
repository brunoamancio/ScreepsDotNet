namespace ScreepsDotNet.Backend.Cli.Infrastructure;

using System;
using System.Collections.Generic;
using Microsoft.Extensions.DependencyInjection;
using Spectre.Console.Cli;

internal sealed class CliTypeRegistrar(IServiceProvider serviceProvider) : ITypeRegistrar
{
    private readonly Dictionary<Type, Type> _registrations = [];
    private readonly Dictionary<Type, object> _instances = [];
    private readonly Dictionary<Type, Func<object>> _lazyRegistrations = [];

    public ITypeResolver Build()
        => new CliTypeResolver(serviceProvider, _registrations, _instances, _lazyRegistrations);

    public void Register(Type service, Type implementation)
    {
        ArgumentNullException.ThrowIfNull(service);
        ArgumentNullException.ThrowIfNull(implementation);

        _registrations[service] = implementation;
    }

    public void RegisterInstance(Type service, object implementation)
    {
        ArgumentNullException.ThrowIfNull(service);
        ArgumentNullException.ThrowIfNull(implementation);

        _instances[service] = implementation;
    }

    public void RegisterLazy(Type service, Func<object> factory)
    {
        ArgumentNullException.ThrowIfNull(service);
        ArgumentNullException.ThrowIfNull(factory);

        _lazyRegistrations[service] = factory;
    }

    private sealed class CliTypeResolver(
        IServiceProvider provider,
        IReadOnlyDictionary<Type, Type> registrations,
        IReadOnlyDictionary<Type, object> instances,
        IReadOnlyDictionary<Type, Func<object>> lazyRegistrations) : ITypeResolver, IDisposable
    {
        public object? Resolve(Type? type)
        {
            if (type is null)
                return null;

            if (instances.TryGetValue(type, out var instance))
                return instance;

            if (lazyRegistrations.TryGetValue(type, out var factory))
                return factory();

            if (registrations.TryGetValue(type, out var implementation))
                return ActivatorUtilities.CreateInstance(provider, implementation);

            return provider.GetService(type) ?? ActivatorUtilities.GetServiceOrCreateInstance(provider, type);
        }

        public void Dispose()
        {
            if (provider is IDisposable disposable)
                disposable.Dispose();
        }
    }
}
