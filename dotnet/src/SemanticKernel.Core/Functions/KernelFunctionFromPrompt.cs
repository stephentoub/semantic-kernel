﻿// Copyright (c) Microsoft. All rights reserved.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.Metrics;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel.Services;
using Microsoft.SemanticKernel.TextGeneration;

namespace Microsoft.SemanticKernel;

/// <summary>
/// A Semantic Kernel "Semantic" prompt function.
/// </summary>
[DebuggerDisplay("{DebuggerDisplay,nq}")]
internal sealed class KernelFunctionFromPrompt : KernelFunction
{
    /// <summary>
    /// Creates a <see cref="KernelFunction"/> instance for a prompt specified via a prompt template.
    /// </summary>
    /// <param name="promptTemplate">Prompt template for the function, defined using the <see cref="PromptTemplateConfig.SemanticKernelTemplateFormat"/> template format.</param>
    /// <param name="executionSettings">Default execution settings to use when invoking this prompt function.</param>
    /// <param name="functionName">A name for the given function. The name can be referenced in templates and used by the pipeline planner.</param>
    /// <param name="description">The description to use for the function.</param>
    /// <param name="templateFormat">Optional format of the template. Must be provided if a prompt template factory is provided</param>
    /// <param name="promptTemplateFactory">Optional: Prompt template factory</param>
    /// <param name="loggerFactory">Logger factory</param>
    /// <returns>A function ready to use</returns>
    public static KernelFunction Create(
        string promptTemplate,
        Dictionary<string, PromptExecutionSettings>? executionSettings = null,
        string? functionName = null,
        string? description = null,
        string? templateFormat = null,
        IPromptTemplateFactory? promptTemplateFactory = null,
        ILoggerFactory? loggerFactory = null)
    {
        Verify.NotNullOrWhiteSpace(promptTemplate);

        if (promptTemplateFactory is not null)
        {
            if (string.IsNullOrWhiteSpace(templateFormat))
            {
                throw new ArgumentException($"Template format is required when providing a {nameof(promptTemplateFactory)}", nameof(templateFormat));
            }
        }

        var promptConfig = new PromptTemplateConfig
        {
            TemplateFormat = templateFormat ?? PromptTemplateConfig.SemanticKernelTemplateFormat,
            Name = functionName,
            Description = description ?? "Generic function, unknown purpose",
            Template = promptTemplate
        };

        if (executionSettings is not null)
        {
            promptConfig.ExecutionSettings = executionSettings;
        }

        var factory = promptTemplateFactory ?? new KernelPromptTemplateFactory(loggerFactory);

        return Create(
            promptTemplate: factory.Create(promptConfig),
            promptConfig: promptConfig,
            loggerFactory: loggerFactory);
    }

    /// <summary>
    /// Creates a <see cref="KernelFunction"/> instance for a prompt specified via a prompt template configuration.
    /// </summary>
    /// <param name="promptConfig">Prompt template configuration</param>
    /// <param name="promptTemplateFactory">Optional: Prompt template factory</param>
    /// <param name="loggerFactory">Logger factory</param>
    /// <returns>A function ready to use</returns>
    public static KernelFunction Create(
        PromptTemplateConfig promptConfig,
        IPromptTemplateFactory? promptTemplateFactory = null,
        ILoggerFactory? loggerFactory = null)
    {
        var factory = promptTemplateFactory ?? new KernelPromptTemplateFactory(loggerFactory);

        return Create(
            promptTemplate: factory.Create(promptConfig),
            promptConfig: promptConfig,
            loggerFactory: loggerFactory);
    }

    /// <summary>
    /// Creates a <see cref="KernelFunction"/> instance for a prompt specified via a prompt template and a prompt template configuration.
    /// </summary>
    /// <param name="promptTemplate">Prompt template for the function, defined using the <see cref="PromptTemplateConfig.SemanticKernelTemplateFormat"/> template format.</param>
    /// <param name="promptConfig">Prompt template configuration.</param>
    /// <param name="loggerFactory">Logger factory</param>
    /// <returns>A function ready to use</returns>
    public static KernelFunction Create(
        IPromptTemplate promptTemplate,
        PromptTemplateConfig promptConfig,
        ILoggerFactory? loggerFactory = null)
    {
        Verify.NotNull(promptTemplate);
        Verify.NotNull(promptConfig);

        return new KernelFunctionFromPrompt(
            template: promptTemplate,
            promptConfig: promptConfig,
            loggerFactory: loggerFactory);
    }

