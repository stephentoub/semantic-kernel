// Copyright (c) Microsoft. All rights reserved.

using Microsoft.SemanticKernel.Orchestration;

namespace Microsoft.SemanticKernel.Events;

/// <summary>
/// Event arguments available to the Kernel.FunctionInvoking event.
/// </summary>
public class FunctionInvokingEventArgs : KernelCancelEventArgs
{
    /// <summary>
    /// Indicates if the function execution should be skipped.
    /// </summary>
    public bool IsSkipRequested => this._skipRequested;

    /// <summary>
    /// Initializes a new instance of the <see cref="FunctionInvokingEventArgs"/> class.
    /// </summary>
    /// <param name="functionView">Function view details</param>
    /// <param name="kernel">The kernel.</param>
    /// <param name="context">Context related to the event</param>
    public FunctionInvokingEventArgs(FunctionView functionView, Kernel kernel, KernelContext context) : base(functionView, kernel, context)
    {
    }

    /// <summary>
    /// Skip the current function invoking attempt.
    /// </summary>
    public void Skip()
    {
        this._skipRequested = true;
    }

    private bool _skipRequested;
}
