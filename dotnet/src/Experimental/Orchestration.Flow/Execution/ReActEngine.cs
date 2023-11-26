﻿// Copyright (c) Microsoft. All rights reserved.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel.AI.ChatCompletion;
using Microsoft.SemanticKernel.Orchestration;

namespace Microsoft.SemanticKernel.Experimental.Orchestration.Execution;

/// <summary>
/// Chat ReAct Engine
/// </summary>
internal sealed class ReActEngine
{
    /// <summary>
    /// The logger
    /// </summary>
    private readonly ILogger _logger;

    /// <summary>
    /// Re-Act function for flow execution
    /// </summary>
    private readonly KernelFunction _reActFunction;

    /// <summary>
    /// The flow planner config
    /// </summary>
    private readonly FlowOrchestratorConfig _config;

    /// <summary>
    /// The goal to use when creating semantic functions that are restricted from flow creation
    /// </summary>
    private const string RestrictedPluginName = "ReActEngine_Excluded";

    /// <summary>
    /// The Action tag
    /// </summary>
    private const string Action = "[ACTION]";

    /// <summary>
    /// The Thought tag
    /// </summary>
    private const string Thought = "[THOUGHT]";

    /// <summary>
    /// The Observation tag
    /// </summary>
    private const string Observation = "[OBSERVATION]";

    /// <summary>
    /// The prefix used for the scratch pad
    /// </summary>
    private const string ScratchPadPrefix =
        "This was my previous work (but they haven't seen any of it! They only see what I return as final answer):";

    /// <summary>
    /// The regex for parsing the action response
    /// </summary>
    private static readonly Regex s_actionRegex =
        new(@"(?<=\[ACTION\])[^{}]*(\{.*?\})(?=\n\[)", RegexOptions.Singleline);

    /// <summary>
    /// The regex for parsing the final action response
    /// </summary>
    private static readonly Regex s_finalActionRegex =
        new(@"\[FINAL.+\][^{}]*({(?:[^{}]*{[^{}]*})*[^{}]*})", RegexOptions.Singleline);

    /// <summary>
    /// The regex for parsing the thought response
    /// </summary>
    private static readonly Regex s_thoughtRegex =
        new(@"(\[THOUGHT\])?(?<thought>.+?)(?=\[ACTION\]|$)", RegexOptions.Singleline);

    /// <summary>
    /// The regex for parsing the final answer response
    /// </summary>
    private static readonly Regex s_finalAnswerRegex =
        new(@"\[FINAL.+\](?<final_answer>.+)", RegexOptions.Singleline);

    internal ReActEngine(Kernel systemKernel, ILogger logger, FlowOrchestratorConfig config)
    {
        this._logger = logger;

        this._config = config;
        this._config.ExcludedPlugins.Add(RestrictedPluginName);

        var modelId = config.AIRequestSettings?.ModelId;
        var promptConfig = config.ReActPromptTemplateConfig;
        if (promptConfig is null)
        {
            promptConfig = new PromptTemplateConfig();

            string promptConfigString = EmbeddedResource.Read("Plugins.ReActEngine.config.json")!;
            if (!string.IsNullOrEmpty(modelId))
            {
                var modelConfigString = EmbeddedResource.Read($"Plugins.ReActEngine.{modelId}.config.json", false);
                promptConfigString = string.IsNullOrEmpty(modelConfigString) ? promptConfigString : modelConfigString!;
            }

            if (!string.IsNullOrEmpty(promptConfigString))
            {
                promptConfig = PromptTemplateConfig.FromJson(promptConfigString);
            }
            else
            {
                promptConfig.SetMaxTokens(config.MaxTokens);
            }
        }

        var promptTemplate = config.ReActPromptTemplate;
        if (string.IsNullOrEmpty(promptTemplate))
        {
            promptTemplate = EmbeddedResource.Read("Plugins.ReActEngine.skprompt.txt")!;

            if (!string.IsNullOrEmpty(promptTemplate))
            {
                var modelPromptTemplate = EmbeddedResource.Read($"Plugins.ReActEngine.{modelId}.skprompt.txt", false);
                promptTemplate = string.IsNullOrEmpty(modelPromptTemplate) ? promptTemplate : modelPromptTemplate!;
            }
        }

        this._reActFunction = this.ImportSemanticFunction(systemKernel, "ReActFunction", promptTemplate!, promptConfig);
    }

