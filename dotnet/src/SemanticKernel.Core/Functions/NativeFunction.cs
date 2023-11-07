// Copyright (c) Microsoft. All rights reserved.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.SemanticKernel.Diagnostics;
using Microsoft.SemanticKernel.Orchestration;

#pragma warning disable IDE0130
// ReSharper disable once CheckNamespace - Using the main namespace
namespace Microsoft.SemanticKernel;
#pragma warning restore IDE0130

/// <summary>
/// <see cref="IKernelFunction"/> implementation backed by a delegate.
/// </summary>
[DebuggerDisplay("{DebuggerDisplay,nq}")]
internal sealed class DelegateKernelFunction : IKernelFunction
{
    private static readonly ReadOnlyDictionary<string, object?> s_emptyArguments = new ReadOnlyDictionary<string, object?>(new Dictionary<string, object?>());

    /// <summary>
    /// Creates an <see cref="IKernelFunction"/> instance for a .NET method, specified via an <see cref="MethodInfo"/> instance
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
        object? target,
        string? functionName,
        string? description,
        IEnumerable<ParameterView>? parameters,
        ILoggerFactory? loggerFactory)
    {
        Verify.NotNull(method);
        if (!method.IsStatic && target is null)
        {
            throw new ArgumentNullException(nameof(target), "Target must not be null for an instance method.");
        }

        ILogger logger = loggerFactory?.CreateLogger(method.DeclaringType ?? typeof(KernelFunction)) ?? NullLogger.Instance;

        MethodDetails methodDetails = GetMethodDetails(method, target, logger);
        var result = new DelegateKernelFunction(
            methodDetails.Function,
            functionName ?? methodDetails.Name,
            description ?? methodDetails.Description,
            parameters?.ToList() ?? methodDetails.Parameters,
            logger);

        if (logger.IsEnabled(LogLevel.Trace))
        {
            logger.LogTrace("Created IKernelFunction '{Name}' for '{MethodName}'", result.Name, method.Name);
        }

        return result;
    }

    /// <inheritdoc/>
    public string Name { get; }

    /// <inheritdoc/>
    public string Description { get; }

    /// <inheritdoc/>
    public FunctionView Describe() => this._view ??= new FunctionView(this.Name, this.Description, this._parameters);

    /// <inheritdoc/>
    public async Task<FunctionResult> InvokeAsync(
        Kernel kernel,
        IReadOnlyDictionary<string, object?>? arguments = null,
        KernelContext? context = null,
        CancellationToken cancellationToken = default)
    {
        Verify.NotNull(kernel);

        try
        {
            if (this._logger.IsEnabled(LogLevel.Trace))
            {
                this._logger.LogTrace("Function {Name} invoked.", this.Name);
            }

            FunctionResult result = await this._function(kernel, arguments ?? s_emptyArguments, context ?? new KernelContext(), cancellationToken).ConfigureAwait(false);

            if (this._logger.IsEnabled(LogLevel.Trace))
            {
                this._logger.LogTrace("Function {Name} invocation completed: {Result}", this.Name, result.GetValue<object>()?.ToString());
            }

            return result;
        }
        catch (Exception e)
        {
            this._logger.LogError(e, "Function {Name} execution failed: {Error}", this.Name, e.Message);
            throw;
        }
    }

    /// <summary>
    /// JSON serialized string representation of the function.
    /// </summary>
    public override string ToString() => this.ToString(false);

    /// <summary>
    /// JSON serialized string representation of the function.
    /// </summary>
    public string ToString(bool writeIndented) =>
        JsonSerializer.Serialize(this, options: writeIndented ? s_toStringIndentedSerialization : s_toStringStandardSerialization);

    #region private

    /// <summary>Delegate used to invoke the underlying delegate.</summary>
    /// <returns></returns>
    private delegate ValueTask<FunctionResult> ImplementationFunc(
        Kernel kernel,
        IReadOnlyDictionary<string, object?> arguments,
        KernelContext context,
        CancellationToken cancellationToken);

    private static readonly JsonSerializerOptions s_toStringStandardSerialization = new();
    private static readonly JsonSerializerOptions s_toStringIndentedSerialization = new() { WriteIndented = true };
    private readonly ImplementationFunc _function;
    private readonly IReadOnlyList<ParameterView> _parameters;
    private readonly ILogger _logger;

    private record struct MethodDetails(string Name, string Description, ImplementationFunc Function, List<ParameterView> Parameters);

    private DelegateKernelFunction(
        ImplementationFunc implementationFunc,
        string functionName,
        string description,
        IReadOnlyList<ParameterView> parameters,
        ILogger logger)
    {
        Verify.ValidFunctionName(functionName);

        this._logger = logger;

        this._function = implementationFunc;
        this._parameters = parameters.ToArray();
        Verify.ParametersUniqueness(this._parameters);

        this.Name = functionName;
        this.Description = description;
    }

    private static MethodDetails GetMethodDetails(MethodInfo method, object? target, ILogger logger)
    {
        // Get the name to use for the function.  If the function has an SKName attribute, we use that.
        // Otherwise, we use the name of the method, but strip off any "Async" suffix if it's {Value}Task-returning.
        // We don't apply any heuristics to the value supplied by SKName so that it can always be used
        // as a definitive override.
        string? functionName = method.GetCustomAttribute<SKNameAttribute>(inherit: true)?.Name?.Trim();
        if (string.IsNullOrEmpty(functionName))
        {
            functionName = SanitizeMetadataName(method.Name!);
            Verify.ValidFunctionName(functionName);

            if (IsAsyncMethod(method) &&
                functionName.EndsWith("Async", StringComparison.Ordinal) &&
                functionName.Length > "Async".Length)
            {
                functionName = functionName.Substring(0, functionName.Length - "Async".Length);
            }
        }

        string? description = method.GetCustomAttribute<DescriptionAttribute>(inherit: true)?.Description;

        var result = new MethodDetails
        {
            Name = functionName!,
            Description = description ?? string.Empty,
        };

        (result.Function, result.Parameters) = GetDelegateInfo(functionName!, target, method);

        return result;
    }

    /// <summary>Gets whether a method has a known async return type.</summary>
    private static bool IsAsyncMethod(MethodInfo method)
    {
        Type t = method.ReturnType;

        if (t == typeof(Task) || t == typeof(ValueTask))
        {
            return true;
        }

        if (t.IsGenericType)
        {
            t = t.GetGenericTypeDefinition();
            if (t == typeof(Task<>) || t == typeof(ValueTask<>))
            {
                return true;
            }
        }

        return false;
    }

    // Inspect a method and returns the corresponding delegate and related info
    private static (ImplementationFunc function, List<ParameterView>) GetDelegateInfo(
        string functionName,
        object? instance,
        MethodInfo method)
    {
        ThrowForInvalidSignatureIf(method.IsGenericMethodDefinition, method, "Generic methods are not supported");

        var stringParameterViews = new List<ParameterView>();
        var parameters = method.GetParameters();

        // Get marshaling funcs for parameters and build up the parameter views.
        var parameterFuncs = new Func<Kernel, IReadOnlyDictionary<string, object?>, KernelContext, CancellationToken, object?>[parameters.Length];
        bool sawFirstParameter = false, hasKernelParam = false, hasKernelContextParam = false, hasCancellationTokenParam = false, hasLoggerParam = false, hasMemoryParam = false, hasCultureParam = false;
        for (int i = 0; i < parameters.Length; i++)
        {
            (parameterFuncs[i], ParameterView? parameterView) = GetParameterMarshalerDelegate(
                method, parameters[i],
                ref sawFirstParameter, ref hasKernelParam, ref hasKernelContextParam, ref hasCancellationTokenParam, ref hasLoggerParam, ref hasMemoryParam, ref hasCultureParam);
            if (parameterView is not null)
            {
                stringParameterViews.Add(parameterView);
            }
        }

        // Get marshaling func for the return value.
        Func<string, object?, KernelContext, ValueTask<FunctionResult>> returnFunc = GetReturnValueMarshalerDelegate(method);

        // Create the func
        ValueTask<FunctionResult> Function(Kernel kernel, IReadOnlyDictionary<string, object?> arguments, KernelContext context, CancellationToken cancellationToken)
        {
            // Create the arguments.
            object?[] args = parameterFuncs.Length != 0 ? new object?[parameterFuncs.Length] : Array.Empty<object?>();
            for (int i = 0; i < args.Length; i++)
            {
                args[i] = parameterFuncs[i](kernel, arguments, context, cancellationToken);
            }

            // Invoke the method.
            object? result = method.Invoke(instance, args);

            // Extract and return the result.
            return returnFunc(functionName, result, context);
        }

        // Check for param names conflict
        Verify.ParametersUniqueness(stringParameterViews);

        // Return the function and its parameter views.
        return (Function, stringParameterViews);
    }

    /// <summary>
    /// Gets a delegate for handling the marshaling of a parameter.
    /// </summary>
    private static (Func<Kernel, IReadOnlyDictionary<string, object?>, KernelContext, CancellationToken, object?>, ParameterView?) GetParameterMarshalerDelegate(
        MethodInfo method, ParameterInfo parameter,
        ref bool sawFirstParameter, ref bool hasKernelParam, ref bool hasKernelContextParam, ref bool hasCancellationTokenParam, ref bool hasLoggerParam, ref bool hasMemoryParam, ref bool hasCultureParam)
    {
        Type type = parameter.ParameterType;

        // Handle special types based on KernelContext data. These can each show up at most once in the method signature,
        // with the KernelContext itself or the primary data from it mapped directly into the method's parameter.
        // They do not get parameter views as they're not supplied from context variables.

        if (type == typeof(Kernel))
        {
            TrackUniqueParameterType(ref hasKernelParam, method, $"At most one {nameof(Kernel)} parameter is permitted.");
            return (static (Kernel kernel, IReadOnlyDictionary<string, object?> _, KernelContext _, CancellationToken _) => kernel, null);
        }

        if (type == typeof(KernelContext))
        {
            TrackUniqueParameterType(ref hasKernelContextParam, method, $"At most one {nameof(KernelContext)} parameter is permitted.");
            return (static (Kernel _, IReadOnlyDictionary<string, object?> _, KernelContext context, CancellationToken _) => context, null);
        }

        if (type == typeof(ILogger) || type == typeof(ILoggerFactory))
        {
            TrackUniqueParameterType(ref hasLoggerParam, method, $"At most one {nameof(ILogger)}/{nameof(ILoggerFactory)} parameter is permitted.");
            return type == typeof(ILogger) ?
                ((Kernel kernel, IReadOnlyDictionary<string, object?> _, KernelContext _, CancellationToken _) => kernel.LoggerFactory.CreateLogger(method?.DeclaringType ?? typeof(KernelFunction)), null) :
                ((Kernel kernel, IReadOnlyDictionary<string, object?> _, KernelContext _, CancellationToken _) => kernel.LoggerFactory, null);
        }

        if (type == typeof(CultureInfo) || type == typeof(IFormatProvider))
        {
            TrackUniqueParameterType(ref hasCultureParam, method, $"At most one {nameof(CultureInfo)}/{nameof(IFormatProvider)} parameter is permitted.");
            return (static (Kernel _, IReadOnlyDictionary<string, object?> _, KernelContext context, CancellationToken _) => context.Culture, null);
        }

        if (type == typeof(CancellationToken))
        {
            TrackUniqueParameterType(ref hasCancellationTokenParam, method, $"At most one {nameof(CancellationToken)} parameter is permitted.");
            return (static (Kernel _, IReadOnlyDictionary<string, object?> _, KernelContext _, CancellationToken cancellationToken) => cancellationToken, null);
        }

        // Handle context variables. These are supplied from the KernelContext's Variables dictionary.

        if (!type.IsByRef && GetConverterFrom(type) is Func<object?, CultureInfo, object?> parser)
        {
            // Use either the parameter's name or an override from an applied SKName attribute.
            SKNameAttribute? nameAttr = parameter.GetCustomAttribute<SKNameAttribute>(inherit: true);
            string name = nameAttr?.Name?.Trim() ?? SanitizeMetadataName(parameter.Name);
            bool nameIsInput = name.Equals("input", StringComparison.OrdinalIgnoreCase);
            ThrowForInvalidSignatureIf(name.Length == 0, method, $"Parameter {parameter.Name}'s context attribute defines an invalid name.");
            ThrowForInvalidSignatureIf(sawFirstParameter && nameIsInput, method, "Only the first parameter may be named 'input'");

            // Use either the parameter's optional default value as contained in parameter metadata (e.g. `string s = "hello"`)
            // or an override from an applied SKParameter attribute. Note that a default value may be null.
            DefaultValueAttribute defaultValueAttribute = parameter.GetCustomAttribute<DefaultValueAttribute>(inherit: true);
            bool hasDefaultValue = defaultValueAttribute is not null;
            object? defaultValue = defaultValueAttribute?.Value;
            if (!hasDefaultValue && parameter.HasDefaultValue)
            {
                hasDefaultValue = true;
                defaultValue = parameter.DefaultValue;
            }

            if (hasDefaultValue)
            {
                // Invariant culture is used here as this value comes from the C# source
                // and it should be deterministic across cultures.
                defaultValue = parser(defaultValue, CultureInfo.InvariantCulture);
            }

            bool fallBackToInput = !sawFirstParameter && !nameIsInput;
            object? parameterFunc(Kernel kernel, IReadOnlyDictionary<string, object?> arguments, KernelContext context, CancellationToken _)
            {
                // 1. Use the value of the variable if it exists.
                if (arguments.TryGetValue(name, out object? value))
                {
                    return Process(value);
                }

                // 2. Otherwise, use the default value if there is one, sourced either from an attribute or the parameter's default.
                if (hasDefaultValue)
                {
                    return defaultValue;
                }

                // 3. Otherwise, fail.
                throw new KernelException($"Missing value for parameter '{name}'",
                    new ArgumentException("Missing value function parameter", name));

                object? Process(object? value)
                {
                    try
                    {
                        return parser(value, context.Culture);
                    }
                    catch (Exception e) when (!e.IsCriticalException())
                    {
                        throw new ArgumentOutOfRangeException(name, value, e.Message);
                    }
                }
            }

            sawFirstParameter = true;

            var parameterView = new ParameterView(
                name,
                parameter.GetCustomAttribute<DescriptionAttribute>(inherit: true)?.Description ?? string.Empty,
                defaultValue?.ToString() ?? string.Empty,
                IsRequired: !parameter.IsOptional);

            return (parameterFunc, parameterView);
        }

        // Fail for unknown parameter types.
        throw GetExceptionForInvalidSignature(method, $"Unknown parameter type {parameter.ParameterType}");
    }

    /// <summary>
    /// Gets a delegate for handling the result value of a method, converting it into the <see cref="Task{KernelContext}"/> to return from the invocation.
    /// </summary>
    private static Func<string, object?, KernelContext, ValueTask<FunctionResult>> GetReturnValueMarshalerDelegate(MethodInfo method)
    {
        // Handle each known return type for the method
        Type returnType = method.ReturnType;

        // No return value, either synchronous (void) or asynchronous (Task / ValueTask).

        if (returnType == typeof(void))
        {
            return static (functionName, result, context) =>
                new ValueTask<FunctionResult>(new FunctionResult(functionName, context));
        }

        if (returnType == typeof(Task))
        {
            return async static (functionName, result, context) =>
            {
                await ((Task)ThrowIfNullResult(result)).ConfigureAwait(false);
                return new FunctionResult(functionName, context);
            };
        }

        if (returnType == typeof(ValueTask))
        {
            return async static (functionName, result, context) =>
            {
                await ((ValueTask)ThrowIfNullResult(result)).ConfigureAwait(false);
                return new FunctionResult(functionName, context);
            };
        }

        // string (which is special as no marshaling is required), either synchronous (string) or asynchronous (Task<string> / ValueTask<string>)

        if (returnType == typeof(string))
        {
            return static (functionName, result, context) =>
            {
                return new ValueTask<FunctionResult>(new FunctionResult(functionName, context, (string?)result));
            };
        }

        if (returnType == typeof(Task<string>))
        {
            return async static (functionName, result, context) =>
            {
                var resultString = await ((Task<string>)ThrowIfNullResult(result)).ConfigureAwait(false);
                return new FunctionResult(functionName, context, resultString);
            };
        }

        if (returnType == typeof(ValueTask<string>))
        {
            return async static (functionName, result, context) =>
            {
                var resultString = await ((ValueTask<string>)ThrowIfNullResult(result)).ConfigureAwait(false);
                return new FunctionResult(functionName, context, resultString);
            };
        }

        // All other synchronous return types T.

        if (!returnType.IsGenericType || returnType.GetGenericTypeDefinition() == typeof(Nullable<>))
        {
            return (functionName, result, context) =>
            {
                return new ValueTask<FunctionResult>(new FunctionResult(functionName, context, result));
            };
        }

        // All other asynchronous return types

        // Task<T>
        if (returnType.GetGenericTypeDefinition() is Type genericTask &&
            genericTask == typeof(Task<>) &&
            returnType.GetProperty("Result", BindingFlags.Public | BindingFlags.Instance)?.GetGetMethod() is MethodInfo taskResultGetter)
        {
            return async (functionName, result, context) =>
            {
                await ((Task)ThrowIfNullResult(result)).ConfigureAwait(false);

                var taskResult = taskResultGetter.Invoke(result!, Array.Empty<object>());

                return new FunctionResult(functionName, context, taskResult);
            };
        }

        // ValueTask<T>
        if (returnType.GetGenericTypeDefinition() is Type genericValueTask &&
            genericValueTask == typeof(ValueTask<>) &&
            returnType.GetMethod("AsTask", BindingFlags.Public | BindingFlags.Instance) is MethodInfo valueTaskAsTask &&
            valueTaskAsTask.ReturnType.GetProperty("Result", BindingFlags.Public | BindingFlags.Instance)?.GetGetMethod() is MethodInfo asTaskResultGetter)
        {
            return async (functionName, result, context) =>
            {
                Task task = (Task)valueTaskAsTask.Invoke(ThrowIfNullResult(result), Array.Empty<object>());
                await task.ConfigureAwait(false);

                var taskResult = asTaskResultGetter.Invoke(task!, Array.Empty<object>());

                return new FunctionResult(functionName, context, taskResult);
            };
        }

        // IAsyncEnumerable<T>
        if (returnType.GetGenericTypeDefinition() is Type genericAsyncEnumerable && genericAsyncEnumerable == typeof(IAsyncEnumerable<>))
        {
            Type elementType = returnType.GetGenericArguments()[0];

            MethodInfo getAsyncEnumeratorMethod = typeof(IAsyncEnumerable<>)
                .MakeGenericType(elementType)
                .GetMethod("GetAsyncEnumerator");

            if (getAsyncEnumeratorMethod is not null)
            {
                return (functionName, result, context) =>
                {
                    var asyncEnumerator = getAsyncEnumeratorMethod.Invoke(result, new object[] { default(CancellationToken) });

                    return new ValueTask<FunctionResult>(new FunctionResult(functionName, context, asyncEnumerator));
                };
            }
        }

        // Unrecognized return type.
        throw GetExceptionForInvalidSignature(method, $"Unknown return type {returnType}");

        // Throws an exception if a result is found to be null unexpectedly
        static object ThrowIfNullResult(object? result) =>
            result ??
            throw new KernelException("Function returned null unexpectedly.");
    }

    /// <summary>Gets an exception that can be thrown indicating an invalid signature.</summary>
    [DoesNotReturn]
    private static Exception GetExceptionForInvalidSignature(MethodInfo method, string reason) =>
        throw new KernelException($"Function '{method.Name}' is not supported by the kernel. {reason}");

    /// <summary>Throws an exception indicating an invalid SKFunction signature if the specified condition is not met.</summary>
    private static void ThrowForInvalidSignatureIf([DoesNotReturnIf(true)] bool condition, MethodInfo method, string reason)
    {
        if (condition)
        {
            throw GetExceptionForInvalidSignature(method, reason);
        }
    }

    /// <summary>Tracks whether a particular kind of parameter has been seen, throwing an exception if it has, and marking it as seen if it hasn't</summary>
    private static void TrackUniqueParameterType(ref bool hasParameterType, MethodInfo method, string failureMessage)
    {
        ThrowForInvalidSignatureIf(hasParameterType, method, failureMessage);
        hasParameterType = true;
    }

    /// <summary>
    /// Gets a TypeConverter-based converter for converting from a variable / argument to the type of the parameter.
    /// </summary>
    /// <param name="targetType">Specifies the target type into which a string should be parsed.</param>
    /// <returns>The parsing function if the target type is supported; otherwise, null.</returns>
    /// <remarks>
    /// The parsing function uses whatever TypeConverter is registered for the target type.
    /// Parsing is first attempted using the current culture, and if that fails, it tries again
    /// with the invariant culture. If both fail, an exception is thrown.
    /// </remarks>
    private static Func<object?, CultureInfo, object?>? GetConverterFrom(Type targetType) =>
        s_parsers.GetOrAdd(targetType, static targetType =>
        {
            // For nullables, parse as the inner type.  We then just need to be careful to treat null as null,
            // as the underlying parser might not be expecting null.
            bool wasNullable = false;
            if (targetType.IsGenericType && targetType.GetGenericTypeDefinition() == typeof(Nullable<>))
            {
                wasNullable = true;
                targetType = Nullable.GetUnderlyingType(targetType);
            }

            // Finally, look up and use a type converter.  Again, special-case null if it was actually Nullable<T>.
            if (GetTypeConverter(targetType) is TypeConverter converter)
            {
                return (input, cultureInfo) =>
                {
                    if (wasNullable && input is null)
                    {
                        return null!;
                    }

                    // First try to parse using the supplied culture (or current if none was supplied).
                    // If that fails, try with the invariant culture and allow any exception to propagate.
                    try
                    {
                        return converter.ConvertFrom(context: null, cultureInfo, input);
                    }
                    catch (Exception e) when (!e.IsCriticalException() && cultureInfo != CultureInfo.InvariantCulture)
                    {
                        return converter.ConvertFrom(context: null, CultureInfo.InvariantCulture, input);
                    }
                };
            }

            // Unsupported type.
            return null;
        });

    private static TypeConverter? GetTypeConverter(Type targetType)
    {
        // In an ideal world, this would use TypeDescriptor.GetConverter. However, that is not friendly to
        // any form of ahead-of-time compilation, as it could end up requiring functionality that was trimmed.
        // Instead, we just use a hard-coded set of converters for the types we know about and then also support
        // types that are explicitly attributed with TypeConverterAttribute.

        if (targetType == typeof(string)) return new StringConverter();
        if (targetType == typeof(byte)) return new ByteConverter();
        if (targetType == typeof(sbyte)) return new SByteConverter();
        if (targetType == typeof(bool)) return new BooleanConverter();
        if (targetType == typeof(ushort)) return new UInt16Converter();
        if (targetType == typeof(short)) return new Int16Converter();
        if (targetType == typeof(char)) return new CharConverter();
        if (targetType == typeof(uint)) return new UInt32Converter();
        if (targetType == typeof(int)) return new Int32Converter();
        if (targetType == typeof(ulong)) return new UInt64Converter();
        if (targetType == typeof(long)) return new Int64Converter();
        if (targetType == typeof(float)) return new SingleConverter();
        if (targetType == typeof(double)) return new DoubleConverter();
        if (targetType == typeof(decimal)) return new DecimalConverter();
        if (targetType == typeof(TimeSpan)) return new TimeSpanConverter();
        if (targetType == typeof(DateTime)) return new DateTimeConverter();
        if (targetType == typeof(DateTimeOffset)) return new DateTimeOffsetConverter();
        if (targetType == typeof(Uri)) return new UriTypeConverter();
        if (targetType == typeof(Guid)) return new GuidConverter();
        if (targetType.IsEnum) return new EnumConverter(targetType);

        if (targetType.GetCustomAttribute<TypeConverterAttribute>() is TypeConverterAttribute tca &&
            Type.GetType(tca.ConverterTypeName, throwOnError: false) is Type converterType &&
            Activator.CreateInstance(converterType) is TypeConverter converter)
        {
            return converter;
        }

        return null;
    }

    [DebuggerBrowsable(DebuggerBrowsableState.Never)]
    private string DebuggerDisplay => $"{this.Name} ({this.Description})";

    /// <summary>
    /// Remove characters from method name that are valid in metadata but invalid for SK.
    /// </summary>
    private static string SanitizeMetadataName(string methodName) =>
        s_invalidNameCharsRegex.Replace(methodName, "_");

    /// <summary>Regex that flags any character other than ASCII digits or letters or the underscore.</summary>
    private static readonly Regex s_invalidNameCharsRegex = new("[^0-9A-Za-z_]");

    /// <summary>Parser functions for converting strings to parameter types.</summary>
    private static readonly ConcurrentDictionary<Type, Func<object?, CultureInfo, object?>?> s_parsers = new();

    private FunctionView? _view;

    #endregion
}
