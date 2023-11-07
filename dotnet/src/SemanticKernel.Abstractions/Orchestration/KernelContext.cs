// Copyright (c) Microsoft. All rights reserved.

using System.Collections.Generic;
using System.Globalization;
using Microsoft.SemanticKernel.AI;

namespace Microsoft.SemanticKernel.Orchestration;

/// <summary>Provides additional context passed to function and service invocations.</summary>
public sealed class KernelContext
{
    private CultureInfo? _culture;
    private IDictionary<string, object?>? _data;

    /// <summary>Gets or sets the culture associated with the context.</summary>
    public CultureInfo Culture
    {
        get => this._culture ?? CultureInfo.CurrentCulture;
        set => this._culture = value;
    }

    /// <summary>Gets or sets the AI request settings associated with this context.</summary>
    public AIRequestSettings? RequestSettings { get; set; }

    /// <summary>Gets or sets additional arbitrary data passed around with function and service calls.</summary>
    public IDictionary<string, object?> Data
    {
        get => this._data ??= new Dictionary<string, object?>();
        set => this._data = value;
    }
}
