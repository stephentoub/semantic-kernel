﻿// Copyright (c) Microsoft. All rights reserved.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Microsoft.SemanticKernel.Experimental.Orchestration.Execution;

namespace Microsoft.SemanticKernel.Experimental.Orchestration;

/// <summary>
/// Step within a <see cref="Flow"/> which defines the step goal, available plugins, required and provided variables.
/// </summary>
public class FlowStep
{
    private readonly List<string> _requires = new();

    private readonly List<string> _provides = new();

    private readonly List<string> _passthrough = new();

    private Dictionary<string, Type?> _pluginTypes = new();

    private Func<Kernel, Dictionary<object, string?>, IEnumerable<object>>? _pluginsFactory;

    /// <summary>
    /// Initializes a new instance of the <see cref="FlowStep"/> class.
    /// </summary>
    /// <param name="goal">The goal of step</param>
    /// <param name="pluginsFactory">The factory to get plugins</param>
    public FlowStep(string goal, Func<Kernel, Dictionary<object, string?>, IEnumerable<object>>? pluginsFactory = null)
    {
        this.Goal = goal;
        this._pluginsFactory = pluginsFactory;
    }

    /// <summary>
    /// Goal of the step
    /// </summary>
    public string Goal { get; set; }

    /// <summary>
    /// <see cref="CompletionType"/> of the step
    /// </summary>
    public CompletionType CompletionType { get; set; } = CompletionType.Once;

    /// <summary>
    /// If the CompletionType is CompletionType.ZeroOrMore, this message will be used to ask the user if they want to execute the current step or skip it.
    /// </summary>
    public string? StartingMessage { get; set; }

    /// <summary>
    /// If the CompletionType is CompletionType.AtLeastOnce or CompletionType.ZeroOrMore, this message will be used to ask the user if they want to try the step again.
    /// </summary>
    public string? TransitionMessage { get; set; } = "Did you want to try the previous step again?";

    /// <summary>
    /// Parameters required for executing the step
    /// </summary>
    public virtual IEnumerable<string> Requires => this._requires;

    /// <summary>
    /// Variables to be provided by the step
    /// </summary>
    public IEnumerable<string> Provides => this._provides;

    /// <summary>
    /// Variables to be passed through on iterations of the step
    /// </summary>
    public IEnumerable<string> Passthrough => this._passthrough;

    /// <summary>
    /// Gets or sets the plugin available for the current step
    /// </summary>
    public List<string>? Plugins
    {
        get => this._pluginTypes.Keys.ToList();
        set
        {
            Dictionary<string, Type?> plugins = GetPluginTypes(value);

            this._pluginTypes = plugins;
            this._pluginsFactory = (kernel, globalPlugins) => this.GetPlugins(globalPlugins, kernel);
        }
    }

    private List<object> GetPlugins(Dictionary<object, string?> globalPlugins, Kernel kernel)
    {
        return this._pluginTypes.Select(kvp =>
        {
            var pluginName = kvp.Key;
            var globalPlugin = globalPlugins.FirstOrDefault(_ => _.Key.GetType().Name.Contains(pluginName)).Key;
            if (globalPlugin != null)
            {
                return globalPlugin;
            }

            var type = kvp.Value;
            if (type != null)
            {
                try
                {
                    return Activator.CreateInstance(type, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance, null, new object[] { kernel }, null);
                }
                catch (MissingMethodException)
                {
                    try
                    {
                        return Activator.CreateInstance(type, true);
                    }
                    catch (MissingMethodException)
                    {
                    }
                }
            }

            return null;
        }).Where(plugin => plugin != null).ToList()!;
    }

    private static Dictionary<string, Type?> GetPluginTypes(List<string>? value)
    {
        Dictionary<string, Type?> plugins = new();

        if (value is not null)
        {
            var types = AppDomain.CurrentDomain.GetAssemblies()
                .Where(a => !a.IsDynamic)
                .SelectMany(a => a.GetTypes())
                .ToList();

            foreach (var pluginName in value)
            {
                if (pluginName is null)
                {
                    continue;
                }

                var type = types.FirstOrDefault(predicate: t => t.FullName?.Equals(pluginName, StringComparison.OrdinalIgnoreCase) ?? false);
                if (type is null)
                {
                    type = types.FirstOrDefault(t => t.FullName?.Contains(pluginName) ?? false);

                    if (type is null)
                    {
                        // If not found, assume the plugin would be loaded separately.
                        plugins.Add(pluginName, null);
                        continue;
                    }
                }

                plugins.Add(pluginName, type);
            }
        }

        return plugins;
    }

    /// <summary>
    /// Register the required arguments for the step
    /// </summary>
    /// <param name="requiredArguments">Array of required arguments</param>
    public void AddRequires(params string[] requiredArguments)
    {
        ValidateArguments(requiredArguments);
        this._requires.AddRange(requiredArguments);
    }

    /// <summary>
    /// Register the arguments provided by the step
    /// </summary>
    /// <param name="providedArguments">Array of provided arguments</param>
    public void AddProvides(params string[] providedArguments)
    {
        ValidateArguments(providedArguments);
        this._provides.AddRange(providedArguments);
    }

    /// <summary>
    /// Register the arguments passed through by the step
    /// </summary>
    /// <param name="passthroughArguments">Array of passthrough arguments</param>
    /// <param name="isReferencedFlow">Is referenced flow</param>
    public void AddPassthrough(string[] passthroughArguments, bool isReferencedFlow = false)
    {
        // A referenced flow is allowed to have steps that have passthrough arguments even if the completion type is not AtLeastOnce or ZeroOrMore. This is so the step can pass arguments to the outer flow.
        if (!isReferencedFlow
            && passthroughArguments.Length != 0
            && this.CompletionType != CompletionType.AtLeastOnce
            && this.CompletionType != CompletionType.ZeroOrMore)
        {
            throw new ArgumentException("Passthrough arguments can only be set for the AtLeastOnce or ZeroOrMore completion type");
        }

        ValidateArguments(passthroughArguments);
        this._passthrough.AddRange(passthroughArguments);
    }

    /// <summary>
    /// Get the plugin instances registered with the step
    /// </summary>
    /// <param name="kernel">The semantic kernel</param>
    /// <param name="globalPlugins">The global plugins available</param>
    public IEnumerable<object> LoadPlugins(Kernel kernel, Dictionary<object, string?> globalPlugins)
    {
        if (this._pluginsFactory != null)
        {
            return this._pluginsFactory(kernel, globalPlugins);
        }

        return Enumerable.Empty<object>();
    }

    /// <summary>
    /// Check if the step depends on another step
    /// </summary>
    /// <param name="otherStep">The other step</param>
    /// <returns>true if the step depends on the other step, false otherwise</returns>
    public bool DependsOn(FlowStep otherStep)
    {
        return this.Requires.Intersect(otherStep.Provides).Any();
    }

    private static void ValidateArguments(string[] arguments)
    {
        var invalidArguments = arguments.Intersect(Constants.ActionVariableNames.All).ToArray();

        if (invalidArguments.Length != 0)
        {
            throw new ArgumentException($"Invalid arguments: {string.Join(",", invalidArguments)}");
        }
    }
}
