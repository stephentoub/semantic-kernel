// Copyright (c) Microsoft. All rights reserved.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.AI.ChatCompletion;
using Microsoft.SemanticKernel.Connectors.AI.OpenAI;
using Microsoft.SemanticKernel.Connectors.AI.OpenAI.AzureSdk;
using Microsoft.SemanticKernel.Functions.OpenAPI.Extensions;
using Microsoft.SemanticKernel.Orchestration;
using Microsoft.SemanticKernel.Plugins.Core;
using RepoUtils;

/**
 * This example shows how to use OpenAI's function calling capability via the chat completions interface.
 * For more information, see https://platform.openai.com/docs/guides/gpt/function-calling.
 */
// ReSharper disable once InconsistentNaming
public static class Example59_OpenAIFunctionCalling
{
    public static async Task RunAsync()
    {
        IKernel kernel = await InitializeKernelAsync();
        var chatHistory = kernel.GetService<IChatCompletion>().CreateNewChat();

        string[] questions = new[]
        {
            "What day is today?",
            "What computer tablets are available for under $200?",
        };

        foreach (string question in questions)
        {
            chatHistory.AddUserMessage(question);
            Console.WriteLine(chatHistory[^1].Content);

            await GetChatCompletionsWithFunctionsAsync(kernel, chatHistory);
            Console.WriteLine(chatHistory[^1].Content);
        }
    }

    private static async Task<IKernel> InitializeKernelAsync()
    {
        // Create kernel with chat completions service
        IKernel kernel = new KernelBuilder()
            .WithLoggerFactory(ConsoleLogger.LoggerFactory)
            .WithOpenAIChatCompletionService(TestConfiguration.OpenAI.ChatModelId, TestConfiguration.OpenAI.ApiKey, serviceId: "chat")
            //.WithAzureChatCompletionService(TestConfiguration.AzureOpenAI.ChatDeploymentName, TestConfiguration.AzureOpenAI.Endpoint, TestConfiguration.AzureOpenAI.ApiKey, serviceId: "chat")
            .Build();

        // Load functions to kernel
        kernel.ImportFunctions(new TimePlugin(), "TimePlugin");
        await kernel.ImportPluginFunctionsAsync("KlarnaShoppingPlugin", new Uri("https://www.klarna.com/.well-known/ai-plugin.json"), new OpenApiFunctionExecutionParameters());

        return kernel;
    }

    // TODO: https://github.com/microsoft/semantic-kernel/issues/2932
    public static async Task GetChatCompletionsWithFunctionsAsync(
        IKernel kernel,
        ChatHistory chat,
        OpenAIRequestSettings? requestSettings = null,
        CancellationToken cancellationToken = default)
    {
        OpenAIRequestSettings settings = requestSettings ?? new();
        settings.FunctionCall = OpenAIRequestSettings.FunctionCallAuto;
        settings.Functions = kernel.Functions.GetFunctionViews().Select(functionView => functionView.ToOpenAIFunction()).ToList();

        while (true)
        {
            IChatResult chatResult = (await kernel.GetService<IChatCompletion>().GetChatCompletionsAsync(chat, settings, cancellationToken).ConfigureAwait(false))[0];

            OpenAIFunctionResponse? functionResponse = chatResult.GetFunctionResponse();
            if (functionResponse is null ||
                !kernel.Functions.TryGetFunction(functionResponse.PluginName, functionResponse.FunctionName, out ISKFunction? function))
            {
                chat.AddAssistantMessage((await chatResult.GetChatMessageAsync(cancellationToken).ConfigureAwait(false)).Content);
                return;
            }

            ContextVariables context = new();
            foreach (KeyValuePair<string, object> parameter in functionResponse.Parameters)
            {
                context.Set(parameter.Key, parameter.Value.ToString());
            }

            KernelResult functionResult = await kernel.RunAsync(function, context, cancellationToken).ConfigureAwait(false);

            ChatMessage resultMessage = new(AuthorRole.Function, functionResult.GetValue<object>()?.ToString() ?? "");
            resultMessage.AdditionalContext = new Dictionary<string, string>(1) { { "Name", functionResponse.FunctionName } };
            chat.Messages.Add(resultMessage);
        }
    }

    // TODO: https://github.com/microsoft/semantic-kernel/issues/2687
    private sealed class ChatMessage : ChatMessageBase
    {
        public ChatMessage(AuthorRole authorRole, string content) : base(authorRole, content) { }
    }
}
