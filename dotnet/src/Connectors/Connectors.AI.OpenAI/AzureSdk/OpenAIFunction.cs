// Copyright (c) Microsoft. All rights reserved.

using System;
using System.Collections.Generic;
using System.Text.Json;
using Azure.AI.OpenAI;
using Json.Schema;
using Json.Schema.Generation;
using Microsoft.SemanticKernel.Text;

namespace Microsoft.SemanticKernel.Connectors.AI.OpenAI.AzureSdk;

/// <summary>
/// Represents a function parameter that can be passed to the OpenAI API
/// </summary>
public class OpenAIFunctionParameter
{
    /// <summary>
    /// Name of the parameter.
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Description of the parameter.
    /// </summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>
    /// Type of the parameter.
    /// </summary>
    public string Type { get; set; } = string.Empty;

    /// <summary>
    /// Whether the parameter is required or not.
    /// </summary>
    public bool IsRequired { get; set; } = false;

    /// <summary>
    /// The Json Schema of the parameter.
    /// </summary>
    public SKParameterTypeJsonSchema? Schema { get; set; } = null;

    /// <summary>
    /// The parameter Type.
    /// </summary>
    public Type? ParameterType { get; set; } = null;
}

/// <summary>
/// Represents a return parameter of a function that can be passed to the OpenAI API
/// </summary>
public class OpenAIFunctionReturnParameter
{
    /// <summary>
    /// Description of the parameter.
    /// </summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>
    /// The Json Schema of the parameter.
    /// </summary>
    public SKParameterTypeJsonSchema? Schema { get; set; } = null;

    /// <summary>
    /// The <see cref="Type"/> of the return parameter.
    /// </summary>
    public Type? ParameterType { get; set; } = null;
}

/// <summary>
/// Represents a function that can be passed to the OpenAI API
/// </summary>
public class OpenAIFunction
{
    /// <summary>
    /// Separator between the plugin name and the function name
    /// </summary>
    public const string NameSeparator = "-";

    /// <summary>
    /// Name of the function
    /// </summary>
    public string FunctionName { get; set; } = string.Empty;

    /// <summary>
    /// Name of the function's associated plugin, if applicable
    /// </summary>
    public string PluginName { get; set; } = string.Empty;

    /// <summary>
    /// Fully qualified name of the function. This is the concatenation of the plugin name and the function name,
    /// separated by the value of <see cref="NameSeparator"/>.
    /// If there is no plugin name, this is the same as the function name.
    /// </summary>
    public string FullyQualifiedName =>
        this.PluginName.IsNullOrEmpty() ? this.FunctionName : $"{this.PluginName}{NameSeparator}{this.FunctionName}";

    /// <summary>
    /// Description of the function
    /// </summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>
    /// List of parameters for the function
    /// </summary>
    public IList<OpenAIFunctionParameter> Parameters { get; set; } = new List<OpenAIFunctionParameter>();

    /// <summary>
    /// The return parameter of the function.
    /// </summary>
    public OpenAIFunctionReturnParameter ReturnParameter { get; set; } = new OpenAIFunctionReturnParameter();

    /// <summary>
    /// Cached <see cref="BinaryData"/> storing the JSON for a function with no parameters.
    /// </summary>
    private static readonly BinaryData s_zeroFunctionParametersSchema = new("{\"type\":\"object\",\"required\":[],\"properties\":{}}");

    /// <summary>
    /// Converts the <see cref="OpenAIFunction"/> to OpenAI's <see cref="FunctionDefinition"/>.
    /// </summary>
    /// <returns>A <see cref="FunctionDefinition"/> containing all the function information.</returns>
    public FunctionDefinition ToFunctionDefinition()
    {
        BinaryData resultParameters = s_zeroFunctionParametersSchema;
        if (this.Parameters.Count > 0)
        {
            var properties = new Dictionary<string, SKParameterTypeJsonSchema>();
            var required = new List<string>();

            foreach (var parameter in this.Parameters)
            {
                if (parameter.Schema is not null || parameter.ParameterType is not null)
                {
                    SKParameterTypeJsonSchema schema = parameter.Schema is not null ?
                        parameter.Schema :
                        SKParameterTypeJsonSchema.Parse(JsonSerializer.Serialize(
                            new JsonSchemaBuilder()
                                .FromType(parameter.ParameterType!)
                                .Description(parameter.Description ?? string.Empty)
                                .Build()));

                    properties.Add(parameter.Name, schema);

                    if (parameter.IsRequired)
                    {
                        required.Add(parameter.Name);
                    }
                }
            }

            resultParameters = BinaryData.FromObjectAsJson(new
            {
                type = "object",
                required = required,
                properties = properties,
            });
        }

        return new FunctionDefinition
        {
            Name = this.FullyQualifiedName,
            Description = this.Description,
            Parameters = resultParameters,
        };
    }
}
