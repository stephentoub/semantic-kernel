﻿// Copyright (c) Microsoft. All rights reserved.

using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using HandlebarsDotNet;
using HandlebarsDotNet.Compiler;

namespace Microsoft.SemanticKernel.PromptTemplates.Handlebars.Helpers;

/// <summary>
/// Extension class to register additional helpers as Kernel System helpers.
/// </summary>
internal static class KernelSystemHelpers
{
    /// <summary>
    /// Register all (default) or specific categories of system helpers.
    /// </summary>
    /// <param name="handlebarsInstance">The <see cref="IHandlebars"/>-instance.</param>
    /// <param name="kernel">Kernel instance.</param>
    /// <param name="variables">Dictionary of variables maintained by the Handlebars context.</param>
    /// <param name="options">Handlebars prompt template options.</param>
    public static void Register(
        IHandlebars handlebarsInstance,
        Kernel kernel,
        KernelArguments variables,
        HandlebarsPromptTemplateOptions options)
    {
        RegisterSystemHelpers(handlebarsInstance, kernel, variables);
    }

    /// <summary>
    /// Register all system helpers.
    /// </summary>
    /// <param name="handlebarsInstance">The <see cref="IHandlebars"/>-instance.</param>
    /// <param name="kernel">Kernel instance.</param>
    /// <param name="variables">Dictionary of variables maintained by the Handlebars context.</param>
    /// <exception cref="KernelException">Exception thrown when a message does not contain a defining role.</exception>
    private static void RegisterSystemHelpers(
        IHandlebars handlebarsInstance,
        Kernel kernel,
        KernelArguments variables)
    {
        // TODO [@teresaqhoang]: Issue #3947 Isolate Handlebars Kernel System helpers in their own class
        // Should also consider standardizing the naming conventions for these helpers, i.e., 'Message' instead of 'message'
        handlebarsInstance.RegisterHelper("message", static (writer, options, context, arguments) =>
        {
            var parameters = (IDictionary<string, object>)arguments[0];

            // Verify that the message has a role
            if (!parameters!.TryGetValue("role", out object? value))
            {
                throw new KernelException("Message must have a role.");
            }

            writer.Write($"<{value}~>", false);
            options.Template(writer, context);
            writer.Write($"</{value}~>", false);
        });

        handlebarsInstance.RegisterHelper("set", (writer, context, arguments) =>
        {
            string name;
            object value = string.Empty;
            if (arguments[0].GetType() == typeof(HashParameterDictionary))
            {
                // Get the parameters from the template arguments
                var parameters = (IDictionary<string, object>)arguments[0];
                name = (string)parameters!["name"];
                value = parameters!["value"];
            }
            else
            {
                name = arguments[0].ToString() ?? string.Empty;
                value = arguments[1];
            }

            // Set the variable in the Handlebars context
            variables[name] = value;
        });

        handlebarsInstance.RegisterHelper("json", static (in HelperOptions options, in Context context, in Arguments arguments) =>
        {
            if (arguments.Length == 0 || arguments[0] is null)
            {
                throw new HandlebarsRuntimeException("`json` helper requires a value to be passed in.");
            }

            object objectToSerialize = arguments[0];
            var type = objectToSerialize.GetType();

            return type == typeof(string) ? objectToSerialize
                : type == typeof(JsonNode) ? objectToSerialize.ToString()
                : JsonSerializer.Serialize(objectToSerialize);
        });

        handlebarsInstance.RegisterHelper("concat", (in HelperOptions options, in Context context, in Arguments arguments) =>
        {
            var args = arguments.ToList().Select(arg =>
            {
                if (arg is UndefinedBindingResult result)
                {
                    return variables.TryGetValue(result.Value, out var variable) ? variable : result.Value;
                }

                return arg;
            });

            return string.Concat(arguments);
        });

        handlebarsInstance.RegisterHelper("array", (in HelperOptions options, in Context context, in Arguments arguments) =>
        {
            return arguments.ToArray();
        });

        handlebarsInstance.RegisterHelper("raw", static (writer, options, context, arguments) =>
        {
            options.Template(writer, null);
        });

        handlebarsInstance.RegisterHelper("range", (in HelperOptions options, in Context context, in Arguments arguments) =>
        {
            // Create list with numbers from start to end (inclusive)
            var start = int.Parse(arguments[0].ToString() ?? string.Empty, kernel.Culture);
            var end = int.Parse(arguments[1].ToString() ?? string.Empty, kernel.Culture) + 1;
            var count = end - start;

            return Enumerable.Range(start, count);
        });

        handlebarsInstance.RegisterHelper("or", static (in HelperOptions options, in Context context, in Arguments arguments) =>
        {
            return arguments.Any(arg => arg != null && arg is not false);
        });

        handlebarsInstance.RegisterHelper("equals", static (in HelperOptions options, in Context context, in Arguments arguments) =>
        {
            if (arguments.Length < 2)
            {
                return false;
            }

            object? left = arguments[0];
            object? right = arguments[1];

            return left == right || (left is not null && left.Equals(right));
        });
    }
}
