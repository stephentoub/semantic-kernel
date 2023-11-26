﻿// Copyright (c) Microsoft. All rights reserved.

using System;
using System.Net.Http;
using Azure;
using Azure.AI.OpenAI;
using Azure.Core;
using Azure.Core.Pipeline;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.SemanticKernel.AI.ChatCompletion;
using Microsoft.SemanticKernel.AI.Embeddings;
using Microsoft.SemanticKernel.AI.ImageGeneration;
using Microsoft.SemanticKernel.AI.TextCompletion;
using Microsoft.SemanticKernel.Connectors.AI.OpenAI.ChatCompletion;
using Microsoft.SemanticKernel.Connectors.AI.OpenAI.ChatCompletionWithData;
using Microsoft.SemanticKernel.Connectors.AI.OpenAI.ImageGeneration;
using Microsoft.SemanticKernel.Connectors.AI.OpenAI.TextCompletion;
using Microsoft.SemanticKernel.Connectors.AI.OpenAI.TextEmbedding;

#pragma warning disable IDE0130
// ReSharper disable once CheckNamespace - Using NS of KernelConfig
namespace Microsoft.SemanticKernel;
#pragma warning restore IDE0130

/// <summary>
/// Provides extension methods for the <see cref="KernelBuilder"/> class to configure OpenAI and AzureOpenAI connectors.
/// </summary>
public static class OpenAIKernelBuilderExtensions
{
    #region Text Completion

    /// <summary>
    /// Adds an Azure OpenAI text completion service to the list.
    /// See https://learn.microsoft.com/azure/cognitive-services/openai for service details.
    /// </summary>
    /// <param name="builder">The <see cref="KernelBuilder"/> instance</param>
    /// <param name="deploymentName">Azure OpenAI deployment name, see https://learn.microsoft.com/azure/cognitive-services/openai/how-to/create-resource</param>
    /// <param name="endpoint">Azure OpenAI deployment URL, see https://learn.microsoft.com/azure/cognitive-services/openai/quickstart</param>
    /// <param name="apiKey">Azure OpenAI API key, see https://learn.microsoft.com/azure/cognitive-services/openai/quickstart</param>
    /// <param name="serviceId">A local identifier for the given AI service</param>
    /// <param name="modelId">Model identifier, see https://learn.microsoft.com/azure/cognitive-services/openai/quickstart</param>
    /// <param name="httpClient">Custom <see cref="HttpClient"/> for HTTP requests.</param>
    /// <returns>Self instance</returns>
    public static KernelBuilder WithAzureTextCompletionService(this KernelBuilder builder,
        string deploymentName,
        string endpoint,
        string apiKey,
        string? serviceId = null,
        string? modelId = null,
        HttpClient? httpClient = null)
    {
        builder.WithAIService<ITextCompletion>(serviceId, serviceProvider =>
        {
            httpClient ??= serviceProvider.GetService<HttpClient>();

            var client = CreateAzureOpenAIClient(deploymentName, endpoint, new AzureKeyCredential(apiKey), httpClient);
            return new AzureTextCompletion(deploymentName, client, modelId, serviceProvider.GetService<ILoggerFactory>());
        });

        return builder;
    }

