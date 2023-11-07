// Copyright (c) Microsoft. All rights reserved.

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Reflection;
using Microsoft.Extensions.Logging;

#pragma warning disable IDE0130
// ReSharper disable once CheckNamespace - Using the main namespace
namespace Microsoft.SemanticKernel;
#pragma warning restore IDE0130

/// <summary>
/// Factory methods for creating <seealso cref="IKernelFunction"/> instances.
/// </summary>
public static class KernelFunction
{
    /// <summary>
    /// Creates an <see cref="IKernelFunction"/> instance for a method, specified via a delegate.
    /// </summary>
    /// <param name="method">The method to be represented via the created <see cref="IKernelFunction"/>.</param>
    /// <param name="functionName">Optional function name. If null, it will default to one derived from the method represented by <paramref name="method"/>.</param>
    /// <param name="description">Optional description of the method. If null, it will default to one derived from the method represented by <paramref name="method"/>, if possible (e.g. via a <see cref="DescriptionAttribute"/> on the method).</param>
    /// <param name="parameters">Optional parameter descriptions. If null, it will default to one derived from the method represented by <paramref name="method"/>.</param>
    /// <param name="loggerFactory">The <see cref="ILoggerFactory"/> to use for logging. If null, no logging will be performed.</param>
    /// <returns>The created <see cref="IKernelFunction"/> wrapper for <paramref name="method"/>.</returns>
    public static IKernelFunction Create(
        Delegate method,
        string? functionName = null,
        string? description = null,
        IEnumerable<ParameterView>? parameters = null,
        ILoggerFactory? loggerFactory = null) =>
        Create(method.Method, method.Target, functionName, description, parameters, loggerFactory);

    /// <summary>
    /// Creates an <see cref="IKernelFunction"/> instance for a method, specified via an <see cref="MethodInfo"/> instance
    /// and an optional target object if the method is an instance method.
    /// </summary>
    /// <param name="method">The method to be represented via the created <see cref="IKernelFunction"/>.</param>
    /// <param name="target">The target object for the <paramref name="method"/> if it represents an instance method. This should be null if and only if <paramref name="method"/> is a static method.</param>
    /// <param name="functionName">Optional function name. If null, it will default to one derived from the method represented by <paramref name="method"/>.</param>
    /// <param name="description">Optional description of the method. If null, it will default to one derived from the method represented by <paramref name="method"/>, if possible (e.g. via a <see cref="DescriptionAttribute"/> on the method).</param>
    /// <param name="parameters">Optional parameter descriptions. If null, it will default to one derived from the method represented by <paramref name="method"/>.</param>
    /// <param name="loggerFactory">The <see cref="ILoggerFactory"/> to use for logging. If null, no logging will be performed.</param>
    /// <returns>The created <see cref="IKernelFunction"/> wrapper for <paramref name="method"/>.</returns>
    public static IKernelFunction Create(
        MethodInfo method,
        object? target = null,
        string? functionName = null,
        string? description = null,
        IEnumerable<ParameterView>? parameters = null,
        ILoggerFactory? loggerFactory = null) =>
        DelegateKernelFunction.Create(method, target, functionName, description, parameters, loggerFactory);
}
