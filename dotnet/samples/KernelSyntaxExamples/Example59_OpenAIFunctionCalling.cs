// Copyright (c) Microsoft. All rights reserved.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.AI.ChatCompletion;
using Microsoft.SemanticKernel.Connectors.AI.OpenAI;
using Microsoft.SemanticKernel.Connectors.AI.OpenAI.AzureSdk;
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

        string[] questions = new[]
        {
            "Write a short poem about dogs, incorporating today's exact date.",
            "Tell me something interesting that happened on this date in years past.",
        };

        Func<IKernel, IEnumerable<string>, Task>[] approaches = new[]
        {
            AutomaticWithMessagesAsync,
            AutomaticWithStreamingMessagesAsync,
            ManualWithMessagesAsync,
            // ManualWithStreamingMessagesAsync,
        };

        foreach (var func in approaches)
        {
            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine(func.Method.Name);
            Console.WriteLine("----------------------------------------------------------------------");
            Console.ResetColor();
            await func(kernel, questions);
            Console.WriteLine();
        }
    }

    // --- Automatic Function Handling via IChatCompletionWithFunctions ---

    private static async Task AutomaticWithMessagesAsync(IKernel kernel, IEnumerable<string> questions)
    {
        var chatCompletion = kernel.GetService<IChatCompletionWithFunctions>("chat");
        var chatHistory = chatCompletion.CreateNewChat();

        foreach (string question in questions)
        {
            chatHistory.AddUserMessage(question);

            chatHistory.AddAssistantMessage(await chatCompletion.GenerateMessageAsync(chatHistory, kernel.CreateNewContext()));

            Console.WriteLine(chatHistory[^1].Content);
        }
    }

    private static async Task AutomaticWithStreamingMessagesAsync(IKernel kernel, IEnumerable<string> questions)
    {
        var chatCompletion = kernel.GetService<IChatCompletionWithFunctions>("chat");
        var chatHistory = chatCompletion.CreateNewChat();

        foreach (string question in questions)
        {
            chatHistory.AddUserMessage(question);

            StringBuilder builder = new();
            await foreach (string message in chatCompletion.GenerateMessageStreamAsync(chatHistory, kernel.CreateNewContext()))
            {
                builder.AppendLine(message);
                Console.Write(message);
            }
            Console.WriteLine();

            chatHistory.AddAssistantMessage(builder.ToString());
        }
    }

    // --- Manual Function Handling directly against IChatCompletion with OpenAIRequestSettings and OpenAIFunctionResponse ---

    private static async Task ManualWithMessagesAsync(IKernel kernel, IEnumerable<string> questions)
    {
        var chatCompletion = kernel.GetService<IChatCompletion>("chat");
        var chatHistory = chatCompletion.CreateNewChat();

        foreach (string question in questions)
        {
            chatHistory.AddUserMessage(question);

            while (true)
            {
                OpenAIRequestSettings settings = new();
                settings.FunctionCall = OpenAIRequestSettings.FunctionCallAuto;
                settings.Functions = kernel.Functions.GetFunctionViews().Select(functionView => functionView.ToOpenAIFunction()).ToList();

                IChatResult chatResult = (await chatCompletion.GetChatCompletionsAsync(chatHistory, settings))[0];

                OpenAIFunctionResponse? functionResponse = chatResult.GetFunctionResponse();
                if (functionResponse is null ||
                    !kernel.Functions.TryGetFunction(functionResponse.PluginName, functionResponse.FunctionName, out ISKFunction? function))
                {
                    chatHistory.AddAssistantMessage((await chatResult.GetChatMessageAsync()).Content);
                    break;
                }

                ContextVariables context = new();
                foreach (KeyValuePair<string, object> parameter in functionResponse.Parameters)
                {
                    context.Set(parameter.Key, parameter.Value.ToString());
                }

                KernelResult functionResult = await kernel.RunAsync(function, context);

                ChatMessage resultMessage = new(AuthorRole.Function, functionResult.GetValue<object>()?.ToString() ?? "");
                resultMessage.AdditionalContext = new Dictionary<string, string>(1) { { "Name", functionResponse.FunctionName } };
                chatHistory.Messages.Add(resultMessage);
            }

            Console.WriteLine(chatHistory[^1].Content);
        }
    }

    private static async Task ManualWithStreamingMessagesAsync(IKernel kernel, IEnumerable<string> questions)
    {
        // This can't currently be implemented due to https://github.com/microsoft/semantic-kernel/issues/3198.
        throw new NotSupportedException();
    }

    // TODO: https://github.com/microsoft/semantic-kernel/issues/2687
    private sealed class ChatMessage : ChatMessageBase
    {
        public ChatMessage(AuthorRole authorRole, string content) : base(authorRole, content) { }
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

        return kernel;
    }
}
