// Copyright (c) Microsoft. All rights reserved.

using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.SemanticKernel.Orchestration;

/// <summary>
/// Function runner implementation.
/// </summary>
internal class FunctionRunner : IFunctionRunner
{
    private readonly Kernel _kernel;

    /// <summary>
    /// Initializes a new instance of the <see cref="FunctionRunner"/> class.
    /// </summary>
    /// <param name="kernel">The kernel instance.</param>
    public FunctionRunner(Kernel kernel)
    {
        this._kernel = kernel;
    }

    /// <inheritdoc/>
    public async Task<FunctionResult> RunAsync(IKernelFunction skFunction, ContextVariables? variables = null, CancellationToken cancellationToken = default)
    {
        return (await this._kernel.RunAsync(skFunction, variables, cancellationToken).ConfigureAwait(false))
            .FunctionResults.First();
    }

    /// <inheritdoc/>
    public Task<FunctionResult> RunAsync(string pluginName, string functionName, ContextVariables? variables = null, CancellationToken cancellationToken = default)
    {
        var function = this._kernel.Functions.GetFunction(pluginName, functionName);
        return this.RunAsync(function, variables, cancellationToken);
    }
}
