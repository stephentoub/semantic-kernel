﻿// Copyright (c) Microsoft. All rights reserved.

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.AI;
using Microsoft.SemanticKernel.AI.TextCompletion;
using Microsoft.SemanticKernel.Connectors.AI.OpenAI;
using Microsoft.SemanticKernel.Orchestration;
using SemanticKernel.IntegrationTests.Fakes;
using Xunit;
using Xunit.Abstractions;

namespace SemanticKernel.IntegrationTests.Extensions;

public sealed class KernelFunctionExtensionsTests : IDisposable
{
    public KernelFunctionExtensionsTests(ITestOutputHelper output)
    {
        this._logger = new RedirectOutput(output);
    }

    [Fact]
    public async Task ItSupportsFunctionCallsAsync()
    {
        var builder = new KernelBuilder()
                .WithAIService<ITextCompletion>(null, new RedirectTextCompletion())
                .WithLoggerFactory(this._logger);
        Kernel target = builder.Build();

        var emailFunctions = target.ImportPluginFromObject<EmailPluginFake>();

        var prompt = $"Hey {{{{{nameof(EmailPluginFake)}.GetEmailAddress}}}}";

        // Act
        FunctionResult actual = await target.InvokePromptAsync(prompt, new OpenAIPromptExecutionSettings() { MaxTokens = 150 });

        // Assert
        Assert.Equal("Hey johndoe1234@example.com", actual.GetValue<string>());
    }

    [Fact]
    public async Task ItSupportsFunctionCallsWithInputAsync()
    {
        var builder = new KernelBuilder()
                .WithAIService<ITextCompletion>(null, new RedirectTextCompletion())
                .WithLoggerFactory(this._logger);
        Kernel target = builder.Build();

        var emailFunctions = target.ImportPluginFromObject<EmailPluginFake>();

        var prompt = $"Hey {{{{{nameof(EmailPluginFake)}.GetEmailAddress \"a person\"}}}}";

        // Act
        FunctionResult actual = await target.InvokePromptAsync(prompt, new OpenAIPromptExecutionSettings() { MaxTokens = 150 });

        // Assert
        Assert.Equal("Hey a person@example.com", actual.GetValue<string>());
    }

    private readonly RedirectOutput _logger;

    public void Dispose()
    {
        this._logger.Dispose();
    }

    private sealed class RedirectTextCompletion : ITextCompletion
    {
        public string? ModelId => null;

        public IReadOnlyDictionary<string, string> Attributes => new Dictionary<string, string>();

        Task<IReadOnlyList<ITextResult>> ITextCompletion.GetCompletionsAsync(string text, PromptExecutionSettings? requestSettings, CancellationToken cancellationToken)
        {
            return Task.FromResult<IReadOnlyList<ITextResult>>(new List<ITextResult> { new RedirectTextCompletionResult(text) });
        }

        public IAsyncEnumerable<T> GetStreamingContentAsync<T>(string prompt, PromptExecutionSettings? requestSettings = null, CancellationToken cancellationToken = default)
        {
            throw new NotImplementedException();
        }
    }

    internal sealed class RedirectTextCompletionResult : ITextResult
    {
        private readonly string _completion;

        public RedirectTextCompletionResult(string completion)
        {
            this._completion = completion;
        }

        public ModelResult ModelResult => new(this._completion);

        public Task<string> GetCompletionAsync(CancellationToken cancellationToken = default)
        {
            return Task.FromResult(this._completion);
        }
    }
}
