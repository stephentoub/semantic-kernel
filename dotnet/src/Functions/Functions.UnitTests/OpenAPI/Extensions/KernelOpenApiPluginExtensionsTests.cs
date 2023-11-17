﻿// Copyright (c) Microsoft. All rights reserved.

using System;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Mime;
using System.Text;
using System.Threading.Tasks;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.Functions.OpenAPI.Extensions;
using Microsoft.SemanticKernel.Functions.OpenAPI.Model;
using Microsoft.SemanticKernel.Functions.OpenAPI.OpenApi;
using Microsoft.SemanticKernel.Orchestration;
using SemanticKernel.Functions.UnitTests.OpenAPI.TestPlugins;
using Xunit;

namespace SemanticKernel.Functions.UnitTests.OpenAPI.Extensions;

public sealed class KernelOpenApiPluginExtensionsTests : IDisposable
{
    /// <summary>
    /// System under test - an instance of OpenApiDocumentParser class.
    /// </summary>
    private readonly OpenApiDocumentParser _sut;

    /// <summary>
    /// OpenAPI document stream.
    /// </summary>
    private readonly Stream _openApiDocument;

    /// <summary>
    /// Kernel instance.
    /// </summary>
    private readonly Kernel _kernel;

    /// <summary>
    /// Creates an instance of a <see cref="KernelOpenApiPluginExtensionsTests"/> class.
    /// </summary>
    public KernelOpenApiPluginExtensionsTests()
    {
        this._kernel = KernelBuilder.Create();

        this._openApiDocument = ResourcePluginsProvider.LoadFromResource("documentV2_0.json");

        this._sut = new OpenApiDocumentParser();
    }

