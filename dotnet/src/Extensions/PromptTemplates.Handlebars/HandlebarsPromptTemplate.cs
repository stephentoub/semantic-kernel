﻿// Copyright (c) Microsoft. All rights reserved.

using System.Threading;
using System.Threading.Tasks;
using HandlebarsDotNet;
using HandlebarsDotNet.Helpers;
using Microsoft.SemanticKernel.PromptTemplates.Handlebars.Helpers;

namespace Microsoft.SemanticKernel.PromptTemplates.Handlebars;

/// <summary>
/// Represents a Handlebars prompt template.
/// </summary>
internal sealed class HandlebarsPromptTemplate : IPromptTemplate
{
    /// <summary>
    /// Default options for built-in Handlebars helpers.
    /// </summary>
    /// TODO [@teresaqhoang]: Support override of default options
    private readonly HandlebarsPromptTemplateOptions _options;

    /// <summary>
    /// Constructor for Handlebars PromptTemplate.
    /// </summary>
    /// <param name="promptConfig">Prompt template configuration</param>
    /// <param name="options">Handlebars prompt template options</param>
    public HandlebarsPromptTemplate(PromptTemplateConfig promptConfig, HandlebarsPromptTemplateOptions? options = null)
    {
        this._promptModel = promptConfig;
        this._options = options ?? new();
    }

    /// <inheritdoc/>
#pragma warning disable CS1998 // Async method lacks 'await' operators and will run synchronously
    public async Task<string> RenderAsync(Kernel kernel, KernelArguments? arguments = null, CancellationToken cancellationToken = default)
#pragma warning restore CS1998 // Async method lacks 'await' operators and will run synchronously
    {
        Verify.NotNull(kernel);

        arguments = this.GetVariables(arguments);
        var handlebarsInstance = HandlebarsDotNet.Handlebars.Create();

        // Register kernel, system, and any custom helpers
        this.RegisterHelpers(handlebarsInstance, kernel, arguments, cancellationToken);

        var template = handlebarsInstance.Compile(this._promptModel.Template);
        return System.Net.WebUtility.HtmlDecode(template(arguments).Trim());
    }

    #region private

    private readonly PromptTemplateConfig _promptModel;

    /// <summary>
    /// Registers kernel, system, and any custom helpers.
    /// </summary>
    private void RegisterHelpers(
        IHandlebars handlebarsInstance,
        Kernel kernel,
        KernelArguments arguments,
        CancellationToken cancellationToken = default)
    {
        // Add SK's built-in system helpers
        KernelSystemHelpers.Register(handlebarsInstance, kernel, arguments, this._options);

        // Add built-in helpers from the HandlebarsDotNet library
        HandlebarsHelpers.Register(handlebarsInstance, optionsCallback: options =>
        {
            options.PrefixSeparator = this._options.PrefixSeparator;
            options.Categories = this._options.Categories;
            options.UseCategoryPrefix = this._options.UseCategoryPrefix;
            options.CustomHelperPaths = this._options.CustomHelperPaths;
        });

        // Add helpers for kernel functions
        KernelFunctionHelpers.Register(handlebarsInstance, kernel, arguments, this._options.PrefixSeparator, cancellationToken);

        // Add any custom helpers
        this._options.RegisterCustomHelpers?.Invoke(
            (string name, HandlebarsReturnHelper customHelper)
                => KernelHelpersUtils.RegisterHelperSafe(handlebarsInstance, name, customHelper),
            this._options,
            arguments);
    }

    /// <summary>
    /// Gets the variables for the prompt template, including setting any default values from the prompt config.
    /// </summary>
    private KernelArguments GetVariables(KernelArguments? arguments)
    {
        KernelArguments result = new();

        foreach (var p in this._promptModel.InputVariables)
        {
            if (p.Default == null || (p.Default is string stringDefault && stringDefault.Length == 0))
            {
                continue;
            }

            result[p.Name] = p.Default;
        }

        if (arguments is not null)
        {
            foreach (var kvp in arguments)
            {
                if (kvp.Value is not null)
                {
                    result[kvp.Key] = kvp.Value;
                }
            }
        }

        return result;
    }

    #endregion
}
