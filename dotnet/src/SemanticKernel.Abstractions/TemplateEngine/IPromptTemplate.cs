// Copyright (c) Microsoft. All rights reserved.

using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.SemanticKernel.Orchestration;

namespace Microsoft.SemanticKernel.TemplateEngine;

/// <summary>
/// Interface for prompt template.
/// </summary>
public interface IPromptTemplate
{
    /// <summary>
    /// The list of parameters required by the template, using configuration and template info.
    /// </summary>
    IReadOnlyList<ParameterView> Parameters { get; }

    /// <summary>
    /// Render the template using the information in the context
    /// </summary>
    /// <param name="kernel">The kernel for the invocation.</param>
    /// <param name="arguments">Arguments providing values that can be used in the template.</param>
    /// <param name="context">Kernel execution context</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> to monitor for cancellation requests. The default is <see cref="CancellationToken.None"/>.</param>
    /// <returns>Prompt rendered to string</returns>
    public Task<string> RenderAsync(Kernel kernel, IReadOnlyDictionary<string, object?> arguments, KernelContext context, CancellationToken cancellationToken = default);
}