    /// <summary>
    /// Adds an Azure OpenAI text completion service to the list.
    /// See https://learn.microsoft.com/azure/cognitive-services/openai for service details.
    /// </summary>
    /// <param name="builder">The <see cref="KernelBuilder"/> instance</param>
    /// <param name="deploymentName">Azure OpenAI deployment name, see https://learn.microsoft.com/azure/cognitive-services/openai/how-to/create-resource</param>
    /// <param name="endpoint">Azure OpenAI deployment URL, see https://learn.microsoft.com/azure/cognitive-services/openai/quickstart</param>
    /// <param name="credentials">Token credentials, e.g. DefaultAzureCredential, ManagedIdentityCredential, EnvironmentCredential, etc.</param>
    /// <param name="serviceId">A local identifier for the given AI service</param>
    /// <param name="modelId">Model identifier, see https://learn.microsoft.com/azure/cognitive-services/openai/quickstart</param>
    /// <param name="httpClient">Custom <see cref="HttpClient"/> for HTTP requests.</param>
    /// <returns>Self instance</returns>
    public static KernelBuilder WithAzureTextCompletionService(this KernelBuilder builder,
        string deploymentName,
        string endpoint,
        TokenCredential credentials,
        string? serviceId = null,
        string? modelId = null,
        HttpClient? httpClient = null)
    {
        builder.WithAIService<ITextCompletion>(serviceId, serviceProvider =>
        {
            httpClient ??= serviceProvider.GetService<HttpClient>();

            var client = CreateAzureOpenAIClient(deploymentName, endpoint, credentials, httpClient);
            return new AzureTextCompletion(deploymentName, client, modelId, serviceProvider.GetService<ILoggerFactory>());
        });

        return builder;
    }

    /// <summary>
    /// Adds an Azure OpenAI text completion service to the list.
    /// See https://learn.microsoft.com/azure/cognitive-services/openai for service details.
    /// </summary>
    /// <param name="builder">The <see cref="KernelBuilder"/> instance</param>
    /// <param name="deploymentName">Azure OpenAI deployment name, see https://learn.microsoft.com/azure/cognitive-services/openai/how-to/create-resource</param>
    /// <param name="openAIClient">Custom <see cref="OpenAIClient"/>.</param>
    /// <param name="serviceId">A local identifier for the given AI service</param>
    /// <param name="modelId">Model identifier, see https://learn.microsoft.com/azure/cognitive-services/openai/quickstart</param>
    /// <returns>Self instance</returns>
    public static KernelBuilder WithAzureTextCompletionService(this KernelBuilder builder,
        string deploymentName,
        OpenAIClient openAIClient,
        string? serviceId = null,
        string? modelId = null)
    {
        builder.WithAIService<ITextCompletion>(serviceId, serviceProvider =>
            new AzureTextCompletion(
                deploymentName,
                openAIClient,
                modelId,
                serviceProvider.GetService<ILoggerFactory>()));

        return builder;
    }

    /// <summary>
    /// Adds the OpenAI text completion service to the list.
    /// See https://platform.openai.com/docs for service details.
    /// </summary>
    /// <param name="builder">The <see cref="KernelBuilder"/> instance</param>
    /// <param name="modelId">OpenAI model name, see https://platform.openai.com/docs/models</param>
    /// <param name="apiKey">OpenAI API key, see https://platform.openai.com/account/api-keys</param>
    /// <param name="orgId">OpenAI organization id. This is usually optional unless your account belongs to multiple organizations.</param>
    /// <param name="serviceId">A local identifier for the given AI service</param>
    /// <param name="httpClient">Custom <see cref="HttpClient"/> for HTTP requests.</param>
    /// <returns>Self instance</returns>
    public static KernelBuilder WithOpenAITextCompletionService(this KernelBuilder builder,
        string modelId,
        string apiKey,
        string? orgId = null,
        string? serviceId = null,
        HttpClient? httpClient = null)
    {
        builder.WithAIService<ITextCompletion>(serviceId, serviceProvider =>
        {
            ILoggerFactory loggerFactory = serviceProvider.GetService<ILoggerFactory>() ?? NullLoggerFactory.Instance;
            httpClient ??= serviceProvider.GetService<HttpClient>();
            return new OpenAITextCompletion(
                modelId,
                apiKey,
                orgId,
                HttpClientProvider.GetHttpClient(serviceProvider),
                loggerFactory);
        });
        return builder;
    }

    #endregion

    #region Text Embedding

