﻿// Copyright (c) Microsoft. All rights reserved.

using System.ComponentModel;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel.Experimental.Orchestration;

namespace SemanticKernel.Experimental.Orchestration.Flow.IntegrationTests;

public sealed class CollectEmailPlugin
{
    private const string Goal = "Collect email from user";

    private const string EmailRegex = @"^([\w\.\-]+)@([\w\-]+)((\.(\w){2,3})+)$";

    private const string SystemPrompt =
        $@"I am AI assistant and will only answer questions related to collect email.
The email should conform the regex: {EmailRegex}

If I cannot answer, say that I don't know.
Do not expose the regex unless asked.
";

    private readonly IChatCompletionService _chat;

    public CollectEmailPlugin(Kernel kernel)
    {
        this._chat = kernel.GetRequiredService<IChatCompletionService>();
    }

    [KernelFunction("ConfigureEmailAddress")]
    [Description("Useful to assist in configuration of email address, must be called after email provided")]
    public async Task<string> CollectEmailAsync(
        [Description("The email address provided by the user, pass no matter what the value is")]
        // ReSharper disable once InconsistentNaming
#pragma warning disable CA1707 // Identifiers should not contain underscores
        string email_address,
#pragma warning restore CA1707 // Identifiers should not contain underscores
        KernelArguments arguments)
    {
        var chat = new ChatHistory(SystemPrompt);
        chat.AddUserMessage(Goal);

        ChatHistory? chatHistory = arguments.GetChatHistory();
        if (chatHistory?.Count > 0)
        {
            chat.AddRange(chatHistory);
        }

        if (!string.IsNullOrEmpty(email_address) && IsValidEmail(email_address))
        {
            return "Thanks for providing the info, the following email would be used in subsequent steps: " + email_address;
        }

        // invalid email, prompt user to provide a valid email
        arguments["email_address"] = string.Empty;
        arguments.PromptInput();

        var response = await this._chat.GetChatMessageContentAsync(chat).ConfigureAwait(false);

        return response.Content ?? string.Empty;
    }

    private static bool IsValidEmail(string email)
    {
        // check using regex
        var regex = new Regex(EmailRegex);
        return regex.IsMatch(email);
    }
}
