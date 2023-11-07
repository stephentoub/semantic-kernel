// Copyright (c) Microsoft. All rights reserved.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.SemanticKernel.Diagnostics;
using Microsoft.SemanticKernel.Events;
using Microsoft.SemanticKernel.Orchestration;

namespace Microsoft.SemanticKernel;

/// <summary>Extension methods for interacting with <see cref="Kernel"/>.</summary>
public static class KernelExtensions
{
    /// <summary>
    /// Import a set of functions as a plugin from the given object instance. Only the functions that have the `SKFunction` attribute will be included in the plugin.
    /// Once these functions are imported, the prompt templates can use functions to import content at runtime.
    /// </summary>
    /// <param name="kernel">The kernel.</param>
    /// <param name="plugin">Instance of a class containing functions</param>
    /// <param name="pluginName">Name of the plugin for function collection and prompt templates. If the value is empty functions are registered in the global namespace.</param>
    /// <returns>A list of all the semantic functions found in the directory, indexed by function name.</returns>
    public static IDictionary<string, IKernelFunction> ImportPlugin(
        this Kernel kernel,
        object plugin,
        string? pluginName = null)
    {
        return ImportPlugin(kernel.Plugins, plugin, pluginName, kernel.LoggerFactory);
    }

    /// <summary>
    /// Import a set of functions as a plugin from the given object instance. Only the functions that have the `SKFunction` attribute will be included in the plugin.
    /// Once these functions are imported, the prompt templates can use functions to import content at runtime.
    /// </summary>
    /// <param name="plugins">The plugins collection into which functions should be imported.</param>
    /// <param name="plugin">Instance of a class containing functions</param>
    /// <param name="pluginName">Name of the plugin for function collection and prompt templates. If the value is empty, a name is derived from the type of the plugin.</param>
    /// <param name="loggerFactory">The <see cref="ILoggerFactory"/> to use for logging. If null, no logging will be performed.</param>
    /// <returns>A list of all the semantic functions found in the directory, indexed by function name.</returns>
    public static IDictionary<string, IKernelFunction> ImportPlugin(
        this PluginCollection plugins,
        object plugin,
        string? pluginName = null,
        ILoggerFactory? loggerFactory = null)
    {
        Verify.NotNull(plugins);
        Verify.NotNull(plugin);

        ILogger logger = (loggerFactory ?? NullLoggerFactory.Instance).CreateLogger(plugins.GetType());

        // Use the type name as the plugin name if no plugin name was provided
        if (string.IsNullOrWhiteSpace(pluginName))
        {
            pluginName = plugin.GetType().Name;
        }
        logger.LogTrace("Importing functions from {0} to the {1} plugin", plugin.GetType().FullName, pluginName);

        // Get all of the methods to try importing
        MethodInfo[] methods = plugin.GetType().GetMethods(BindingFlags.Static | BindingFlags.Instance | BindingFlags.Public);
        logger.LogTrace("Importing plugin name: {0}. Potential methods found: {1}", pluginName, methods.Length);

        // Filter out non-SKFunctions and fail if two functions have the same name
        Dictionary<string, IKernelFunction> result = new(StringComparer.OrdinalIgnoreCase);
        foreach (MethodInfo method in methods)
        {
            if (method.GetCustomAttribute<SKFunctionAttribute>() is not null)
            {
                IKernelFunction function = KernelFunction.Create(method, plugin, loggerFactory: loggerFactory);
                if (result.ContainsKey(function.Name))
                {
                    throw new KernelException("Function overloads are not supported. Differentiate function names.");
                }

                result.Add(function.Name, function);
            }
        }

        logger.LogTrace("Methods imported {0}", result.Count);

        // If the plugin doesn't exist, add it. Otherwise, add the functions to the existing plugin.
        // Either way, add a copy that's distinct from the dictionary returned from this function, so
        // that the return of this function is its own isolated view of what was imported as part of
        // this call.
        if (!plugins.TryGetPlugin(pluginName!, out IDictionary<string, IKernelFunction>? existingPlugin))
        {
            plugins.Add(pluginName!, new Dictionary<string, IKernelFunction>(result));
        }
        else
        {
            foreach (KeyValuePair<string, IKernelFunction> f in result)
            {
                if (existingPlugin.ContainsKey(f.Key))
                {
                    throw new KernelException($"Function {f.Key} already exists in plugin {pluginName}");
                }

                existingPlugin.Add(f.Key, f.Value);
            }
        }

        return result;
    }

