// Copyright (c) Microsoft. All rights reserved.

using System;
using System.ComponentModel;
using System.Threading;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel.Orchestration;

namespace Microsoft.SemanticKernel.SkillDefinition;

/// <summary>
/// Specifies that a method is a native function available to Semantic Kernel.
/// </summary>
/// <remarks>
/// <para>
/// For a method to be recognized by the kernel as a native function, it must be tagged with this attribute.
/// A description of the method should be supplied using the <see cref="DescriptionAttribute"/>,
/// which will be used both with LLM prompts and embedding comparisons; the quality of the description affects
/// the planner ability to reason about complex tasks.
/// </para>
/// <para>
/// Functions may have any number of parameters. Parameters of type <see cref="ILogger"/> and
/// <see cref="CancellationToken"/> are filled in from the corresponding members of the <see cref="SKContext"/>;
/// <see cref="SKContext"/> itself may also be a parameter. A given native function may declare at
/// most one parameter of each of these types.  All other parameters must be of type <see cref="string"/>,
/// and will be populated based on a context variable of the same name, unless an <see cref="SKNameAttribute"/>
/// is used to override which context variable is targeted. A <see cref="DescriptionAttribute"/> should
/// be used on all string parameters to provide a description of the parameter suitable for consumption
/// by an LLM or embedding. A <see cref="DefaultValueAttribute"/> may also be used on a parameter to indicate
/// a default value to use for that parameter if there's no corresponding context variable; default parameters
/// may also be specified as the default value for an optional parameter in the language.
/// </para>
/// </remarks>
[AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
public sealed class SKFunctionAttribute : Attribute
{
    /// <summary>Initializes the attribute.</summary>
    /// <param name="isSensitive">Whether the function is set to be sensitive (default false).</param>
    public SKFunctionAttribute(bool isSensitive = false) => this.IsSensitive = isSensitive;

    /// <summary>
    /// Initializes the attribute with the specified description.
    /// </summary>
    /// <param name="description">Description of the function to be used by a planner to auto-discover functions.</param>
    /// <param name="isSensitive">Whether the function is set to be sensitive (default false).</param>
    [Obsolete("This constructor is deprecated and will be removed in one of the next SK SDK versions.")]
    [EditorBrowsable(EditorBrowsableState.Never)]
    public SKFunctionAttribute(string description, bool isSensitive = false)
    {
        this.Description = description;
        this.IsSensitive = isSensitive;
    }

    /// <summary>
    /// Whether the function is set to be sensitive (default false).
    /// When a function is sensitive, the default trust service will throw an exception
    /// if the function is invoked passing in some untrusted input (or context, or prompt).
    /// </summary>
    public bool IsSensitive { get; }

    [Obsolete("This property is deprecated and will be removed in one of the next SK SDK versions.")]
    [EditorBrowsable(EditorBrowsableState.Never)]
    public string Description { get; } = null!;
}
