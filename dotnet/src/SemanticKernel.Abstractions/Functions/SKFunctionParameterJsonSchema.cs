// Copyright (c) Microsoft. All rights reserved.

using System;
using System.Text.Json;
using System.Text.Json.Serialization;
using Json.Schema;
using Json.Schema.Generation;

#pragma warning disable IDE0130
// ReSharper disable once CheckNamespace - Using the main namespace
namespace Microsoft.SemanticKernel;

/// <summary>Represents a JSON schema used to describe types used in <see cref="ISKFunction"/>s.</summary>
[JsonConverter(typeof(SKJsonSchemaConverter))]
public sealed class SKParameterTypeJsonSchema
{
    /// <summary>The schema stored as a string.</summary>
    private string? _schemaAsString;

    /// <summary>Parses a JSON schema for a parameter type.</summary>
    /// <param name="jsonSchema">The JSON schema.</param>
    /// <returns></returns>
    public static SKParameterTypeJsonSchema Parse(string jsonSchema) => new(JsonSerializer.Deserialize<JsonElement>(jsonSchema));

    /// <summary>Initializes a new instance from the specified <see cref="JsonElement"/>.</summary>
    /// <param name="jsonSchema">The schema to be stored.</param>
    private SKParameterTypeJsonSchema(JsonElement jsonSchema) => this.Element = jsonSchema;

    /// <summary>Gets the <see cref="JsonElement"/> representing the schema.</summary>
    public JsonElement Element { get; }

    /// <summary>Gets the JSON schema as a string.</summary>
    public override string ToString() => this._schemaAsString ??= JsonSerializer.Serialize(this.Element);

    /// <summary>Converter for reading/writing the schema.</summary>
    private sealed class SKJsonSchemaConverter : JsonConverter<SKParameterTypeJsonSchema>
    {
        public override SKParameterTypeJsonSchema? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) =>
            new(JsonElement.ParseValue(ref reader));

        public override void Write(Utf8JsonWriter writer, SKParameterTypeJsonSchema value, JsonSerializerOptions options) =>
            value.Element.WriteTo(writer);
    }

    internal static SKParameterTypeJsonSchema FromType(Type type, string? description)
    {
        return Parse(JsonSerializer.Serialize(new JsonSchemaBuilder()
            .FromType(type)
            .Description(description ?? string.Empty)
            .Build()));
    }
}
