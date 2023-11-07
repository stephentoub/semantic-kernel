// Copyright (c) Microsoft. All rights reserved.

using System;
using System.Threading;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.SemanticKernel.Diagnostics;
using Microsoft.SemanticKernel.Events;
using Microsoft.SemanticKernel.Functions;
using Microsoft.SemanticKernel.Http;
using Microsoft.SemanticKernel.Services;

namespace Microsoft.SemanticKernel;

/// <summary>
/// Provides a lightweight container for the functions and services related
/// to the invocation of functions.
/// </summary>
public class Kernel
{
    private IAIServiceSelector? _aIServiceSelector;
    private PluginCollection? _plugins;

    /// <summary>
    /// Creates an instance of <see cref="Kernel"/>.
    /// </summary>
    /// <param name="plugins">Plug-in collection. If null, an empty collection will be used.</param>
    /// <param name="aiServiceProvider">AI Service Provider. If null, no services will be available.</param>
    /// <param name="aiServiceSelector">AI Service Selector. If null, a default selector will be used.</param>
    /// <param name="httpHandlerFactory">HTTP handler factory. If null, a default factory will be used.</param>
    /// <param name="loggerFactory">The <see cref="ILoggerFactory"/> to use for logging. If null, no logging will be performed.</param>
    public Kernel(
        PluginCollection? plugins,
        IAIServiceProvider? aiServiceProvider,
        IAIServiceSelector? aiServiceSelector,
        IDelegatingHandlerFactory? httpHandlerFactory,
        ILoggerFactory? loggerFactory)
    {
        this._plugins = plugins;
        this.ServiceProvider = aiServiceProvider ?? NullServiceProvider.Instance;
        this._aIServiceSelector = aiServiceSelector;
        this.HttpHandlerFactory = httpHandlerFactory ?? NullHttpHandlerFactory.Instance;
        this.LoggerFactory = loggerFactory ?? NullLoggerFactory.Instance;
    }

    /// <summary>Gets the ILoggerFactory used to create a logger for logging.</summary>
    public ILoggerFactory LoggerFactory { get; }

    /// <summary>Gets the function collection containing all functions imported into the kernel.</summary>
    public PluginCollection Plugins =>
        this._plugins ??
        Interlocked.CompareExchange(ref this._plugins, new PluginCollection(), null) ??
        this._plugins;

    /// <summary>Gets the HTTP handler factory to used for networking operations.</summary>
    public IDelegatingHandlerFactory HttpHandlerFactory { get; }

    /// <summary>Gets or sets a delegate that's invoked before each function invocation.</summary>
    public EventHandler<FunctionInvokingEventArgs>? FunctionInvoking { get; set; }

    /// <summary>Gets or sets a delegate that's invoked after each function invocation.</summary>
    public EventHandler<FunctionInvokedEventArgs>? FunctionInvoked { get; set; }

    /// <summary>Gets a configured AI service.</summary>
    /// <typeparam name="T">The type of the service to retrieve.</typeparam>
    /// <param name="name">Optional name. If the name is not provided, returns the default service available for the specified type.</param>
    /// <returns>The registered service.</returns>
    /// <exception cref="KernelException">The requested service could not be found.</exception>
    public T GetService<T>(string? name = null) where T : class, IAIService =>
        this.ServiceProvider.GetService<T>(name) ??
        throw new KernelException($"Service of type {typeof(T)} {(name is not null ? $" and name {name} " : "")}not registered.");

    /// <summary>Gets the service selector to use.</summary>
    public IAIServiceSelector ServiceSelector =>
        this._aIServiceSelector ??
        Interlocked.CompareExchange(ref this._aIServiceSelector, new OrderedIAIServiceSelector(), null) ??
        this._aIServiceSelector;

    /// <summary>Gets the service provider to use.</summary>
    public IAIServiceProvider ServiceProvider { get; }

    private sealed class NullServiceProvider : IAIServiceProvider
    {
        internal static NullServiceProvider Instance { get; } = new();

        public T? GetService<T>(string? name = null) where T : IAIService =>
            throw new KernelException(); // TODO: Fill in message
    }

    /// <summary>Gets the name of the argument that's used as the default argument name.</summary>
    public static string InputArgumentName { get; } = "input";
}
