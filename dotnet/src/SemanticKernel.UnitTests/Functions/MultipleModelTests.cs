﻿// Copyright (c) Microsoft. All rights reserved.

using System.Threading;
using System.Threading.Tasks;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.AI;
using Microsoft.SemanticKernel.AI.TextCompletion;
using Moq;
using Xunit;

namespace SemanticKernel.UnitTests.Functions;
public class MultipleModelTests
{
    [Fact]
    public async Task ItUsesServiceIdWhenProvidedAsync()
    {
        // Arrange
        var mockTextCompletion1 = new Mock<ITextCompletion>();
        var mockTextCompletion2 = new Mock<ITextCompletion>();
        var mockCompletionResult = new Mock<ITextResult>();

        mockTextCompletion1.Setup(c => c.GetCompletionsAsync(It.IsAny<string>(), It.IsAny<PromptExecutionSettings>(), It.IsAny<CancellationToken>())).ReturnsAsync(new[] { mockCompletionResult.Object });
        mockTextCompletion2.Setup(c => c.GetCompletionsAsync(It.IsAny<string>(), It.IsAny<PromptExecutionSettings>(), It.IsAny<CancellationToken>())).ReturnsAsync(new[] { mockCompletionResult.Object });
        mockCompletionResult.Setup(cr => cr.GetCompletionAsync(It.IsAny<CancellationToken>())).ReturnsAsync("llmResult");

        var kernel = new KernelBuilder()
            .WithAIService("service1", mockTextCompletion1.Object)
            .WithAIService("service2", mockTextCompletion2.Object)
            .Build();

        var templateConfig = new PromptTemplateConfig();
        templateConfig.ModelSettings.Add(new PromptExecutionSettings() { ServiceId = "service1" });
        KernelFunction func = kernel.CreateFunctionFromPrompt("template", templateConfig, "functionName");

        // Act
        await kernel.InvokeAsync(func);

        // Assert
        mockTextCompletion1.Verify(a => a.GetCompletionsAsync("template", It.IsAny<PromptExecutionSettings>(), It.IsAny<CancellationToken>()), Times.Once());
        mockTextCompletion2.Verify(a => a.GetCompletionsAsync("template", It.IsAny<PromptExecutionSettings>(), It.IsAny<CancellationToken>()), Times.Never());
    }

    [Fact]
    public async Task ItFailsIfInvalidServiceIdIsProvidedAsync()
    {
        // Arrange
        var mockTextCompletion1 = new Mock<ITextCompletion>();
        var mockTextCompletion2 = new Mock<ITextCompletion>();

        var kernel = new KernelBuilder()
            .WithAIService("service1", mockTextCompletion1.Object)
            .WithAIService("service2", mockTextCompletion2.Object)
            .Build();

        var templateConfig = new PromptTemplateConfig();
        templateConfig.ModelSettings.Add(new PromptExecutionSettings() { ServiceId = "service3" });
        var func = kernel.CreateFunctionFromPrompt("template", templateConfig, "functionName");

        // Act
        var exception = await Assert.ThrowsAsync<KernelException>(() => kernel.InvokeAsync(func));

        // Assert
        Assert.Equal("Service of type Microsoft.SemanticKernel.AI.TextCompletion.ITextCompletion and name service3 not registered.", exception.Message);
    }

