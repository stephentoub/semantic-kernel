// Copyright (c) Microsoft. All rights reserved.

using System;
using System.Collections.Generic;

namespace Microsoft.SemanticKernel.Orchestration;

/// <summary>
/// Kernel result after execution.
/// </summary>
public sealed class KernelResult
{
    private readonly object? _value;

    /// <summary>
    /// Creates instance of <see cref="KernelResult"/> based on function results.
    /// </summary>
    /// <param name="value">Kernel result object.</param>
    /// <param name="functionResults">Results from all functions in pipeline.</param>
    public KernelResult(object? value, IReadOnlyCollection<FunctionResult> functionResults)
    {
        this._value = value;
        this.FunctionResults = functionResults;
    }

    /// <summary>
    /// Results from all functions in pipeline.
    /// </summary>
    public IReadOnlyCollection<FunctionResult> FunctionResults { get; }

    /// <summary>
    /// Returns kernel result value.
    /// </summary>
    /// <typeparam name="T">Target type for result value casting.</typeparam>
    /// <exception cref="InvalidCastException">Thrown when it's not possible to cast result value to <typeparamref name="T"/>.</exception>
    public T? GetValue<T>()
    {
        if (this._value is null)
        {
            return default;
        }

        if (this._value is T typedResult)
        {
            return typedResult;
        }

        throw new InvalidCastException($"Cannot cast {this._value.GetType()} to {typeof(T)}");
    }

    /// <inheritdoc/>
    public override string ToString() => this._value?.ToString() ?? base.ToString();
}
