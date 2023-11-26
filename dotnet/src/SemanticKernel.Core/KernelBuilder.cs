// Copyright (c) Microsoft. All rights reserved.

using System;
using System.Globalization;
using System.Net.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel.Services;

namespace Microsoft.SemanticKernel;

/// <summary>
/// A builder for Semantic Kernel.
/// </summary>
public sealed class KernelBuilder
{
    private ServiceCollection? _services;
    private CultureInfo? _culture;

    /// <summary>
    /// Create a new kernel instance
    /// </summary>
    /// <returns>New kernel instance</returns>
    public static Kernel Create()
    {
        var builder = new KernelBuilder();
        return builder.Build();
    }

    /// <summary>
    /// Build a new kernel instance using the settings passed so far.
    /// </summary>
    /// <returns>Kernel instance</returns>
    public Kernel Build()
    {
        var instance = new Kernel(this._services?.BuildServiceProvider());

        if (this._culture != null)
        {
            instance.Culture = this._culture;
        }

        return instance;
    }

    /// <summary>
    /// Add a logger to the kernel to be built.
    /// </summary>
    /// <param name="loggerFactory">The <see cref="ILoggerFactory"/> to use for logging. If null, no logging will be performed.</param>
    /// <returns>Updated kernel builder including the logger.</returns>
    public KernelBuilder WithLoggerFactory(ILoggerFactory loggerFactory)
    {
        Verify.NotNull(loggerFactory);
        (this._services ??= new()).AddSingleton(loggerFactory);
        return this;
    }

    /// <summary>
    /// Add an <see cref="HttpClient"/> to the kernel to be built.
    /// </summary>
    /// <param name="httpClient"><see cref="HttpClient"/> to add.</param>
    /// <returns>Updated kernel builder including the client.</returns>
    public KernelBuilder WithHttpClient(HttpClient httpClient)
    {
        Verify.NotNull(httpClient);
        (this._services ??= new()).AddSingleton(httpClient);
        return this;
    }

    /// <summary>
    /// Adds a <typeparamref name="TService"/> instance to the services collection
    /// </summary>
    /// <param name="instance">The <typeparamref name="TService"/> instance.</param>
    public KernelBuilder WithDefaultAIService<TService>(TService instance) where TService : class, IAIService
    {
        (this._services ??= new()).AddSingleton(instance);
        return this;
    }

    /// <summary>
    /// Adds a <typeparamref name="TService"/> factory method to the services collection
    /// </summary>
    /// <param name="factory">The factory method that creates the AI service instances of type <typeparamref name="TService"/>.</param>
    public KernelBuilder WithDefaultAIService<TService>(Func<IServiceProvider, TService> factory) where TService : class, IAIService
    {
        (this._services ??= new()).AddSingleton<TService>(factory);
        return this;
    }

    /// <summary>
    /// Adds a <typeparamref name="TService"/> instance to the services collection
    /// </summary>
    /// <param name="serviceId">The service ID</param>
    /// <param name="instance">The <typeparamref name="TService"/> instance.</param>
    public KernelBuilder WithAIService<TService>(
        string? serviceId,
        TService instance) where TService : class, IAIService
    {
        this._services ??= new();
        if (serviceId is not null)
        {
            this._services.AddKeyedSingleton(serviceId, instance);
        }
        else
        {
            this._services.AddSingleton(instance);
        }
        return this;
    }

    /// <summary>
    /// Adds a <typeparamref name="TService"/> factory method to the services collection
    /// </summary>
    /// <param name="serviceId">The service ID</param>
    /// <param name="factory">The factory method that creates the AI service instances of type <typeparamref name="TService"/>.</param>
    public KernelBuilder WithAIService<TService>(
        string? serviceId,
        Func<IServiceProvider, TService> factory) where TService : class, IAIService
    {
        this._services ??= new();
        if (serviceId is not null)
        {
            this._services.AddKeyedSingleton<TService>(serviceId, (serviceProvider, _) => factory(serviceProvider));
        }
        else
        {
            this._services.AddSingleton<TService>(factory);
        }
        return this;
    }

    /// <summary>
    /// Adds a <cref name="IAIServiceSelector"/> to the builder
    /// </summary>
    public KernelBuilder WithAIServiceSelector(IAIServiceSelector serviceSelector)
    {
        (this._services ??= new()).AddSingleton(serviceSelector);
        return this;
    }

    /// <summary>
    /// Sets a culture to be used by the kernel.
    /// </summary>
    /// <param name="culture">The culture.</param>
    public KernelBuilder WithCulture(CultureInfo culture)
    {
        this._culture = culture;
        return this;
    }
}