    /// <summary>
    /// Adds an Azure OpenAI text embeddings service to the list.
    /// See https://learn.microsoft.com/azure/cognitive-services/openai for service details.
    /// </summary>
    /// <param name="builder">The <see cref="KernelBuilder"/> instance</param>
    /// <param name="deploymentName">Azure OpenAI deployment name, see https://learn.microsoft.com/azure/cognitive-services/openai/how-to/create-resource</param>
    /// <param name="endpoint">Azure OpenAI deployment URL, see https://learn.microsoft.com/azure/cognitive-services/openai/quickstart</param>
    /// <param name="apiKey">Azure OpenAI API key, see https://learn.microsoft.com/azure/cognitive-services/openai/quickstart</param>
    /// <param name="serviceId">A local identifier for the given AI service</param>
    /// <param name="modelId">Model identifier, see https://learn.microsoft.com/azure/cognitive-services/openai/quickstart</param>
    /// <param name="httpClient">Custom <see cref="HttpClient"/> for HTTP requests.</param>
    /// <returns>Self instance</returns>
    public static KernelBuilder WithAzureOpenAITextEmbeddingGenerationService(this KernelBuilder builder,
        string deploymentName,
        string endpoint,
        string apiKey,
        string? serviceId = null,
        string? modelId = null,
        HttpClient? httpClient = null)
    {
        builder.WithAIService<ITextEmbeddingGeneration>(serviceId, serviceProvider =>
        {
            httpClient ??= serviceProvider.GetService<HttpClient>();
            return new AzureOpenAITextEmbeddingGeneration(
                deploymentName,
                endpoint,
                apiKey,
                modelId,
                HttpClientProvider.GetHttpClient(serviceProvider),
                serviceProvider.GetService<ILoggerFactory>());
        });
        return builder;
    }

    /// <summary>
    /// Adds an Azure OpenAI text embeddings service to the list.
    /// See https://learn.microsoft.com/azure/cognitive-services/openai for service details.
    /// </summary>
    /// <param name="builder">The <see cref="KernelBuilder"/> instance</param>
    /// <param name="deploymentName">Azure OpenAI deployment name, see https://learn.microsoft.com/azure/cognitive-services/openai/how-to/create-resource</param>
    /// <param name="endpoint">Azure OpenAI deployment URL, see https://learn.microsoft.com/azure/cognitive-services/openai/quickstart</param>
    /// <param name="credential">Token credentials, e.g. DefaultAzureCredential, ManagedIdentityCredential, EnvironmentCredential, etc.</param>
    /// <param name="serviceId">A local identifier for the given AI service</param>
    /// <param name="modelId">Model identifier, see https://learn.microsoft.com/azure/cognitive-services/openai/quickstart</param>
    /// <param name="httpClient">Custom <see cref="HttpClient"/> for HTTP requests.</param>
    /// <returns>Self instance</returns>
    public static KernelBuilder WithAzureOpenAITextEmbeddingGenerationService(this KernelBuilder builder,
        string deploymentName,
        string endpoint,
        TokenCredential credential,
        string? serviceId = null,
        string? modelId = null,
        HttpClient? httpClient = null)
    {
        builder.WithAIService<ITextEmbeddingGeneration>(serviceId, serviceProvider =>
        {
            httpClient ??= serviceProvider.GetService<HttpClient>();
            return new AzureOpenAITextEmbeddingGeneration(
                deploymentName,
                endpoint,
                credential,
                modelId,
                HttpClientProvider.GetHttpClient(serviceProvider),
                serviceProvider.GetService<ILoggerFactory>());
        });
        return builder;
    }

