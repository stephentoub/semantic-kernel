// Copyright (c) Microsoft. All rights reserved.

using System;
using Microsoft.SemanticKernel.Diagnostics;

#pragma warning disable IDE0130
// ReSharper disable once CheckNamespace - Using the main namespace
namespace Microsoft.SemanticKernel;

/// <summary>Encapsulates information about a parameter of an <see cref="ISKFunction"/>.</summary>
public sealed class ParameterView
{
    private SKParameterTypeJsonSchema? _jsonSchema;

    /// <summary>Initializes the view.</summary>
    /// <param name="name">The name of the parameter, compose of alphanumeric and underscore characters only.</param>
    /// <param name="description">The description of the parameter.</param>
    /// <param name="defaultValue">The default value of the parameter, if provided.</param>
    /// <param name="isRequired">Whether the parameter is required.</param>
    /// <param name="type">The .NET type of the parameter. null if this parameter did not come from a .NET method.</param>
    /// <param name="jsonType">The <see cref="ParameterViewJsonType"/> of the parameter.</param>
    /// <param name="jsonSchema">The JSON schema of the parameter type.</param>
    public ParameterView(
        string name,
        string? description = null,
        string? defaultValue = null,
        bool isRequired = false,
        Type? type = null,
        ParameterViewJsonType? jsonType = null,
        SKParameterTypeJsonSchema? jsonSchema = null)
    {
        Verify.NotNull(name);

        this.Name = name;
        this.Description = description;
        this.DefaultValue = defaultValue;
        this.IsRequired = isRequired;
        this.Type = type;
        this.JsonType = jsonType;
        this._jsonSchema = jsonSchema;
    }

    /// <summary>Gets the name of the parameter, compose of alphanumeric and underscore characters only.</summary>
    public string Name { get; }

    /// <summary>Gets the description of the parameter.</summary>
    public string? Description { get; }

    /// <summary>Gets the default value of the parameter if one was supplied.</summary>
    public string? DefaultValue { get; }

    /// <summary>Gets whether the parameter is required.</summary>
    public bool IsRequired { get; }

    /// <summary>Gets the .NET type of the parameter.</summary>
    /// <remarks>null if this parameter did not come from a .NET method.</remarks>
    public Type? Type { get; }

    /// <summary>Gets the <see cref="ParameterViewJsonType"/> of the parameter.</summary>
    public ParameterViewJsonType? JsonType { get; }

    /// <summary>Gets the JSON schema of the parameter type.</summary>
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
