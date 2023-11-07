// Copyright (c) Microsoft. All rights reserved.

using System;
using System.Collections.Generic;

namespace Microsoft.SemanticKernel.Orchestration;

/// <summary>
/// Function result after execution.
/// </summary>
public sealed class FunctionResult
{
    // TODO: We should be able to get rid of this and just use kernel context
    internal Dictionary<string, object>? _metadata;

    /// <summary>
    /// Name of executed function.
    /// </summary>
    public string FunctionName { get; internal set; }

    /// <summary>
    /// Metadata for storing additional information about function execution result.
    /// </summary>
    public Dictionary<string, object> Metadata
    {
        get => this._metadata ??= new();
        internal set => this._metadata = value;
    }

    /// <summary>
    /// Function result object.
    /// </summary>
    internal object? Value { get; }

    /// <summary>
    /// Gets the kernel used in the invocation of the function.
    /// </summary>
    public Kernel Kernel { get; }

    /// <summary>
    /// Gets the additional context used in the invocation of the function.
    /// </summary>
    public KernelContext KernelContext { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="FunctionResult"/> class.
    /// </summary>
    /// <param name="functionName">Name of executed function.</param>
    /// <param name="kernel">The kernel.</param>
    /// <param name="context">Instance of <see cref="KernelContext"/> to pass in function pipeline.</param>
    /// <param name="result">The resulting value of the function.</param>
    public FunctionResult(string functionName, Kernel kernel, KernelContext context, object? result = null)
    {
        this.FunctionName = functionName;
        this.Kernel = kernel;
        this.KernelContext = context;
        this.Value = result;
    }

    /// <summary>
    /// Returns function result value.
    /// </summary>
    /// <typeparam name="T">Target type for result value casting.</typeparam>
    /// <exception cref="InvalidCastException">Thrown when it's not possible to cast result value to <typeparamref name="T"/>.</exception>
    public T? GetValue<T>()
    {
        if (this.Value is null)
        {
            return default;
        }

        if (this.Value is T typedResult)
        {
            return typedResult;
        }

        throw new InvalidCastException($"Cannot cast {this.Value.GetType()} to {typeof(T)}");
    }

    /// <summary>
    /// Get typed value from metadata.
    /// </summary>
    public bool TryGetMetadataValue<T>(string key, out T value)
    {
        if (this._metadata is { } metadata &&
            metadata.TryGetValue(key, out object? valueObject) &&
            valueObject is T typedValue)
        {
            value = typedValue;
            return true;
        }

        value = default!;
        return false;
    }

    /// <inheritdoc/>
    public override string ToString() => this.Value?.ToString() ?? base.ToString();
}
