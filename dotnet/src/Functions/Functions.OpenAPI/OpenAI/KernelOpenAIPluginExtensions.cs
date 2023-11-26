﻿// Copyright (c) Microsoft. All rights reserved.

using System;
using System.IO;
using System.Net.Http;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.SemanticKernel.Functions.OpenAPI.Extensions;

namespace Microsoft.SemanticKernel.Functions.OpenAPI.OpenAI;

/// <summary>
/// Provides extension methods for importing plugins exposed through OpenAI's ChatGPT format.
/// </summary>
public static class KernelOpenAIPluginExtensions
{
    // TODO: Review XML comments

    /// <summary>
    /// Creates a plugin for an OpenAI plugin exposed through OpenAI's ChatGPT format and imports it into the <paramref name="kernel"/>'s plugin collection.
    /// </summary>
    /// <param name="kernel">Semantic Kernel instance.</param>
    /// <param name="pluginName">Plugin name.</param>
    /// <param name="filePath">The file path to the AI Plugin</param>
    /// <param name="executionParameters">Plugin execution parameters.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A collection of invocable functions</returns>
    public static async Task<IKernelPlugin> ImportPluginFromOpenAIAsync(
        this Kernel kernel,
        string pluginName,
        string filePath,
        OpenAIFunctionExecutionParameters? executionParameters = null,
        CancellationToken cancellationToken = default)
    {
        IKernelPlugin plugin = await kernel.CreatePluginFromOpenAIAsync(pluginName, filePath, executionParameters, cancellationToken).ConfigureAwait(false);
        kernel.Plugins.Add(plugin);
        return plugin;
    }

    /// <summary>
    /// Creates a plugin for an OpenAI plugin exposed through OpenAI's ChatGPT format and imports it into the <paramref name="kernel"/>'s plugin collection.
    /// </summary>
    /// <param name="kernel">Semantic Kernel instance.</param>
    /// <param name="pluginName">Plugin name.</param>
    /// <param name="uri">A local or remote URI referencing the AI Plugin</param>
    /// <param name="executionParameters">Plugin execution parameters.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A collection of invocable functions</returns>
    public static async Task<IKernelPlugin> ImportPluginFromOpenAIAsync(
        this Kernel kernel,
        string pluginName,
        Uri uri,
        OpenAIFunctionExecutionParameters? executionParameters = null,
        CancellationToken cancellationToken = default)
    {
        IKernelPlugin plugin = await kernel.CreatePluginFromOpenAIAsync(pluginName, uri, executionParameters, cancellationToken).ConfigureAwait(false);
        kernel.Plugins.Add(plugin);
        return plugin;
    }

    /// <summary>
    /// Creates a plugin for an OpenAI plugin exposed through OpenAI's ChatGPT format and imports it into the <paramref name="kernel"/>'s plugin collection.
    /// </summary>
    /// <param name="kernel">Semantic Kernel instance.</param>
    /// <param name="pluginName">Plugin name.</param>
    /// <param name="stream">A stream representing the AI Plugin</param>
    /// <param name="executionParameters">Plugin execution parameters.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A collection of invocable functions</returns>
    public static async Task<IKernelPlugin> ImportPluginFromOpenAIAsync(
        this Kernel kernel,
        string pluginName,
        Stream stream,
        OpenAIFunctionExecutionParameters? executionParameters = null,
        CancellationToken cancellationToken = default)
    {
        IKernelPlugin plugin = await kernel.CreatePluginFromOpenAIAsync(pluginName, stream, executionParameters, cancellationToken).ConfigureAwait(false);
        kernel.Plugins.Add(plugin);
        return plugin;
    }