    /// <inheritdoc/>j
    protected override async ValueTask<FunctionResult> InvokeCoreAsync(
        Kernel kernel,
        KernelArguments arguments,
        CancellationToken cancellationToken = default)
    {
        this.AddDefaultValues(arguments);

        (var aiService, var executionSettings, var renderedPrompt, var renderedEventArgs) = await this.RenderPromptAsync(kernel, arguments, cancellationToken).ConfigureAwait(false);
        if (renderedEventArgs?.Cancel is true)
        {
            throw new OperationCanceledException($"A {nameof(Kernel)}.{nameof(Kernel.PromptRendered)} event handler requested cancellation before function invocation.");
        }

        if (aiService is IChatCompletionService chatCompletion)
        {
            var chatContent = await chatCompletion.GetChatMessageContentAsync(renderedPrompt, executionSettings, kernel, cancellationToken).ConfigureAwait(false);
            this.CaptureUsageDetails(chatContent.ModelId, chatContent.Metadata, this._logger);
            return new FunctionResult(this, chatContent, kernel.Culture, chatContent.Metadata);
        }

        if (aiService is ITextGenerationService textGeneration)
        {
            var textContent = await textGeneration.GetTextContentWithDefaultParserAsync(renderedPrompt, executionSettings, kernel, cancellationToken).ConfigureAwait(false);
            this.CaptureUsageDetails(textContent.ModelId, textContent.Metadata, this._logger);
            return new FunctionResult(this, textContent, kernel.Culture, textContent.Metadata);
        }

        // The service selector didn't find an appropriate service. This should only happen with a poorly implemented selector.
        throw new NotSupportedException($"The AI service {aiService.GetType()} is not supported. Supported services are {typeof(IChatCompletionService)} and {typeof(ITextGenerationService)}");
    }

    protected override async IAsyncEnumerable<TResult> InvokeStreamingCoreAsync<TResult>(
        Kernel kernel,
        KernelArguments arguments,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        this.AddDefaultValues(arguments);

        (var aiService, var executionSettings, var renderedPrompt, var renderedEventArgs) = await this.RenderPromptAsync(kernel, arguments, cancellationToken).ConfigureAwait(false);
        if (renderedEventArgs?.Cancel ?? false)
        {
            yield break;
        }

        IAsyncEnumerable<StreamingKernelContent>? asyncReference;
        if (aiService is IChatCompletionService chatCompletion)
        {
            asyncReference = chatCompletion.GetStreamingChatMessageContentsAsync(renderedPrompt, executionSettings, kernel, cancellationToken);
        }
        else if (aiService is ITextGenerationService textGeneration)
        {
            asyncReference = textGeneration.GetStreamingTextContentsWithDefaultParserAsync(renderedPrompt, executionSettings, kernel, cancellationToken);
        }
        else
        {
            // The service selector didn't find an appropriate service. This should only happen with a poorly implemented selector.
            throw new NotSupportedException($"The AI service {aiService.GetType()} is not supported. Supported services are {typeof(IChatCompletionService)} and {typeof(ITextGenerationService)}");
        }

        await foreach (var content in asyncReference)
        {
            cancellationToken.ThrowIfCancellationRequested();

            yield return typeof(TResult) switch
            {
                _ when typeof(TResult) == typeof(string)
                    => (TResult)(object)content.ToString(),

                _ when content is TResult contentAsT
                    => contentAsT,

                _ when content.InnerContent is TResult innerContentAsT
                    => innerContentAsT,

                _ when typeof(TResult) == typeof(byte[])
                    => (TResult)(object)content.ToByteArray(),

                _ => throw new NotSupportedException($"The specific type {typeof(TResult)} is not supported. Support types are {typeof(StreamingTextContent)}, string, byte[], or a matching type for {typeof(StreamingTextContent)}.{nameof(StreamingTextContent.InnerContent)} property")
            };
        }

        // There is no post cancellation check to override the result as the stream data was already sent.
    }

    /// <summary>
    /// JSON serialized string representation of the function.
    /// </summary>
    public override string ToString() => JsonSerializer.Serialize(this);

    private KernelFunctionFromPrompt(
        IPromptTemplate template,
        PromptTemplateConfig promptConfig,
        ILoggerFactory? loggerFactory = null) : base(
            promptConfig.Name ?? CreateRandomFunctionName(),
            promptConfig.Description ?? string.Empty,
            promptConfig.GetKernelParametersMetadata(),
            promptConfig.GetKernelReturnParameterMetadata(),
            promptConfig.ExecutionSettings)
    {
        this._logger = loggerFactory?.CreateLogger(typeof(KernelFunctionFactory)) ?? NullLogger.Instance;

        this._promptTemplate = template;
        this._promptConfig = promptConfig;
    }

    #region private

    private readonly ILogger _logger;
    private readonly PromptTemplateConfig _promptConfig;
    private readonly IPromptTemplate _promptTemplate;

    [DebuggerBrowsable(DebuggerBrowsableState.Never)]
    private string DebuggerDisplay => string.IsNullOrWhiteSpace(this.Description) ? this.Name : $"{this.Name} ({this.Description})";

    /// <summary>The measurement tag name for the model used.</summary>
    private const string MeasurementModelTagName = "semantic_kernel.function.model_id";

    /// <summary><see cref="Counter{T}"/> to record function invocation prompt token usage.</summary>
    private static readonly Histogram<int> s_invocationTokenUsagePrompt = s_meter.CreateHistogram<int>(
        name: "semantic_kernel.function.invocation.token_usage.prompt",
        unit: "{token}",
        description: "Measures the prompt token usage");

