// Copyright (c) Microsoft. All rights reserved.

using System;
using Microsoft.SemanticKernel.Diagnostics;
using Microsoft.SemanticKernel.Orchestration;

namespace Microsoft.SemanticKernel.Events;

/// <summary>
/// Base arguments for events.
/// </summary>
public abstract class KernelEventArgs : EventArgs
{
    /// <summary>
    /// Initializes a new instance of the <see cref="KernelEventArgs"/> class.
    /// </summary>
    /// <param name="functionView">Function view details</param>
    /// <param name="context">Context related to the event</param>
    internal KernelEventArgs(FunctionView functionView, KernelContext context)
    {
        Verify.NotNull(context);
        Verify.NotNull(functionView);

        this.FunctionView = functionView;
        this.KernelContext = context;
    }

    /// <summary>
    /// Function view details.
    /// </summary>
    public FunctionView FunctionView { get; }

    /// <summary>Kernel associated with the event.</summary>
    public Kernel Kernel { get; }

    /// <summary>
    /// Context related to the event.
    /// </summary>
    public KernelContext KernelContext { get; }
}