    internal async Task<ReActStep?> GetNextStepAsync(Kernel kernel, ContextVariables variables, string question, List<ReActStep> previousSteps)
    {
        variables.Set("question", question);
        var scratchPad = this.CreateScratchPad(previousSteps);
        variables.Set("agentScratchPad", scratchPad);

        var availableFunctions = this.GetAvailableFunctions(kernel).ToArray();
        if (availableFunctions.Length == 1)
        {
            var firstActionFunction = availableFunctions.First();
            if (firstActionFunction.Parameters.Count == 0)
            {
                var action = $"{firstActionFunction.PluginName}.{firstActionFunction.Name}";
                this._logger?.LogDebug($"Auto selecting {Action} as it is the only function available and it has no parameters.", action);
                return new ReActStep
                {
                    Action = action
                };
            }
        }

        var functionDesc = this.GetFunctionDescriptions(availableFunctions);
        variables.Set("functionDescriptions", functionDesc);

        this._logger?.LogInformation("question: {Question}", question);
        this._logger?.LogInformation("functionDescriptions: {FunctionDescriptions}", functionDesc);
        this._logger?.LogInformation("Scratchpad: {ScratchPad}", scratchPad);

        var llmResponse = await this._reActFunction.InvokeAsync(kernel, variables).ConfigureAwait(false);

        string llmResponseText = llmResponse.GetValue<string>()!.Trim();
        this._logger?.LogDebug("Response : {ActionText}", llmResponseText);

        var actionStep = this.ParseResult(llmResponseText);

        if (!string.IsNullOrEmpty(actionStep.Action) || previousSteps.Count == 0)
        {
            return actionStep;
        }

        actionStep.Observation = "Failed to parse valid action step, missing action or final answer.";
        this._logger?.LogWarning("Failed to parse valid action step from llm response={LLMResponseText}", llmResponseText);
        this._logger?.LogWarning("Scratchpad={ScratchPad}", scratchPad);
        return actionStep;
    }

    internal async Task<string> InvokeActionAsync(ReActStep actionStep, string chatInput, ChatHistory chatHistory, Kernel kernel, ContextVariables contextVariables)
    {
        var variables = actionStep.ActionVariables ?? new Dictionary<string, string>();

        variables[Constants.ActionVariableNames.ChatInput] = chatInput;
        variables[Constants.ActionVariableNames.ChatHistory] = ChatHistorySerializer.Serialize(chatHistory);
        this._logger?.LogInformation("Action: {Action}({ActionVariables})", actionStep.Action, JsonSerializer.Serialize(variables));

        var availableFunctions = this.GetAvailableFunctions(kernel);
        var targetFunction = availableFunctions.FirstOrDefault(f => ToFullyQualifiedName(f) == actionStep.Action);
        if (targetFunction is null)
        {
            throw new MissingMethodException($"The function '{actionStep.Action}' was not found.");
        }

        var function = kernel.Plugins.GetFunction(targetFunction.PluginName, targetFunction.Name);
        var functionView = function.GetMetadata();

        var actionContextVariables = this.CreateActionContextVariables(variables, kernel, contextVariables);

        foreach (var parameter in functionView.Parameters)
        {
            if (!actionContextVariables.ContainsKey(parameter.Name))
            {
                actionContextVariables.Set(parameter.Name, parameter.DefaultValue ?? string.Empty);
            }
        }

        try
        {
            var result = await function.InvokeAsync(kernel, actionContextVariables).ConfigureAwait(false);

            foreach (var variable in actionContextVariables)
            {
                contextVariables.Set(variable.Key, variable.Value);
            }

            this._logger?.LogDebug("Invoked {FunctionName}. Result: {Result}", targetFunction.Name, result.GetValue<string>());

            return result.GetValue<string>() ?? string.Empty;
        }
        catch (Exception e) when (!e.IsCriticalException())
        {
            this._logger?.LogError(e, "Something went wrong in action step: {0}.{1}. Error: {2}", targetFunction.PluginName, targetFunction.Name, e.Message);
            return $"Something went wrong in action step: {targetFunction.PluginName}.{targetFunction.Name}. Error: {e.Message} {e.InnerException?.Message}";
        }
    }

    private ContextVariables CreateActionContextVariables(Dictionary<string, string> actionVariables, Kernel kernel, ContextVariables contextVariables)
    {
        var actionContext = contextVariables.Clone();

        foreach (var kvp in actionVariables)
        {
            actionContext.Set(kvp.Key, kvp.Value);
        }

        return actionContext;
    }

    private KernelFunction ImportSemanticFunction(Kernel kernel, string functionName, string promptTemplate, PromptTemplateConfig config)
    {
        var factory = new KernelPromptTemplateFactory(kernel.GetService<ILoggerFactory>());
        var template = factory.Create(promptTemplate, config);

        var plugin = new KernelPlugin(RestrictedPluginName);

        kernel.Plugins.Add(plugin);

        return plugin.AddFunctionFromPrompt(template, config, functionName);
    }

    private string CreateScratchPad(List<ReActStep> stepsTaken)
    {
        if (stepsTaken.Count == 0)
        {
            return string.Empty;
        }

        var scratchPadLines = new List<string>
        {
            // Add the original first thought
            ScratchPadPrefix,
            $"{Thought} {stepsTaken[0].Thought}"
        };

        // Keep track of where to insert the next step
        var insertPoint = scratchPadLines.Count;

        // Keep the most recent steps in the scratch pad.
        for (var i = stepsTaken.Count - 1; i >= 0; i--)
        {
            if (scratchPadLines.Count / 4.0 > (this._config.MaxTokens * 0.75))
            {
                this._logger.LogDebug("Scratchpad is too long, truncating. Skipping {CountSkipped} steps.", i + 1);
                break;
            }

            var s = stepsTaken[i];

            if (!string.IsNullOrEmpty(s.Observation))
            {
                scratchPadLines.Insert(insertPoint, $"{Observation} \n{s.Observation}");
            }

            if (!string.IsNullOrEmpty(s.Action))
            {
                // ignore the built-in context variables
                var variablesToPrint = s.ActionVariables?.Where(v => !Constants.ActionVariableNames.All.Contains(v.Key)).ToDictionary(_ => _.Key, _ => _.Value);
                scratchPadLines.Insert(insertPoint, $"{Action} {{\"action\": \"{s.Action}\",\"action_variables\": {JsonSerializer.Serialize(variablesToPrint)}}}");
            }

            if (i != 0)
            {
                scratchPadLines.Insert(insertPoint, $"{Thought} {s.Thought}");
            }
        }

        return string.Join("\n", scratchPadLines).Trim();
    }