    /// <summary>
    /// Adds the OpenAI text embeddings service to the list.
    /// See https://platform.openai.com/docs for service details.
    /// </summary>
    /// <param name="builder">The <see cref="KernelBuilder"/> instance</param>
    /// <param name="modelId">OpenAI model name, see https://platform.openai.com/docs/models</param>
    /// <param name="apiKey">OpenAI API key, see https://platform.openai.com/account/api-keys</param>
    /// <param name="orgId">OpenAI organization id. This is usually optional unless your account belongs to multiple organizations.</param>
    /// <param name="serviceId">A local identifier for the given AI service</param>
    /// <param name="httpClient">Custom <see cref="HttpClient"/> for HTTP requests.</param>
    /// <returns>Self instance</returns>
    public static KernelBuilder WithOpenAITextEmbeddingGenerationService(this KernelBuilder builder,
        string modelId,
        string apiKey,
        string? orgId = null,
        string? serviceId = null,
        HttpClient? httpClient = null)
    {
        builder.WithAIService<ITextEmbeddingGeneration>(serviceId, serviceProvider =>
        {
            httpClient ??= serviceProvider.GetService<HttpClient>();
            return new OpenAITextEmbeddingGeneration(
                modelId,
                apiKey,
                orgId,
                HttpClientProvider.GetHttpClient(serviceProvider),
                serviceProvider.GetService<ILoggerFactory>());
        });
        return builder;
    }

    #endregion

    #region Chat Completion

    /// <summary>
    /// Adds the Azure OpenAI ChatGPT completion service to the list.
    /// See https://platform.openai.com/docs for service details.
    /// </summary>
    /// <param name="builder">The <see cref="KernelBuilder"/> instance</param>
    /// <param name="deploymentName">Azure OpenAI deployment name, see https://learn.microsoft.com/azure/cognitive-services/openai/how-to/create-resource</param>
    /// <param name="endpoint">Azure OpenAI deployment URL, see https://learn.microsoft.com/azure/cognitive-services/openai/quickstart</param>
    /// <param name="apiKey">Azure OpenAI API key, see https://learn.microsoft.com/azure/cognitive-services/openai/quickstart</param>
    /// <param name="alsoAsTextCompletion">Whether to use the service also for text completion, if supported</param>
    /// <param name="serviceId">A local identifier for the given AI service</param>
    /// <param name="modelId">Model identifier</param>
    /// <param name="httpClient">Custom <see cref="HttpClient"/> for HTTP requests.</param>
    /// <returns>Self instance</returns>
    public static KernelBuilder WithAzureOpenAIChatCompletionService(this KernelBuilder builder,
        string deploymentName,
        string endpoint,
        string apiKey,
        bool alsoAsTextCompletion = true,
        string? serviceId = null,
        string? modelId = null,
        HttpClient? httpClient = null)
    {
        AzureOpenAIChatCompletion Factory(IServiceProvider serviceProvider)
        {
            ILoggerFactory loggerFactory = serviceProvider.GetService<ILoggerFactory>() ?? NullLoggerFactory.Instance;
            httpClient ??= serviceProvider.GetService<HttpClient>();

            OpenAIClient client = CreateAzureOpenAIClient(deploymentName, endpoint, new AzureKeyCredential(apiKey), httpClient);

            return new(deploymentName, client, modelId, loggerFactory);
        }

        builder.WithAIService<IChatCompletion>(serviceId, Factory);

        if (alsoAsTextCompletion)
        {
            builder.WithAIService<ITextCompletion>(serviceId, Factory);
        }

        return builder;
    }