    // /////////////////////////
    // TODO: Review which such methods we actually want
    // /////////////////////////

    /// <summary>
    /// Run a single synchronous or asynchronous <see cref="IKernelFunction"/>.
    /// </summary>
    /// <param name="kernel">The kernel.</param>
    /// <param name="function">A Semantic Kernel function to run</param>
    /// <param name="arguments">Input to process</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> to monitor for cancellation requests. The default is <see cref="CancellationToken.None"/>.</param>
    /// <returns>Result of the function</returns>
    public static Task<KernelResult> RunAsync(
        this Kernel kernel,
        IKernelFunction function,
        IReadOnlyDictionary<string, object?>? arguments = null,
        CancellationToken cancellationToken = default)
    {
        Verify.NotNull(kernel);
        return RunAsync(kernel, arguments, cancellationToken, function);
    }

    /// <summary>
    /// Run a pipeline composed of synchronous and asynchronous functions.
    /// </summary>
    /// <param name="kernel">The kernel.</param>
    /// <param name="pipeline">List of functions</param>
    /// <returns>Result of the function composition</returns>
    public static Task<KernelResult> RunAsync(
        this Kernel kernel,
        params IKernelFunction[] pipeline)
    {
        Verify.NotNull(kernel);
        return RunAsync(kernel, (IReadOnlyDictionary<string, object?>?)null, CancellationToken.None, pipeline);
    }

    /// <summary>
    /// Run a pipeline composed of synchronous and asynchronous functions.
    /// </summary>
    /// <param name="kernel">The kernel.</param>
    /// <param name="argument">Initial argument.</param>
    /// <param name="pipeline">List of functions</param>
    /// <returns>Result of the function composition</returns>
    public static Task<KernelResult> RunAsync(
        this Kernel kernel,
        string argument,
        params IKernelFunction[] pipeline)
    {
        Verify.NotNull(kernel);
        return RunAsync(kernel, new Dictionary<string, object?> { { "Input", argument } }, CancellationToken.None, pipeline);
    }

    /// <summary>
    /// Run a pipeline composed of synchronous and asynchronous functions.
    /// </summary>
    /// <param name="kernel">The kernel.</param>
    /// <param name="arguments">Input to process</param>
    /// <param name="pipeline">List of functions</param>
    /// <returns>Result of the function composition</returns>
    public static Task<KernelResult> RunAsync(
        this Kernel kernel,
        IReadOnlyDictionary<string, object?> arguments,
        params IKernelFunction[] pipeline)
    {
        Verify.NotNull(kernel);
        return RunAsync(kernel, arguments, CancellationToken.None, pipeline);
    }

    /// <summary>
    /// Run a pipeline composed of synchronous and asynchronous functions.
    /// </summary>
    /// <param name="kernel">The kernel.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> to monitor for cancellation requests. The default is <see cref="CancellationToken.None"/>.</param>
    /// <param name="pipeline">List of functions</param>
    /// <returns>Result of the function composition</returns>
    public static Task<KernelResult> RunAsync(
        this Kernel kernel,
        CancellationToken cancellationToken,
        params IKernelFunction[] pipeline)
    {
        Verify.NotNull(kernel);
        return RunAsync(kernel, (IReadOnlyDictionary<string, object?>?)null, cancellationToken, pipeline);
    }

    /// <summary>
    /// Run a pipeline composed of synchronous and asynchronous functions.
    /// </summary>
    /// <param name="kernel">The kernel.</param>
    /// <param name="argument">Initial argument to the functions.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> to monitor for cancellation requests. The default is <see cref="CancellationToken.None"/>.</param>
    /// <param name="pipeline">List of functions</param>
    /// <returns>Result of the function composition</returns>
    public static Task<KernelResult> RunAsync(
        this Kernel kernel,
        string argument,
        CancellationToken cancellationToken,
        params IKernelFunction[] pipeline)
    {
        Verify.NotNull(kernel);
        return RunAsync(kernel, new Dictionary<string, object?> { { "Input", argument } }, cancellationToken, pipeline);
    }

