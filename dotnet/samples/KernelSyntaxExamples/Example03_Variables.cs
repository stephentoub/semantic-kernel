// Copyright (c) Microsoft. All rights reserved.

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.Orchestration;
using Plugins;
using RepoUtils;

// ReSharper disable once InconsistentNaming
public static class Example03_Variables
{
    private static readonly ILoggerFactory s_loggerFactory = ConsoleLogger.LoggerFactory;

    public static async Task RunAsync()
    {
        Console.WriteLine("======== Variables ========");

        Kernel kernel = new KernelBuilder().WithLoggerFactory(s_loggerFactory).Build();
        var textFunctions = kernel.ImportPlugin(new StaticTextPlugin(), "text");

        var variables = new Dictionary<string, object>()
        {
            ["input"] = "Today is: ",
            ["day"] = DateTimeOffset.Now,
        };

        KernelResult result = await kernel.RunAsync(variables,
            textFunctions["AppendDay"],
            textFunctions["Uppercase"]);

        Console.WriteLine(result.GetValue<string>());
    }
}