    [Theory]
    [InlineData(new string[] { "service1" }, new int[] { 1, 0, 0 })]
    [InlineData(new string[] { "service4", "service1" }, new int[] { 1, 0, 0 })]
    public async Task ItUsesServiceIdByOrderAsync(string[] serviceIds, int[] callCount)
    {
        // Arrange
        var mockTextCompletion1 = new Mock<ITextCompletion>();
        var mockTextCompletion2 = new Mock<ITextCompletion>();
        var mockTextCompletion3 = new Mock<ITextCompletion>();
        var mockCompletionResult = new Mock<ITextResult>();

        mockTextCompletion1.Setup(c => c.GetCompletionsAsync(It.IsAny<string>(), It.IsAny<PromptExecutionSettings>(), It.IsAny<CancellationToken>())).ReturnsAsync(new[] { mockCompletionResult.Object });
        mockTextCompletion2.Setup(c => c.GetCompletionsAsync(It.IsAny<string>(), It.IsAny<PromptExecutionSettings>(), It.IsAny<CancellationToken>())).ReturnsAsync(new[] { mockCompletionResult.Object });
        mockTextCompletion3.Setup(c => c.GetCompletionsAsync(It.IsAny<string>(), It.IsAny<PromptExecutionSettings>(), It.IsAny<CancellationToken>())).ReturnsAsync(new[] { mockCompletionResult.Object });
        mockCompletionResult.Setup(cr => cr.GetCompletionAsync(It.IsAny<CancellationToken>())).ReturnsAsync("llmResult");

        var kernel = new KernelBuilder()
            .WithAIService("service1", mockTextCompletion1.Object)
            .WithAIService("service2", mockTextCompletion2.Object)
            .WithAIService("service3", mockTextCompletion3.Object)
            .Build();

        var templateConfig = new PromptTemplateConfig();
        foreach (var serviceId in serviceIds)
        {
            templateConfig.ModelSettings.Add(new PromptExecutionSettings() { ServiceId = serviceId });
        }
        var func = kernel.CreateFunctionFromPrompt("template", templateConfig, "functionName");

        // Act
        await kernel.InvokeAsync(func);

        // Assert
        mockTextCompletion1.Verify(a => a.GetCompletionsAsync("template", It.Is<PromptExecutionSettings>(settings => settings.ServiceId == "service1"), It.IsAny<CancellationToken>()), Times.Exactly(callCount[0]));
        mockTextCompletion2.Verify(a => a.GetCompletionsAsync("template", It.Is<PromptExecutionSettings>(settings => settings.ServiceId == "service2"), It.IsAny<CancellationToken>()), Times.Exactly(callCount[1]));
        mockTextCompletion3.Verify(a => a.GetCompletionsAsync("template", It.Is<PromptExecutionSettings>(settings => settings.ServiceId == "service3"), It.IsAny<CancellationToken>()), Times.Exactly(callCount[2]));
    }

    [Fact]
    public async Task ItUsesServiceIdWithJsonPromptTemplateConfigAsync()
    {
        // Arrange
        var mockTextCompletion1 = new Mock<ITextCompletion>();
        var mockTextCompletion2 = new Mock<ITextCompletion>();
        var mockTextCompletion3 = new Mock<ITextCompletion>();
        var mockCompletionResult = new Mock<ITextResult>();

        mockTextCompletion1.Setup(c => c.GetCompletionsAsync(It.IsAny<string>(), It.IsAny<PromptExecutionSettings>(), It.IsAny<CancellationToken>())).ReturnsAsync(new[] { mockCompletionResult.Object });
        mockTextCompletion2.Setup(c => c.GetCompletionsAsync(It.IsAny<string>(), It.IsAny<PromptExecutionSettings>(), It.IsAny<CancellationToken>())).ReturnsAsync(new[] { mockCompletionResult.Object });
        mockTextCompletion3.Setup(c => c.GetCompletionsAsync(It.IsAny<string>(), It.IsAny<PromptExecutionSettings>(), It.IsAny<CancellationToken>())).ReturnsAsync(new[] { mockCompletionResult.Object });
        mockCompletionResult.Setup(cr => cr.GetCompletionAsync(It.IsAny<CancellationToken>())).ReturnsAsync("llmResult");

        var kernel = new KernelBuilder()
            .WithAIService("service1", mockTextCompletion1.Object)
            .WithAIService("service2", mockTextCompletion2.Object)
            .WithAIService("service3", mockTextCompletion3.Object)
            .Build();

        var json = @"{
  ""schema"": 1,
  ""description"": ""Semantic function"",
  ""models"": [
    {
      ""service_id"": ""service2"",
      ""max_tokens"": 100,
      ""temperature"": 0.2,
      ""top_p"": 0.0,
      ""presence_penalty"": 0.0,
      ""frequency_penalty"": 0.0,
      ""stop_sequences"": [
        ""\n""
      ]
    },
    {
      ""service_id"": ""service3"",
      ""max_tokens"": 100,
      ""temperature"": 0.4,
      ""top_p"": 0.0,
      ""presence_penalty"": 0.0,
      ""frequency_penalty"": 0.0,
      ""stop_sequences"": [
        ""\n""
      ]
    }
  ]
}";

        var templateConfig = PromptTemplateConfig.FromJson(json);
        var func = kernel.CreateFunctionFromPrompt("template", templateConfig, "functionName");

        // Act
        await kernel.InvokeAsync(func);

        // Assert
        mockTextCompletion1.Verify(a => a.GetCompletionsAsync("template", It.Is<PromptExecutionSettings>(settings => settings.ServiceId == "service1"), It.IsAny<CancellationToken>()), Times.Never());
        mockTextCompletion2.Verify(a => a.GetCompletionsAsync("template", It.Is<PromptExecutionSettings>(settings => settings.ServiceId == "service2"), It.IsAny<CancellationToken>()), Times.Once());
        mockTextCompletion3.Verify(a => a.GetCompletionsAsync("template", It.Is<PromptExecutionSettings>(settings => settings.ServiceId == "service3"), It.IsAny<CancellationToken>()), Times.Never());
    }
}