    /// <summary>
    /// Adds the Azure OpenAI ChatGPT completion service to the list.
    /// See https://platform.openai.com/docs for service details.
    /// </summary>
    /// <param name="builder">The <see cref="KernelBuilder"/> instance</param>
    /// <param name="deploymentName">Azure OpenAI deployment name, see https://learn.microsoft.com/azure/cognitive-services/openai/how-to/create-resource</param>
    /// <param name="endpoint">Azure OpenAI deployment URL, see https://learn.microsoft.com/azure/cognitive-services/openai/quickstart</param>
    /// <param name="credentials">Token credentials, e.g. DefaultAzureCredential, ManagedIdentityCredential, EnvironmentCredential, etc.</param>
    /// <param name="alsoAsTextCompletion">Whether to use the service also for text completion, if supported</param>
    /// <param name="serviceId">A local identifier for the given AI service</param>
    /// <param name="modelId">Model identifier</param>
    /// <param name="httpClient">Custom <see cref="HttpClient"/> for HTTP requests.</param>
    /// <returns>Self instance</returns>
    public static KernelBuilder WithAzureOpenAIChatCompletionService(this KernelBuilder builder,
        string deploymentName,
        string endpoint,
        TokenCredential credentials,
        bool alsoAsTextCompletion = true,
        string? serviceId = null,
        string? modelId = null,
        HttpClient? httpClient = null)
    {
        AzureOpenAIChatCompletion Factory(IServiceProvider serviceProvider)
        {
            ILoggerFactory loggerFactory = serviceProvider.GetService<ILoggerFactory>() ?? NullLoggerFactory.Instance;
            httpClient ??= serviceProvider.GetService<HttpClient>();

            OpenAIClient client = CreateAzureOpenAIClient(deploymentName, endpoint, credentials, httpClient);

            return new(deploymentName, client, modelId, loggerFactory);
        }

        builder.WithAIService<IChatCompletion>(serviceId, Factory);

        // If the class implements the text completion interface, allow to use it also for semantic functions
        if (alsoAsTextCompletion)
        {
            builder.WithAIService<ITextCompletion>(serviceId, Factory);
        }

        return builder;
    }

    /// <summary>
    /// Adds the Azure OpenAI chat completion with data service to the list.
    /// More information: <see href="https://learn.microsoft.com/en-us/azure/ai-services/openai/use-your-data-quickstart"/>
    /// </summary>
    /// <param name="builder">The <see cref="KernelBuilder"/> instance.</param>
    /// <param name="config">Required configuration for Azure OpenAI chat completion with data.</param>
    /// <param name="alsoAsTextCompletion">Whether to use the service also for text completion, if supported.</param>
    /// <param name="serviceId">A local identifier for the given AI service.</param>
    /// <param name="httpClient">Custom <see cref="HttpClient"/> for HTTP requests.</param>
    /// <returns>Self instance</returns>
    public static KernelBuilder WithAzureOpenAIChatCompletionService(this KernelBuilder builder,
        AzureOpenAIChatCompletionWithDataConfig config,
        bool alsoAsTextCompletion = true,
        string? serviceId = null,
        HttpClient? httpClient = null)
    {
        AzureOpenAIChatCompletionWithData Factory(IServiceProvider serviceProvider) =>
            new(config,
                HttpClientProvider.GetHttpClient(httpClient ?? serviceProvider.GetService<HttpClient>()),
                serviceProvider.GetService<ILoggerFactory>());

        builder.WithAIService<IChatCompletion>(serviceId, Factory);

        if (alsoAsTextCompletion)
        {
            builder.WithAIService<ITextCompletion>(serviceId, Factory);
        }

        return builder;
    }

    /// <summary>
    /// Adds the OpenAI ChatGPT completion service to the list.
    /// See https://platform.openai.com/docs for service details.
    /// </summary>
    /// <param name="builder">The <see cref="KernelBuilder"/> instance</param>
    /// <param name="modelId">OpenAI model name, see https://platform.openai.com/docs/models</param>
    /// <param name="apiKey">OpenAI API key, see https://platform.openai.com/account/api-keys</param>
    /// <param name="orgId">OpenAI organization id. This is usually optional unless your account belongs to multiple organizations.</param>
    /// <param name="serviceId">A local identifier for the given AI service</param>
    /// <param name="alsoAsTextCompletion">Whether to use the service also for text completion, if supported</param>
    /// <param name="httpClient">Custom <see cref="HttpClient"/> for HTTP requests.</param>
    /// <returns>Self instance</returns>
    public static KernelBuilder WithOpenAIChatCompletionService(this KernelBuilder builder,
        string modelId,
        string apiKey,
        string? orgId = null,
        string? serviceId = null,
        bool alsoAsTextCompletion = true,
        HttpClient? httpClient = null)
    {
        OpenAIChatCompletion Factory(IServiceProvider serviceProvider)
        {
            ILoggerFactory loggerFactory = serviceProvider.GetService<ILoggerFactory>() ?? NullLoggerFactory.Instance;
            httpClient ??= serviceProvider.GetService<HttpClient>();
            return new(
                modelId,
                apiKey,
                orgId,
                HttpClientProvider.GetHttpClient(httpClient),
                loggerFactory);
        }

        builder.WithAIService<IChatCompletion>(serviceId, Factory);

        // If the class implements the text completion interface, allow to use it also for semantic functions
        if (alsoAsTextCompletion)
        {
            builder.WithAIService<ITextCompletion>(serviceId, Factory);
        }

        return builder;
    }

