﻿// Copyright (c) Microsoft. All rights reserved.

using System;
using System.Linq;
using Microsoft.SemanticKernel.Experimental.Orchestration.Abstractions;

namespace Microsoft.SemanticKernel.Experimental.Orchestration;

/// <summary>
/// The flow validator
/// </summary>
public class FlowValidator : IFlowValidator
{
    /// <inheritdoc/>
    public void Validate(Flow flow)
    {
        Verify.NotNullOrWhiteSpace(flow.Goal, nameof(flow.Goal));

        ValidateNonEmpty(flow);
        ValidatePartialOrder(flow);
        ValidateReferenceStep(flow);
        ValidateStartingMessage(flow);
        ValidatePassthroughVariables(flow);
    }

    private static void ValidateStartingMessage(Flow flow)
    {
        foreach (var step in flow.Steps)
        {
            if (step.CompletionType is CompletionType.Optional or CompletionType.ZeroOrMore
                && string.IsNullOrEmpty(step.StartingMessage))
            {
                throw new ArgumentException(
                    $"Missing starting message for step={step.Goal} with completion type={step.CompletionType}");
            }
        }
    }

    private static void ValidateNonEmpty(Flow flow)
    {
        if (flow.Steps.Count == 0)
        {
            throw new ArgumentException("Flow must contain at least one flow step.");
        }
    }

    private static void ValidatePartialOrder(Flow flow)
    {
        try
        {
            var sorted = flow.SortSteps();
        }
        catch (Exception ex)
        {
            throw new ArgumentException("Flow steps must be a partial order set.", ex);
        }
    }

    private static void ValidateReferenceStep(Flow flow)
    {
        var steps = flow.Steps
            .Select(step => step as ReferenceFlowStep)
            .Where(step => step != null);

        foreach (var step in steps)
        {
            Verify.NotNullOrWhiteSpace(step!.FlowName);

            if (step.Requires.Any())
            {
                throw new ArgumentException("Reference flow step cannot have any direct requirements.");
            }

            if (step.Provides.Any())
            {
                throw new ArgumentException("Reference flow step cannot have any direct provides.");
            }

            if (step.Plugins?.Count != 0)
            {
                throw new ArgumentException("Reference flow step cannot have any direct plugins.");
            }
        }
    }

    private static void ValidatePassthroughVariables(Flow flow)
    {
        foreach (var step in flow.Steps)
        {
            if (step.CompletionType != CompletionType.AtLeastOnce
                && step.CompletionType != CompletionType.ZeroOrMore
                && step.Passthrough.Any())
            {
                throw new ArgumentException(
                    $"step={step.Goal} with completion type={step.CompletionType} cannot have passthrough variables as that is only applicable for the AtLeastOnce or ZeroOrMore completion types");
            }
        }
    }
}
