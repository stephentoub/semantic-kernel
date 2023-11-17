// Copyright (c) Microsoft. All rights reserved.

using System.Collections.Generic;
using System.Linq;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.Connectors.AI.OpenAI.AzureSdk;
using Xunit;

namespace SemanticKernel.Connectors.UnitTests.OpenAI.FunctionCalling;
public sealed class FunctionViewExtensionsTests
{
    [Fact]
    public void ItCanConvertToOpenAIFunctionNoParameters()
    {
        // Arrange
        var sut = new FunctionView(
            Name: "foo",
            PluginName: "bar",
            Description: "baz",
            ReturnParameter: new ReturnParameterView("retDesc", jsonSchema: SKParameterTypeJsonSchema.Parse("\"schema\"")));

        // Act
        var result = sut.ToOpenAIFunction();

        // Assert
        Assert.Equal(sut.Name, result.FunctionName);
        Assert.Equal(sut.PluginName, result.PluginName);
        Assert.Equal(sut.Description, result.Description);
        Assert.Equal($"{sut.PluginName}-{sut.Name}", result.FullyQualifiedName);
        Assert.NotNull(result.ReturnParameter);
        Assert.Equivalent(new OpenAIFunctionReturnParameter { Description = "retDesc", Schema = SKParameterTypeJsonSchema.Parse("\"schema\"") }, result.ReturnParameter);
    }

    [Fact]
    public void ItCanConvertToOpenAIFunctionNoPluginName()
    {
        // Arrange
        var sut = new FunctionView(
            Name: "foo",
            PluginName: string.Empty,
            Description: "baz",
            ReturnParameter: new ReturnParameterView("retDesc", jsonSchema: SKParameterTypeJsonSchema.Parse("\"schema\"")));

        // Act
        var result = sut.ToOpenAIFunction();

        // Assert
        Assert.Equal(sut.Name, result.FunctionName);
        Assert.Equal(sut.PluginName, result.PluginName);
        Assert.Equal(sut.Description, result.Description);
        Assert.Equal(sut.Name, result.FullyQualifiedName);
        Assert.NotNull(result.ReturnParameter);
        Assert.Equivalent(new OpenAIFunctionReturnParameter { Description = "retDesc", Schema = SKParameterTypeJsonSchema.Parse("\"schema\"") }, result.ReturnParameter);
    }

    [Fact]
    public void ItCanConvertToOpenAIFunctionWithParameter()
    {
        // Arrange
        var param1 = new ParameterView(
            name: "param1",
            description: "This is param1",
            defaultValue: "1",
            type: typeof(int),
            jsonType: new ParameterViewJsonType("int"));

        var sut = new FunctionView(
            Name: "foo",
            PluginName: "bar",
            Description: "baz",
            Parameters: new List<ParameterView> { param1 },
            ReturnParameter: new ReturnParameterView("retDesc", jsonSchema: SKParameterTypeJsonSchema.Parse("\"schema\"")));

        // Act
        var result = sut.ToOpenAIFunction();
        var outputParam = result.Parameters.First();

        // Assert
        Assert.Equal("int", outputParam.Type);
        Assert.Equal(param1.Name, outputParam.Name);
        Assert.Equal("This is param1 (default value: 1)", outputParam.Description);
        Assert.Equal(param1.IsRequired, outputParam.IsRequired);
        Assert.NotNull(outputParam.Schema);
        Assert.Equal("integer", outputParam.Schema.Element.GetProperty("type").GetString());
        Assert.Equivalent(new OpenAIFunctionReturnParameter { Description = "retDesc", Schema = SKParameterTypeJsonSchema.Parse("\"schema\"") }, result.ReturnParameter);
    }

    [Fact]
    public void ItCanConvertToOpenAIFunctionWithParameterNoType()
    {
        // Arrange
        var param1 = new ParameterView(
            name: "param1",
            description: "This is param1",
            jsonType: null);

        var sut = new FunctionView(
            Name: "foo",
            PluginName: "bar",
            Description: "baz",
            Parameters: new List<ParameterView> { param1 },
            ReturnParameter: new ReturnParameterView("retDesc", jsonSchema: SKParameterTypeJsonSchema.Parse("\"schema\"")));

        // Act
        var result = sut.ToOpenAIFunction();
        var outputParam = result.Parameters.First();

        // Assert
        Assert.Equal("string", outputParam.Type);
        Assert.Equal(param1.Name, outputParam.Name);
        Assert.Equal(param1.Description, outputParam.Description);
        Assert.Equal(param1.IsRequired, outputParam.IsRequired);
        Assert.Equivalent(new OpenAIFunctionReturnParameter { Description = "retDesc", Schema = SKParameterTypeJsonSchema.Parse("\"schema\"") }, result.ReturnParameter);
    }

    [Fact]
    public void ItCanConvertToOpenAIFunctionWithNoReturnParameterType()
    {
        // Arrange
        var param1 = new ParameterView(
            name: "param1",
            description: "This is param1",
            type: typeof(int),
            jsonType: null,
            isRequired: false);

        var sut = new FunctionView(
            Name: "foo",
            PluginName: "bar",
            Description: "baz",
            Parameters: new List<ParameterView> { param1 });

        // Act
        var result = sut.ToOpenAIFunction();
        var outputParam = result.Parameters.First();

        // Assert
        Assert.Equal("string", outputParam.Type);
        Assert.Equal(param1.Name, outputParam.Name);
        Assert.Equal(param1.Description, outputParam.Description);
        Assert.Equal(param1.IsRequired, outputParam.IsRequired);
        Assert.NotNull(outputParam.Schema);
        Assert.Equal("integer", outputParam.Schema.Element.GetProperty("type").GetString());
    }
}