    /// <summary>
    /// Adds the Azure OpenAI ChatGPT completion service to the list.
    /// See https://platform.openai.com/docs for service details.
    /// </summary>
    /// <param name="builder">The <see cref="KernelBuilder"/> instance</param>
    /// <param name="deploymentName">Azure OpenAI deployment name, see https://learn.microsoft.com/azure/cognitive-services/openai/how-to/create-resource</param>
    /// <param name="openAIClient">Custom <see cref="OpenAIClient"/> for HTTP requests.</param>
    /// <param name="alsoAsTextCompletion">Whether to use the service also for text completion, if supported</param>
    /// <param name="serviceId">A local identifier for the given AI service</param>
    /// <param name="modelId">Model identifier</param>
    /// <returns>Self instance</returns>
    public static KernelBuilder WithAzureOpenAIChatCompletionService(this KernelBuilder builder,
        string deploymentName,
        OpenAIClient openAIClient,
        bool alsoAsTextCompletion = true,
        string? serviceId = null,
        string? modelId = null)
    {
        AzureOpenAIChatCompletion Factory(IServiceProvider serviceProvider)
        {
            ILoggerFactory loggerFactory = serviceProvider.GetService<ILoggerFactory>() ?? NullLoggerFactory.Instance;
            return new(deploymentName, openAIClient, modelId, loggerFactory);
        };

        builder.WithAIService<IChatCompletion>(serviceId, Factory);

        // If the class implements the text completion interface, allow to use it also for semantic functions
        if (alsoAsTextCompletion)
        {
            builder.WithAIService<ITextCompletion>(serviceId, Factory);
        }

        return builder;
    }

    /// <summary>
    /// Adds the OpenAI ChatGPT completion service to the list.
    /// See https://platform.openai.com/docs for service details.
    /// </summary>
    /// <param name="builder">The <see cref="KernelBuilder"/> instance</param>
    /// <param name="modelId">OpenAI model id</param>
    /// <param name="openAIClient">Custom <see cref="OpenAIClient"/> for HTTP requests.</param>
    /// <param name="alsoAsTextCompletion">Whether to use the service also for text completion, if supported</param>
    /// <param name="serviceId">A local identifier for the given AI service</param>
    /// <returns>Self instance</returns>
    public static KernelBuilder WithOpenAIChatCompletionService(this KernelBuilder builder,
        string modelId,
        OpenAIClient openAIClient,
        bool alsoAsTextCompletion = true,
        string? serviceId = null)
    {
        OpenAIChatCompletion Factory(IServiceProvider serviceProvider)
        {
            ILoggerFactory loggerFactory = serviceProvider.GetService<ILoggerFactory>() ?? NullLoggerFactory.Instance;
            return new(modelId, openAIClient, loggerFactory);
        }

        builder.WithAIService<IChatCompletion>(serviceId, Factory);

        // If the class implements the text completion interface, allow to use it also for semantic functions
        if (alsoAsTextCompletion && typeof(ITextCompletion).IsAssignableFrom(typeof(AzureOpenAIChatCompletion)))
        {
            builder.WithAIService<ITextCompletion>(serviceId, Factory);
        }

        return builder;
    }