    [Fact]
    public async Task ItCanIncludeOpenApiOperationParameterTypesIntoFunctionParametersViewAsync()
    {
        //Act
        var plugin = await this._kernel.ImportPluginFromOpenApiAsync("fakePlugin", this._openApiDocument);

        //Assert
        var setSecretFunction = plugin["SetSecret"];
        Assert.NotNull(setSecretFunction);

        var functionView = setSecretFunction.Describe();
        Assert.NotNull(functionView);

        var secretNameParameter = functionView.Parameters.First(p => p.Name == "secret_name");
        Assert.Equal(ParameterViewJsonType.String, secretNameParameter.JsonType);

        var apiVersionParameter = functionView.Parameters.First(p => p.Name == "api_version");
        Assert.Equal("string", apiVersionParameter?.JsonType?.ToString());

        var payloadParameter = functionView.Parameters.First(p => p.Name == "payload");
        Assert.Equal(ParameterViewJsonType.Object, payloadParameter.JsonType);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task ItUsesServerUrlOverrideIfProvidedAsync(bool removeServersProperty)
    {
        // Arrange
        const string DocumentUri = "http://localhost:3001/openapi.json";
        const string ServerUrlOverride = "https://server-override.com/";

        var openApiDocument = ResourcePluginsProvider.LoadFromResource("documentV3_0.json");

        if (removeServersProperty)
        {
            openApiDocument = OpenApiTestHelper.ModifyOpenApiDocument(openApiDocument, (doc) =>
            {
                doc.Remove("servers");
            });
        }

        using var messageHandlerStub = new HttpMessageHandlerStub(openApiDocument);
        using var httpClient = new HttpClient(messageHandlerStub, false);

        var executionParameters = new OpenApiFunctionExecutionParameters { HttpClient = httpClient, ServerUrlOverride = new Uri(ServerUrlOverride) };
        var variables = this.GetFakeContextVariables();

        // Act
        var plugin = await this._kernel.ImportPluginFromOpenApiAsync("fakePlugin", new Uri(DocumentUri), executionParameters);
        var setSecretFunction = plugin["SetSecret"];

        messageHandlerStub.ResetResponse();

        var result = await this._kernel.RunAsync(setSecretFunction, variables);

        // Assert
        Assert.NotNull(messageHandlerStub.RequestUri);
        Assert.StartsWith(ServerUrlOverride, messageHandlerStub.RequestUri.AbsoluteUri, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("documentV2_0.json")]
    [InlineData("documentV3_0.json")]
    public async Task ItUsesServerUrlFromOpenApiDocumentAsync(string documentFileName)
    {
        // Arrange
        const string DocumentUri = "http://localhost:3001/openapi.json";
        const string ServerUrlFromDocument = "https://my-key-vault.vault.azure.net/";

        var openApiDocument = ResourcePluginsProvider.LoadFromResource(documentFileName);

        using var messageHandlerStub = new HttpMessageHandlerStub(openApiDocument);
        using var httpClient = new HttpClient(messageHandlerStub, false);

        var executionParameters = new OpenApiFunctionExecutionParameters { HttpClient = httpClient };
        var variables = this.GetFakeContextVariables();

        // Act
        var plugin = await this._kernel.ImportPluginFromOpenApiAsync("fakePlugin", new Uri(DocumentUri), executionParameters);
        var setSecretFunction = plugin["SetSecret"];

        messageHandlerStub.ResetResponse();

        var result = await this._kernel.RunAsync(setSecretFunction, variables);

        // Assert
        Assert.NotNull(messageHandlerStub.RequestUri);
        Assert.StartsWith(ServerUrlFromDocument, messageHandlerStub.RequestUri.AbsoluteUri, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("http://localhost:3001/openapi.json", "http://localhost:3001/", "documentV2_0.json")]
    [InlineData("http://localhost:3001/openapi.json", "http://localhost:3001/", "documentV3_0.json")]
    [InlineData("https://api.example.com/openapi.json", "https://api.example.com/", "documentV2_0.json")]
    [InlineData("https://api.example.com/openapi.json", "https://api.example.com/", "documentV3_0.json")]
    [SuppressMessage("Design", "CA1054:URI-like parameters should not be strings", Justification = "Required for test data.")]
    public async Task ItUsesOpenApiDocumentHostUrlWhenServerUrlIsNotProvidedAsync(string documentUri, string expectedServerUrl, string documentFileName)
    {
        // Arrange
        var openApiDocument = ResourcePluginsProvider.LoadFromResource(documentFileName);

        using var content = OpenApiTestHelper.ModifyOpenApiDocument(openApiDocument, (doc) =>
        {
            doc.Remove("servers");
            doc.Remove("host");
            doc.Remove("schemes");
        });

        using var messageHandlerStub = new HttpMessageHandlerStub(content);
        using var httpClient = new HttpClient(messageHandlerStub, false);

        var executionParameters = new OpenApiFunctionExecutionParameters { HttpClient = httpClient };
        var variables = this.GetFakeContextVariables();

        // Act
        var plugin = await this._kernel.ImportPluginFromOpenApiAsync("fakePlugin", new Uri(documentUri), executionParameters);
        var setSecretFunction = plugin["SetSecret"];

        messageHandlerStub.ResetResponse();

        var result = await this._kernel.RunAsync(setSecretFunction, variables);

        // Assert
        Assert.NotNull(messageHandlerStub.RequestUri);
        Assert.StartsWith(expectedServerUrl, messageHandlerStub.RequestUri.AbsoluteUri, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ItShouldConvertPluginComplexResponseToStringToSaveItInContextAsync()
    {
        //Arrange
        using var messageHandlerStub = new HttpMessageHandlerStub();
        messageHandlerStub.ResponseToReturn.Content = new StringContent("fake-content", Encoding.UTF8, MediaTypeNames.Application.Json);

        using var httpClient = new HttpClient(messageHandlerStub, false);

        var executionParameters = new OpenApiFunctionExecutionParameters
        {
            HttpClient = httpClient
        };

        var fakePlugin = new FakePlugin();

        var openApiPlugins = await this._kernel.ImportPluginFromOpenApiAsync("fakePlugin", this._openApiDocument, executionParameters);
        var fakePlugins = this._kernel.ImportPluginFromObject(fakePlugin, "fakePlugin2");

        var kernel = KernelBuilder.Create();

        var arguments = new ContextVariables
        {
            { "secret-name", "fake-secret-name" },
            { "api-version", "fake-api-version" }
        };

        //Act
        var res = await kernel.RunAsync(arguments, openApiPlugins["GetSecret"], fakePlugins["DoFakeAction"]);

        //Assert
        Assert.NotNull(res);

        var openApiPluginResult = res.FunctionResults.FirstOrDefault();
        Assert.NotNull(openApiPluginResult);

        var result = openApiPluginResult.GetValue<RestApiOperationResponse>();

        //Check original response
        Assert.NotNull(result);
        Assert.Equal("fake-content", result.Content);

        //Check the response, converted to a string indirectly through an argument passed to a fake plugin that follows the OpenApi plugin in the pipeline since there's no direct access to the context.
        Assert.Equal("fake-content", fakePlugin.ParameterValueFakeMethodCalledWith);
    }

    public void Dispose()
    {
        this._openApiDocument.Dispose();
    }

    #region private ================================================================================

    private ContextVariables GetFakeContextVariables()
    {
        var variables = new ContextVariables
        {
            ["secret-name"] = "fake-secret-name",
            ["api-version"] = "fake-api-version",
            ["X-API-Version"] = "fake-api-version",
            ["payload"] = "fake-payload"
        };

        return variables;
    }

    private sealed class FakePlugin
    {
        public string? ParameterValueFakeMethodCalledWith { get; private set; }

        [SKFunction]
        public void DoFakeAction(string parameter)
        {
            this.ParameterValueFakeMethodCalledWith = parameter;
        }
    }

    #endregion
}
