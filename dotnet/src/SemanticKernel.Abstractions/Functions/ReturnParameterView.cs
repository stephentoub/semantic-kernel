// Copyright (c) Microsoft. All rights reserved.

using System;

#pragma warning disable IDE0130
// ReSharper disable once CheckNamespace - Using the main namespace
namespace Microsoft.SemanticKernel;

/// <summary>Encapsulates information about the return parameter of an <see cref="ISKFunction"/>.</summary>
public sealed class ReturnParameterView
{
    private SKParameterTypeJsonSchema? _jsonSchema;

    /// <summary>Initializes the view.</summary>
    /// <param name="description">The description of the return parameter.</param>
    /// <param name="type">The .NET type of the return parameter. null if this parameter did not come from a .NET function.</param>
    /// <param name="jsonSchema">The JSON schema of the type.</param>
    public ReturnParameterView(
        string? description = null,
        Type? type = null,
        SKParameterTypeJsonSchema? jsonSchema = null)
    {
        this.Description = description;
        this.Type = type;
        this._jsonSchema = jsonSchema;
    }

    /// <summary>The description of the return parameter.</summary>
    public string? Description { get; }

    /// <summary>The .NET type of the return parameter.</summary>
    /// <remarks>null if this parameter did not come from a .NET function.</remarks>
    public Type? Type { get; }

    /// <summary>The JSON schema of the type.</summary>
    public SKParameterTypeJsonSchema? JsonSchema
    {
        get
        {
            if (this._jsonSchema is null && this.Type is not null)
            {
                this._jsonSchema = SKParameterTypeJsonSchema.FromType(this.Type, this.Description);
            }

            return this._jsonSchema;
        }
    }
}