    /// <summary>
    /// Run a pipeline composed of synchronous and asynchronous functions.
    /// </summary>
    /// <param name="kernel">The kernel.</param>
    /// <param name="arguments">Input to process</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> to monitor for cancellation requests. The default is <see cref="CancellationToken.None"/>.</param>
    /// <param name="functions">List of functions</param>
    /// <returns>Result of the function composition</returns>
    public static async Task<KernelResult> RunAsync(
        this Kernel kernel,
        IReadOnlyDictionary<string, object?>? arguments,
        CancellationToken cancellationToken,
        params IKernelFunction[] functions)
    {
        Verify.NotNull(kernel);
        Verify.NotNull(functions);

        var context = new KernelContext();

        FunctionResult? functionResult = null;
        ILogger? logger = null;

        int i = 0;
        var allFunctionResults = new FunctionResult[functions.Length];

        Dictionary<string, object?> mutableArgs = new(StringComparer.OrdinalIgnoreCase);
        if (arguments is not null)
        {
            foreach (KeyValuePair<string, object?> arg in arguments)
            {
                mutableArgs[arg.Key] = arg.Value;
            }
        }

        foreach (IKernelFunction function in functions)
        {
repeat:
            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                FunctionView functionDetails = function.Describe();

                if (kernel.FunctionInvoking is EventHandler<FunctionInvokingEventArgs> preHandler)
                {
                    var args = new FunctionInvokingEventArgs(functionDetails, kernel, context);
                    preHandler(kernel, args);

                    if (args.CancelToken.IsCancellationRequested)
                    {
                        logger ??= kernel.LoggerFactory.CreateLogger(kernel.GetType());
                        logger.LogInformation("Execution was cancelled on function invoking event of pipeline step {StepCount}: {FunctionName}.", i, function.Name);
                        break;
                    }

                    if (args.IsSkipRequested)
                    {
                        logger ??= kernel.LoggerFactory.CreateLogger(kernel.GetType());
                        logger.LogInformation("Execution was skipped on function invoking event of pipeline step {StepCount}: {FunctionName}.", i, function.Name);
                        continue;
                    }
                }

                functionResult = await function.InvokeAsync(kernel, mutableArgs, context, cancellationToken: cancellationToken).ConfigureAwait(false);
                mutableArgs["Input"] = functionResult.Value;

                FunctionInvokedEventArgs? invokedArgs = null;
                if (kernel.FunctionInvoked is EventHandler<FunctionInvokedEventArgs> postHandler)
                {
                    invokedArgs = new FunctionInvokedEventArgs(functionDetails, functionResult);
                    postHandler(kernel, invokedArgs);
                    functionResult = new FunctionResult(functionResult.FunctionName, kernel, invokedArgs.KernelContext, functionResult.Value);
                }

                allFunctionResults[i] = functionResult;

                if (invokedArgs is not null)
                {
                    if (invokedArgs.CancelToken.IsCancellationRequested)
                    {
                        logger ??= kernel.LoggerFactory.CreateLogger(kernel.GetType());
                        logger.LogInformation("Execution was cancelled on function invoked event of pipeline step {StepCount}: {FunctionName}.", i, function.Name);
                        break;
                    }

                    if (invokedArgs.IsRepeatRequested)
                    {
                        logger ??= kernel.LoggerFactory.CreateLogger(kernel.GetType());
                        logger.LogInformation("Execution repeat request on function invoked event of pipeline step {StepCount}: {FunctionName}.", i, function.Name);
                        goto repeat;
                    }
                }
            }
            catch (Exception ex)
            {
                logger ??= kernel.LoggerFactory.CreateLogger(kernel.GetType());
                logger.LogError("Function {Function} call fail during pipeline step {Step} with error {Error}:", function.Name, i, ex.Message);
                throw;
            }

            i++;
        }

        return new KernelResult(functionResult?.Value, allFunctionResults);
    }
}
