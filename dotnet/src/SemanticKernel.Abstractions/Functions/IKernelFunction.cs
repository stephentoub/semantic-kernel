// Copyright (c) Microsoft. All rights reserved.

using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.SemanticKernel.Orchestration;

#pragma warning disable IDE0130

namespace Microsoft.SemanticKernel;

/// <summary>
/// Semantic Kernel callable function interface
/// </summary>
public interface IKernelFunction
{
    /// <summary>
    /// Gets the name of the function.
    /// </summary>
    /// <remarks>
    /// The name is used by the function collection and in prompt templates.
    /// </remarks>
    string Name { get; }

    /// <summary>
    /// Gets a description of the function.
    /// </summary>
    /// <remarks>
    /// The description is used in combination with embeddings when searching relevant functions.
    /// </remarks>
    string Description { get; }

    /// <summary>
    /// Creates a <see cref="FunctionView"/> that describes a function, including its parameters.
    /// </summary>
    /// <returns>An instance of <see cref="FunctionView"/> describing the function.</returns>
    FunctionView Describe();

    /// <summary>
    /// Invoke the <see cref="IKernelFunction"/>.
    /// </summary>
    /// <param name="kernel">The kernel used in the invocation of the function.</param>
    /// <param name="arguments">Arguments for the function invocation.</param>
    /// <param name="context">Additional optional context to be provided to the function invocation.</param>
    /// <returns>The function result and additional contextual information.</returns>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> to monitor for cancellation requests. The default is <see cref="CancellationToken.None"/>.</param>
    Task<FunctionResult> InvokeAsync(
        Kernel kernel,
        IReadOnlyDictionary<string, object?>? arguments = null,
        KernelContext? context = null,
        CancellationToken cancellationToken = default);
}
