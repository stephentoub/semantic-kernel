// Copyright (c) Microsoft. All rights reserved.

using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using Microsoft.SemanticKernel.SkillDefinition;

namespace Skills;

public sealed class TextSkill
{
    [SKFunction, Description("Remove spaces to the left of a string")]
    public string LStrip([Description("Text to edit")] string input) =>
        input.TrimStart();

    [SKFunction, Description("Remove spaces to the right of a string")]
    public string RStrip([Description("Text to edit")] string input) =>
        input.TrimEnd();

    [SKFunction, Description("Remove spaces to the left and right of a string")]
    public string Strip([Description("Text to edit")] string input) =>
        input.Trim();

    [SKFunction, Description("Change all string chars to uppercase")]
    public string Uppercase([Description("Text to uppercase")] string input) =>
        input.ToUpperInvariant();

    [SKFunction, Description("Change all string chars to lowercase")]
    [SuppressMessage("Globalization", "CA1308:Normalize strings to uppercase", Justification = "By design.")]
    public string Lowercase([Description("Text to lowercase")] string input) =>
        input.ToLowerInvariant();
}
