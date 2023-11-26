// Copyright (c) Microsoft. All rights reserved.

using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Threading;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.SemanticKernel.AI;
using Microsoft.SemanticKernel.Events;
using Microsoft.SemanticKernel.Orchestration;
using Microsoft.SemanticKernel.Services;

namespace Microsoft.SemanticKernel;

/// <summary>
/// Provides state for use throughout a Semantic Kernel workload.
/// </summary>
/// <remarks>
/// An instance of <see cref="Kernel"/> is passed through to every function invocation and service call
/// throughout the system, providing to each the ability to access shared state and services.
/// </remarks>
public sealed class Kernel
{
    private Dictionary<string, object?>? _data;
    private CultureInfo _culture = CultureInfo.CurrentCulture;
    private KernelPluginCollection? _plugins;

    /// <summary>
    /// Initializes a new instance of <see cref="Kernel"/>.
    /// </summary>
    /// <param name="services">The <see cref="IServiceProvider"/> used to query for services available through the kernel.</param>
    /// <param name="plugins">
    /// The collection of plugins available through the kernel. If null, an empty collection will be used.
    /// If non-null, the supplied collection instance is used, not a copy; if it's desired for the <see cref="Kernel"/>
    /// to have a copy, the caller is responsible for supplying it.
    /// </param>
    /// <remarks>
    /// The KernelBuilder class provides a fluent API for constructing a <see cref="Kernel"/> instance.
    /// </remarks>
    public Kernel(
        IServiceProvider? services = null,
        KernelPluginCollection? plugins = null)
    {
        this.Services = services ?? EmptyServiceProvider.Instance;
        this._plugins = plugins;
    }

    /// <summary>
    /// Clone the <see cref="Kernel"/> object to create a new instance that may be mutated without affecting the current instance.
    /// </summary>
    /// <remarks>
    /// The current instance is unmodified by this operation. The new <see cref="Kernel"/> will be initialized with:
    /// <list type="bullet">
    /// <item>
    /// The same <see cref="IServiceProvider"/> reference as is returned by the current instance's <see cref="Kernel.Services"/>.
    /// </item>
    /// <item>
    /// A new <see cref="KernelPluginCollection"/> instance initialized with the same <see cref="IKernelPlugin"/> instances as are stored by the current instance's <see cref="Kernel.Plugins"/> collection.
    /// Changes to the new instance's plugin collection will not affect the current instance's plugin collection, and vice versa.
    /// </item>
    /// <item>
    /// All of the delegates registered with each event. Delegates are immutable (every time an additional delegate is added or removed, a new one is created),
    /// so changes to the new instance's event delegates will not affect the current instance's event delegates, and vice versa.
    /// </item>
    /// <item>
    /// A new <see cref="IDictionary{TKey, TValue}"/> containing all of the key/value pairs from the current instance's <see cref="Kernel.Data"/> dictionary.
    /// Any changes made to the new instance's dictionary will not affect the current instance's dictionary, and vice versa.
    /// </item>
    /// <item>The same <see cref="CultureInfo"/> reference as is returned by the current instance's <see cref="Kernel.Culture"/>.</item>
    /// </list>
    /// </remarks>
    public Kernel Clone() =>
        new(this.Services, this._plugins is { Count: > 0 } ? new KernelPluginCollection(this._plugins) : null)
        {
            FunctionInvoking = this.FunctionInvoking,
            FunctionInvoked = this.FunctionInvoked,
            PromptRendering = this.PromptRendering,
            PromptRendered = this.PromptRendered,
            _data = this._data is { Count: > 0 } ? new Dictionary<string, object?>(this._data) : null,
            _culture = this._culture,
        };

    #region Core State: Plugins and Services
    /// <summary>
    /// Gets the collection of plugins available through the kernel.
    /// </summary>
    public KernelPluginCollection Plugins =>
        this._plugins ??
        Interlocked.CompareExchange(ref this._plugins, new KernelPluginCollection(), null) ??
        this._plugins;

    /// <summary>
    /// Gets the service provider used to query for services available through the kernel.
    /// </summary>
    public IServiceProvider Services { get; }
    #endregion

    #region Additional Transient State
    /// <summary>
    /// Gets the culture currently associated with this context.
    /// </summary>
    /// <remarks>
    /// The culture defaults to <see cref="CultureInfo.CurrentCulture"/> if not explicitly set.
    /// It may be set to another culture, such as <see cref="CultureInfo.InvariantCulture"/>,
    /// and any functions invoked within the context can consult this property for use in
    /// operations like formatting and parsing.
    /// </remarks>
    [AllowNull]
    public CultureInfo Culture
    {
        get => this._culture;
        set => this._culture = value ?? CultureInfo.CurrentCulture;
    }

    /// <summary>
    /// Gets a dictionary for ambient data associated with the kernel.
    /// </summary>
    /// <remarks>
    /// This may be used to flow arbitrary data in and out of operations performed with this kernel instance.
    /// </remarks>
    public IDictionary<string, object?> Data =>
        this._data ??
        Interlocked.CompareExchange(ref this._data, new Dictionary<string, object?>(), null) ??
        this._data;

    /// <summary>
    /// Provides an event that's raised prior to a function's invocation.
    /// </summary>
    public event EventHandler<FunctionInvokingEventArgs>? FunctionInvoking;

    /// <summary>
    /// Provides an event that's raised after a function's invocation.
    /// </summary>
    public event EventHandler<FunctionInvokedEventArgs>? FunctionInvoked;

    /// <summary>
    /// Provides an event that's raised prior to a prompt being rendered.
    /// </summary>
    public event EventHandler<PromptRenderingEventArgs>? PromptRendering;

