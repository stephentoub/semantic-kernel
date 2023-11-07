// Copyright (c) Microsoft. All rights reserved.

using System;

namespace Microsoft.SemanticKernel.Planning;

/// <summary>
/// Interface for standard Semantic Kernel callable plan.
/// </summary>
[Obsolete("This interface is obsoleted, use IKernelFunction interface instead")]
public interface IPlan : IKernelFunction
{
}
