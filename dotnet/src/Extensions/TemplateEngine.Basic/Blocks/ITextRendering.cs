// Copyright (c) Microsoft. All rights reserved.

using System.Collections.Generic;
using Microsoft.SemanticKernel.Orchestration;

namespace Microsoft.SemanticKernel.TemplateEngine.Basic.Blocks;

/// <summary>
/// Interface of static blocks that don't need async IO to be rendered.
/// </summary>
public interface ITextRendering
{
    /// <summary>
    /// Render the block using only the given variables.
    /// </summary>
    /// <param name="variables">Optional variables used to render the block</param>
    /// <returns>Rendered content</returns>
    public string Render(IReadOnlyDictionary<string, object?>? variables);
}