    /// <summary><see cref="Counter{T}"/> to record function invocation completion token usage.</summary>
    private static readonly Histogram<int> s_invocationTokenUsageCompletion = s_meter.CreateHistogram<int>(
        name: "semantic_kernel.function.invocation.token_usage.completion",
        unit: "{token}",
        description: "Measures the completion token usage");

    /// <summary>Add default values to the arguments if an argument is not defined</summary>
    private void AddDefaultValues(KernelArguments arguments)
    {
        foreach (var parameter in this._promptConfig.InputVariables)
        {
            if (!arguments.ContainsName(parameter.Name) && parameter.Default != null)
            {
                arguments[parameter.Name] = parameter.Default;
            }
        }
    }

    private async Task<(IAIService, PromptExecutionSettings?, string, PromptRenderedEventArgs?)> RenderPromptAsync(Kernel kernel, KernelArguments arguments, CancellationToken cancellationToken)
    {
        var serviceSelector = kernel.ServiceSelector;
        IAIService? aiService;

        // Try to use IChatCompletionService.
        if (serviceSelector.TrySelectAIService<IChatCompletionService>(
            kernel, this, arguments,
            out IChatCompletionService? chatService, out PromptExecutionSettings? executionSettings))
        {
            aiService = chatService;
        }
        else
        {
            // If IChatCompletionService isn't available, try to fallback to ITextGenerationService,
            // throwing if it's not available.
            (aiService, executionSettings) = serviceSelector.SelectAIService<ITextGenerationService>(kernel, this, arguments);
        }

        Verify.NotNull(aiService);

        kernel.OnPromptRendering(this, arguments);

        var renderedPrompt = await this._promptTemplate.RenderAsync(kernel, arguments, cancellationToken).ConfigureAwait(false);

        if (this._logger.IsEnabled(LogLevel.Trace))
        {
            this._logger.LogTrace("Rendered prompt: {Prompt}", renderedPrompt);
        }

        var renderedEventArgs = kernel.OnPromptRendered(this, arguments, renderedPrompt);

        if (renderedEventArgs is not null &&
            renderedEventArgs.Cancel is false &&
            renderedEventArgs.RenderedPrompt != renderedPrompt)
        {
            renderedPrompt = renderedEventArgs.RenderedPrompt;

            if (this._logger.IsEnabled(LogLevel.Trace))
            {
                this._logger.LogTrace("Rendered prompt changed by handler: {Prompt}", renderedEventArgs.RenderedPrompt);
            }
        }

        return (aiService, executionSettings, renderedPrompt, renderedEventArgs);
    }

    /// <summary>Create a random, valid function name.</summary>
    private static string CreateRandomFunctionName() => $"func{Guid.NewGuid():N}";

    /// <summary>
    /// Captures usage details, including token information.
    /// </summary>
    private void CaptureUsageDetails(string? modelId, IReadOnlyDictionary<string, object?>? metadata, ILogger logger)
    {
        if (!logger.IsEnabled(LogLevel.Information) &&
            !s_invocationTokenUsageCompletion.Enabled &&
            !s_invocationTokenUsagePrompt.Enabled)
        {
            // Bail early to avoid unnecessary work.
            return;
        }

        if (string.IsNullOrWhiteSpace(modelId))
        {
            logger.LogInformation("No model ID provided to capture usage details.");
            return;
        }

        if (metadata is null)
        {
            logger.LogInformation("No metadata provided to capture usage details.");
            return;
        }

        if (!metadata.TryGetValue("Usage", out object? usageObject) || usageObject is null)
        {
            logger.LogInformation("No usage details provided to capture usage details.");
            return;
        }

        JsonElement jsonObject;
        try
        {
            jsonObject = JsonSerializer.SerializeToElement(usageObject);
        }
        catch (Exception ex) when (ex is NotSupportedException)
        {
            logger.LogWarning(ex, "Error while parsing usage details from model result.");
            return;
        }

        if (jsonObject.TryGetProperty("PromptTokens", out var promptTokensJson) &&
            promptTokensJson.TryGetInt32(out int promptTokens) &&
            jsonObject.TryGetProperty("CompletionTokens", out var completionTokensJson) &&
            completionTokensJson.TryGetInt32(out int completionTokens))
        {
            logger.LogInformation(
                "Prompt tokens: {PromptTokens}. Completion tokens: {CompletionTokens}.",
                promptTokens, completionTokens);

            TagList tags = new() {
                { MeasurementFunctionTagName, this.Name },
                { MeasurementModelTagName, modelId }
            };

            s_invocationTokenUsagePrompt.Record(promptTokens, in tags);
            s_invocationTokenUsageCompletion.Record(completionTokens, in tags);
        }
        else
        {
            logger.LogWarning("Unable to get token details from model result.");
        }
    }

    #endregion
}