    /// <summary>
    /// Creates a plugin for an OpenAI plugin exposed through OpenAI's ChatGPT format.
    /// </summary>
    /// <param name="kernel">Semantic Kernel instance.</param>
    /// <param name="pluginName">Plugin name.</param>
    /// <param name="filePath">The file path to the AI Plugin</param>
    /// <param name="executionParameters">Plugin execution parameters.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A collection of invocable functions</returns>
    public static async Task<IKernelPlugin> CreatePluginFromOpenAIAsync(
        this Kernel kernel,
        string pluginName,
        string filePath,
        OpenAIFunctionExecutionParameters? executionParameters = null,
        CancellationToken cancellationToken = default)
    {
        Verify.NotNull(kernel);
        Verify.ValidPluginName(pluginName, kernel.Plugins);

        var openAIManifest = await DocumentLoader.LoadDocumentFromFilePathAsync(
            filePath,
            kernel.GetService<ILoggerFactory>().CreateLogger(typeof(KernelOpenAIPluginExtensions)),
            cancellationToken).ConfigureAwait(false);

        return await CreateAsync(
            kernel,
            openAIManifest,
            pluginName,
            executionParameters,
            cancellationToken: cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Creates a plugin for an OpenAI plugin exposed through OpenAI's ChatGPT format.
    /// </summary>
    /// <param name="kernel">Semantic Kernel instance.</param>
    /// <param name="pluginName">Plugin name.</param>
    /// <param name="uri">A local or remote URI referencing the AI Plugin</param>
    /// <param name="executionParameters">Plugin execution parameters.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A collection of invocable functions</returns>
    public static async Task<IKernelPlugin> CreatePluginFromOpenAIAsync(
        this Kernel kernel,
        string pluginName,
        Uri uri,
        OpenAIFunctionExecutionParameters? executionParameters = null,
        CancellationToken cancellationToken = default)
    {
        Verify.NotNull(kernel);
        Verify.ValidPluginName(pluginName, kernel.Plugins);

#pragma warning disable CA2000 // Dispose objects before losing scope. No need to dispose the Http client here. It can either be an internal client using NonDisposableHttpClientHandler or an external client managed by the calling code, which should handle its disposal.
        var httpClient = HttpClientProvider.GetHttpClient(executionParameters?.HttpClient ?? kernel.Services.GetService<HttpClient>());
#pragma warning restore CA2000

        var openAIManifest = await DocumentLoader.LoadDocumentFromUriAsync(
            uri,
            kernel.GetService<ILoggerFactory>().CreateLogger(typeof(KernelOpenAIPluginExtensions)),
            httpClient,
            null, // auth is not needed when loading the manifest
            executionParameters?.UserAgent,
            cancellationToken).ConfigureAwait(false);

        return await CreateAsync(
            kernel,
            openAIManifest,
            pluginName,
            executionParameters,
            cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Creates a plugin for an OpenAI plugin exposed through OpenAI's ChatGPT format.
    /// </summary>
    /// <param name="kernel">Semantic Kernel instance.</param>
    /// <param name="pluginName">Plugin name.</param>
    /// <param name="stream">A stream representing the AI Plugin</param>
    /// <param name="executionParameters">Plugin execution parameters.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A collection of invocable functions</returns>
    public static async Task<IKernelPlugin> CreatePluginFromOpenAIAsync(
        this Kernel kernel,
        string pluginName,
        Stream stream,
        OpenAIFunctionExecutionParameters? executionParameters = null,
        CancellationToken cancellationToken = default)
    {
        Verify.NotNull(kernel);
        Verify.ValidPluginName(pluginName, kernel.Plugins);

        var openAIManifest = await DocumentLoader.LoadDocumentFromStreamAsync(stream).ConfigureAwait(false);

        return await CreateAsync(
            kernel,
            openAIManifest,
            pluginName,
            executionParameters,
            cancellationToken: cancellationToken).ConfigureAwait(false);
    }

    #region private

    private static async Task<IKernelPlugin> CreateAsync(
        Kernel kernel,
        string openAIManifest,
        string pluginName,
        OpenAIFunctionExecutionParameters? executionParameters = null,
        CancellationToken cancellationToken = default)
    {
        JsonNode pluginJson;
        OpenAIAuthenticationConfig openAIAuthConfig;
        try
        {
            pluginJson = JsonNode.Parse(openAIManifest)!;
            openAIAuthConfig = pluginJson["auth"].Deserialize<OpenAIAuthenticationConfig>()!;
        }
        catch (JsonException ex)
        {
            throw new KernelException("Parsing of Open AI manifest failed.", ex);
        }

        if (executionParameters?.AuthCallback is not null)
        {
            var callback = executionParameters.AuthCallback;
            ((OpenApiFunctionExecutionParameters)executionParameters).AuthCallback = async (request) =>
            {
                await callback(request, pluginName, openAIAuthConfig).ConfigureAwait(false);
            };
        }

        return await kernel.CreatePluginFromOpenApiAsync(
            pluginName,
            ParseOpenAIManifestForOpenApiSpecUrl(pluginJson),
            executionParameters,
            cancellationToken).ConfigureAwait(false);
    }

    private static Uri ParseOpenAIManifestForOpenApiSpecUrl(JsonNode pluginJson)
    {
        string? apiType = pluginJson?["api"]?["type"]?.ToString();
        if (string.IsNullOrWhiteSpace(apiType) || apiType != "openapi")
        {
            throw new KernelException($"Unexpected API type '{apiType}' found in Open AI manifest.");
        }

        string? apiUrl = pluginJson?["api"]?["url"]?.ToString();
        if (string.IsNullOrWhiteSpace(apiUrl))
        {
            throw new KernelException("No Open API spec URL found in Open AI manifest.");
        }

        try
        {
            return new Uri(apiUrl);
        }
        catch (System.UriFormatException ex)
        {
            throw new KernelException("Invalid Open API spec URI found in Open AI manifest.", ex);
        }
    }

    #endregion
}