    #endregion

    #region Images

    /// <summary>
    /// Add the OpenAI DallE image generation service to the list
    /// </summary>
    /// <param name="builder">The <see cref="KernelBuilder"/> instance</param>
    /// <param name="apiKey">OpenAI API key, see https://platform.openai.com/account/api-keys</param>
    /// <param name="orgId">OpenAI organization id. This is usually optional unless your account belongs to multiple organizations.</param>
    /// <param name="serviceId">A local identifier for the given AI service</param>
    /// <param name="httpClient">Custom <see cref="HttpClient"/> for HTTP requests.</param>
    /// <returns>Self instance</returns>
    public static KernelBuilder WithOpenAIImageGenerationService(this KernelBuilder builder,
        string apiKey,
        string? orgId = null,
        string? serviceId = null,
        HttpClient? httpClient = null)
    {
        return builder.WithAIService<IImageGeneration>(serviceId, serviceProvider =>
            new OpenAIImageGeneration(
                apiKey,
                orgId,
                HttpClientProvider.GetHttpClient(httpClient ?? serviceProvider.GetService<HttpClient>()),
                serviceProvider.GetService<ILoggerFactory>()));
    }

    /// <summary>
    /// Add the  Azure OpenAI DallE image generation service to the list
    /// </summary>
    /// <param name="builder">The <see cref="KernelBuilder"/> instance</param>
    /// <param name="endpoint">Azure OpenAI deployment URL, see https://learn.microsoft.com/azure/cognitive-services/openai/quickstart</param>
    /// <param name="apiKey">Azure OpenAI API key, see https://learn.microsoft.com/azure/cognitive-services/openai/quickstart</param>
    /// <param name="serviceId">A local identifier for the given AI service</param>
    /// <param name="httpClient">Custom <see cref="HttpClient"/> for HTTP requests.</param>
    /// <param name="maxRetryCount">Maximum number of attempts to retrieve the image generation operation result.</param>
    /// <returns>Self instance</returns>
    public static KernelBuilder WithAzureOpenAIImageGenerationService(this KernelBuilder builder,
        string endpoint,
        string apiKey,
        string? serviceId = null,
        HttpClient? httpClient = null,
        int maxRetryCount = 5)
    {
        return builder.WithAIService<IImageGeneration>(serviceId, serviceProvider =>
            new AzureOpenAIImageGeneration(
                endpoint,
                apiKey,
                HttpClientProvider.GetHttpClient(httpClient ?? serviceProvider.GetService<HttpClient>()),
                serviceProvider.GetService<ILoggerFactory>(),
                maxRetryCount));
    }

    #endregion

    private static OpenAIClient CreateAzureOpenAIClient(string deploymentName, string endpoint, AzureKeyCredential credentials, HttpClient? httpClient)
    {
        OpenAIClientOptions options = CreateOpenAIClientOptions(httpClient);

        return new(new Uri(endpoint), credentials, options);
    }

    private static OpenAIClient CreateAzureOpenAIClient(string deploymentName, string endpoint, TokenCredential credentials, HttpClient? httpClient)
    {
        OpenAIClientOptions options = CreateOpenAIClientOptions(httpClient);

        return new(new Uri(endpoint), credentials, options);
    }

    private static OpenAIClientOptions CreateOpenAIClientOptions(HttpClient? httpClient)
    {
        OpenAIClientOptions options = new()
        {
#pragma warning disable CA2000 // Dispose objects before losing scope
            Transport = new HttpClientTransport(HttpClientProvider.GetHttpClient(httpClient)),
#pragma warning restore CA2000 // Dispose objects before losing scope
        };

        if (httpClient is not null)
        {
            // Disable Azure SDK retry policy if and only if a custom HttpClient is provided.
            options.RetryPolicy = new RetryPolicy(maxRetries: 0);
        }

        return options;
    }
}
