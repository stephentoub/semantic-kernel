﻿// Copyright (c) Microsoft. All rights reserved.

using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.SemanticKernel.AI.ChatCompletion;
using Microsoft.SemanticKernel.AI.TextCompletion;
using Microsoft.SemanticKernel.Connectors.AI.OpenAI.ChatCompletion;
using Microsoft.SemanticKernel.Experimental.Assistants.Extensions;
using Microsoft.SemanticKernel.Experimental.Assistants.Models;

namespace Microsoft.SemanticKernel.Experimental.Assistants.Internal;

/// <summary>
/// Represents an assistant that can call the model and use tools.
/// </summary>
internal sealed class Assistant : IAssistant
{
    /// <inheritdoc/>
    public string Id => this._model.Id;

    /// <inheritdoc/>
    public Kernel Kernel { get; }

    /// <inheritdoc/>
    public KernelPluginCollection Plugins => this.Kernel.Plugins;

    /// <inheritdoc/>
#pragma warning disable CA1720 // Identifier contains type name - We don't control the schema
#pragma warning disable CA1716 // Identifiers should not match keywords
    public string Object => this._model.Object;
#pragma warning restore CA1720 // Identifier contains type name - We don't control the schema
#pragma warning restore CA1716 // Identifiers should not match keywords

    /// <inheritdoc/>
    public long CreatedAt => this._model.CreatedAt;

    /// <inheritdoc/>
    public string? Name => this._model.Name;

    /// <inheritdoc/>
    public string? Description => this._model.Description;

    /// <inheritdoc/>
    public string Model => this._model.Model;

    /// <inheritdoc/>
    public string Instructions => this._model.Instructions;

    private readonly OpenAIRestContext _restContext;
    private readonly AssistantModel _model;

    /// <summary>
    /// Create a new assistant.
    /// </summary>
    /// <param name="restContext">A context for accessing OpenAI REST endpoint</param>
    /// <param name="chatService">An OpenAI chat service.</param>
    /// <param name="assistantModel">The assistant definition</param>
    /// <param name="plugins">Plugins to initialize as assistant tools</param>
    /// <param name="cancellationToken">A cancellation token</param>
    /// <returns>An initialized <see cref="Assistant"> instance.</see></returns>
    public static async Task<IAssistant> CreateAsync(
        OpenAIRestContext restContext,
        OpenAIChatCompletion chatService,
        AssistantModel assistantModel,
        IEnumerable<IKernelPlugin>? plugins = null,
        CancellationToken cancellationToken = default)
    {
        var resultModel =
            await restContext.CreateAssistantModelAsync(assistantModel, cancellationToken).ConfigureAwait(false) ??
            throw new KernelException("Unexpected failure creating assistant: no result.");

        return new Assistant(resultModel, chatService, restContext, plugins);
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="Assistant"/> class.
    /// </summary>
    internal Assistant(
        AssistantModel model,
        OpenAIChatCompletion chatService,
        OpenAIRestContext restContext,
        IEnumerable<IKernelPlugin>? plugins = null)
    {
        this._model = model;
        this._restContext = restContext;

        var services = new ServiceCollection();
        services.AddSingleton<IChatCompletion>(chatService);
        services.AddSingleton<ITextCompletion>(chatService);
        this.Kernel = new Kernel(services.BuildServiceProvider(), plugins is not null ? new KernelPluginCollection(plugins) : null);
    }

    /// <inheritdoc/>
    public Task<IChatThread> NewThreadAsync(CancellationToken cancellationToken = default)
    {
        return ChatThread.CreateAsync(this._restContext, cancellationToken);
    }

    /// <inheritdoc/>
    public Task<IChatThread> GetThreadAsync(string id, CancellationToken cancellationToken = default)
    {
        return ChatThread.GetAsync(this._restContext, id, cancellationToken);
    }

    /// <summary>
    /// Marshal thread run through <see cref="KernelFunction"/> interface.
    /// </summary>
    /// <param name="input">The user input</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>An assistant response (<see cref="AssistantResponse"/></returns>
    [KernelFunction, Description("Provide input to assistant a response")]
    public async Task<string> AskAsync(
        [Description("The input for the assistant.")]
        string input,
        CancellationToken cancellationToken = default)
    {
        var thread = await this.NewThreadAsync(cancellationToken).ConfigureAwait(false);
        await thread.AddUserMessageAsync(input, cancellationToken).ConfigureAwait(false);
        var message = await thread.InvokeAsync(this, cancellationToken).ConfigureAwait(false);
        var response =
            new AssistantResponse
            {
                ThreadId = thread.Id,
                Response = string.Concat(message.Select(m => m.Content)),
            };

        return JsonSerializer.Serialize(response);
    }
}