    /// <summary>
    /// Provides an event that's raised after a prompt is rendered.
    /// </summary>
    public event EventHandler<PromptRenderedEventArgs>? PromptRendered;
    #endregion

    #region Helpers on top of Plugins and Services
    /// <summary>Gets a service from the <see cref="Services"/> collection.</summary>
    /// <typeparam name="T">Specifies the type of the service to get.</typeparam>
    /// <param name="serviceKey">An object that specifies the key of the service to get.</param>
    /// <returns>The found service instance.</returns>
    /// <exception cref="KernelException">A service of the specified type and name could not be found.</exception>
    /// <remarks>
    /// This provides an opinionated view on top of <see cref="Services"/>. If a <paramref name="serviceKey"/>
    /// is provided, it will attempt to find a service registered with that key. If no <paramref name="serviceKey"/>
    /// is provided, it will attempt to find any service registered, regardless of whether it was registered with
    /// with a key. If multiple services meet the criteria, it will return the last one registered. For certain
    /// services, like <see cref="ILoggerFactory"/>, it will also return a default implementation if no key was
    /// specified and no service was found. If it's able to find the specified service, that service is returned.
    /// Otherwise, an exception is thrown.
    /// </remarks>
    public T GetService<T>(object? serviceKey = null) where T : class
    {
        T? service = null;

        if (serviceKey is not null)
        {
            if (this.Services is IKeyedServiceProvider)
            {
                // We were given a service ID, so we need to use the keyed service lookup.
                service = this.Services.GetKeyedService<T>(serviceKey);
            }
        }
        else
        {
            // No ID was given. We first want to use non-keyed lookup, in order to match against
            // a service registered without an ID. If we can't find one, then we try to match with
            // a service registered with an ID. In both cases, if there were multiple, this will match
            // with whichever was registered last.
            service = this.Services.GetService<T>();
            if (service is null && this.Services is IKeyedServiceProvider)
            {
                service = this.Services.GetKeyedService<T>(KeyedService.AnyKey);
            }

            // If no service could be found, special-case specific services to provide a default.
            if (service is null)
            {
                if (typeof(T) == typeof(ILoggerFactory) || typeof(T) == typeof(NullLoggerFactory))
                {
                    return (T)(object)NullLoggerFactory.Instance;
                }

                if (typeof(T) == typeof(IAIServiceSelector) || typeof(T) == typeof(OrderedIAIServiceSelector))
                {
                    return (T)(object)OrderedIAIServiceSelector.Instance;
                }
            }
        }

        // If we couldn't find the service, throw an exception.
        if (service is null)
        {
            throw new KernelException(serviceKey is null ?
                $"Service of type '{typeof(T)}' not registered." :
                $"Service of type '{typeof(T)}' and service key '{serviceKey}' not registered.");
        }

        // Return the found service.
        return service;
    }

    /// <summary>Gets all services of the specified type.</summary>
    /// <typeparam name="T">Specifies the type of the services to retrieve.</typeparam>
    /// <returns>An enumerable of all instances of the specified service that are registered.</returns>
    public IEnumerable<T> GetServices<T>() where T : class =>
        this.Services is IKeyedServiceProvider ?
            this.Services.GetKeyedServices<T>(KeyedService.AnyKey) :
            this.Services.GetServices<T>();
    #endregion

    #region Helpers
    internal FunctionInvokingEventArgs? OnFunctionInvoking(KernelFunction function, ContextVariables variables)
    {
        FunctionInvokingEventArgs? eventArgs = null;
        if (this.FunctionInvoking is { } functionInvoking)
        {
            eventArgs = new(function, variables);
            functionInvoking.Invoke(this, eventArgs);
        }

        return eventArgs;
    }

    internal FunctionInvokedEventArgs? OnFunctionInvoked(KernelFunction function, FunctionResult result)
    {
        FunctionInvokedEventArgs? eventArgs = null;
        if (this.FunctionInvoked is { } functionInvoked)
        {
            eventArgs = new(function, result);
            functionInvoked.Invoke(this, eventArgs);
        }

        return eventArgs;
    }

    internal PromptRenderingEventArgs? OnPromptRendering(KernelFunction function, ContextVariables variables, PromptExecutionSettings? requestSettings)
    {
        PromptRenderingEventArgs? eventArgs = null;
        if (this.PromptRendering is { } promptRendering)
        {
            eventArgs = new(function, variables, requestSettings);
            promptRendering.Invoke(this, eventArgs);
        }

        return eventArgs;
    }

    internal PromptRenderedEventArgs? OnPromptRendered(KernelFunction function, ContextVariables variables, string renderedPrompt)
    {
        PromptRenderedEventArgs? eventArgs = null;
        if (this.PromptRendered is { } promptRendered)
        {
            eventArgs = new(function, variables, renderedPrompt);
            promptRendered.Invoke(this, eventArgs);
        }

        return eventArgs;
    }

    /// <summary>Empty <see cref="IServiceProvider"/> implementation that returns null from all <see cref="IServiceProvider.GetService"/> calls.</summary>
    private sealed class EmptyServiceProvider : IServiceProvider, IKeyedServiceProvider
    {
        public static EmptyServiceProvider Instance { get; } = new();

        public object? GetService(Type serviceType) => null;

        public object? GetKeyedService(Type serviceType, object? serviceKey) => null;

        public object GetRequiredKeyedService(Type serviceType, object? serviceKey) =>
            throw new InvalidOperationException(serviceKey is null ?
                $"No service for type '{serviceType}' has been registered." :
                $"No service for type '{serviceType}' and service key '{serviceKey}' has been registered.");
    }
    #endregion
}