    private ReActStep ParseResult(string input)
    {
        var result = new ReActStep
        {
            OriginalResponse = input
        };

        // Extract final answer
        Match finalAnswerMatch = s_finalAnswerRegex.Match(input);

        if (finalAnswerMatch.Success)
        {
            result.FinalAnswer = finalAnswerMatch.Groups[1].Value.Trim();
        }

        // Extract thought
        Match thoughtMatch = s_thoughtRegex.Match(input);

        if (thoughtMatch.Success)
        {
            result.Thought = thoughtMatch.Value.Trim();
        }
        else if (!input.Contains(Action))
        {
            result.Thought = input;
        }
        else
        {
            throw new InvalidOperationException("Unexpected input format");
        }

        result.Thought = result.Thought.Replace(Thought, string.Empty).Trim();

        // Extract action
        string actionStepJson = input;
        Match actionMatch = s_actionRegex.Match(input + "\n[");
        if (actionMatch.Success)
        {
            actionStepJson = actionMatch.Groups[1].Value.Trim();
        }
        else
        {
            Match finalActionMatch = s_finalActionRegex.Match(input);
            if (finalActionMatch.Success)
            {
                actionStepJson = finalActionMatch.Groups[1].Value.Trim();
            }
        }

        try
        {
            var reActStep = JsonSerializer.Deserialize<ReActStep>(actionStepJson);
            if (reActStep is null)
            {
                result.Observation = $"Action step parsing error, empty JSON: {actionStepJson}";
            }
            else
            {
                result.Action = reActStep.Action;
                result.ActionVariables = reActStep.ActionVariables;
            }
        }
        catch (JsonException)
        {
            result.Observation = $"Action step parsing error, invalid JSON: {actionStepJson}";
        }

        if (string.IsNullOrEmpty(result.Thought) && string.IsNullOrEmpty(result.Action))
        {
            result.Observation = "Action step error, no thought or action found. Please give a valid thought and/or action.";
        }

        return result;
    }

    private string GetFunctionDescriptions(KernelFunctionMetadata[] functions)
    {
        return string.Join("\n", functions.Select(ToManualString));
    }

    private IEnumerable<KernelFunctionMetadata> GetAvailableFunctions(Kernel kernel)
    {
        var functionViews = kernel.Plugins.GetFunctionsMetadata();

        var excludedPlugins = this._config.ExcludedPlugins ?? new HashSet<string>();
        var excludedFunctions = this._config.ExcludedFunctions ?? new HashSet<string>();

        var availableFunctions =
            functionViews
                .Where(s => !excludedPlugins.Contains(s.PluginName!) && !excludedFunctions.Contains(s.Name))
                .OrderBy(x => x.PluginName)
                .ThenBy(x => x.Name);

        return this._config.EnableAutoTermination
            ? availableFunctions.Append(GetStopAndPromptUserFunction())
            : availableFunctions;
    }

    private static KernelFunctionMetadata GetStopAndPromptUserFunction()
    {
        KernelParameterMetadata promptParameter = new(Constants.StopAndPromptParameterName)
        {
            Description = "The message to be shown to the user.",
            ParameterType = typeof(string),
            Schema = KernelJsonSchema.Parse("{\"type\":\"string\"}"),
        };

        return new KernelFunctionMetadata(Constants.StopAndPromptFunctionName)
        {
            PluginName = "_REACT_ENGINE_",
            Description = "Terminate the session, only used when previous attempts failed with FATAL error and need notify user",
            Parameters = new[] { promptParameter }
        };
    }

    private static string ToManualString(KernelFunctionMetadata function)
    {
        var inputs = string.Join("\n", function.Parameters.Select(parameter =>
        {
            var defaultValueString = string.IsNullOrEmpty(parameter.DefaultValue) ? string.Empty : $"(default='{parameter.DefaultValue}')";
            return $"  - {parameter.Name}: {parameter.Description} {defaultValueString}";
        }));

        var functionDescription = function.Description.Trim();

        if (string.IsNullOrEmpty(inputs))
        {
            return $"{ToFullyQualifiedName(function)}: {functionDescription}\n";
        }

        return $"{ToFullyQualifiedName(function)}: {functionDescription}\n{inputs}\n";
    }

    private static string ToFullyQualifiedName(KernelFunctionMetadata function)
    {
        return $"{function.PluginName}.{function.Name}";
    }
}
