// Copyright (c) Microsoft. All rights reserved.

using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics.CodeAnalysis;
using Microsoft.SemanticKernel.Diagnostics;

#pragma warning disable CA1033 // Interface methods should be callable by child types
#pragma warning disable CS8767 // Nullability of reference types in type of parameter doesn't match implicitly implemented member (possibly because of nullability attributes).
#pragma warning disable CS8769 // Nullability of reference types in type of parameter doesn't match implemented member (possibly because of nullability attributes).
#pragma warning disable IDE0130 // Using main namespace
#pragma warning disable RCS1168 // Parameter name differs from base name.
#pragma warning disable CA1725 // Parameter names should match base declaration

namespace Microsoft.SemanticKernel;

/// <summary>Provides a dictionary mapping plug-in name to the collection of functions in that plug-in.</summary>
/// <remarks>The dictionary uses ordinal ignore-case behavior for name lookups.</remarks>
public class PluginCollection :
    IDictionary<string, IDictionary<string, IKernelFunction>>,
    IReadOnlyDictionary<string, IReadOnlyDictionary<string, IKernelFunction>>
{
    private readonly Dictionary<string, IDictionary<string, IKernelFunction>> _plugins = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Gets the number of plugins in the collection.</summary>
    public int Count => this._plugins.Count;

    /// <summary>Adds a plugin to the collection.</summary>
    /// <param name="pluginName">The name of the plugin.</param>
    /// <param name="functions">The functions available from the plugin.</param>
    public void Add(string pluginName, IDictionary<string, IKernelFunction> functions)
    {
        Verify.NotNull(functions);
        this._plugins.Add(pluginName, functions);
    }

    /// <summary>Gets the functions associated with the specified plugin.</summary>
    /// <param name="pluginName">The name of the plugin.</param>
    /// <param name="functions">The functions associated with the specified plugin, if found; otherwise, null.</param>
    /// <returns>true if the plugin is in the collection; otherwise, false.</returns>
    public bool TryGetPlugin(string pluginName, [NotNullWhen(true)] out IDictionary<string, IKernelFunction>? functions) =>
        this._plugins.TryGetValue(pluginName, out functions);

    /// <summary>Gets a function from the collection.</summary>
    /// <param name="pluginName">The name of the plugin containing the function.</param>
    /// <param name="functionName">The name of the function.</param>
    /// <param name="function">The function, if found; otherwise, null.</param>
    /// <returns>true if the function was found; otherwise, false.</returns>
    public bool TryGetFunction(string pluginName, string functionName, [NotNullWhen(true)] out IKernelFunction? function)
    {
        if (this._plugins.TryGetValue(pluginName, out IDictionary<string, IKernelFunction>? functions) &&
            functions.TryGetValue(functionName, out function))
        {
            return true;
        }

        function = null;
        return false;
    }

    /// <summary>Gets or sets the functions associated with the specified plugin.</summary>
    /// <param name="pluginName">The name of the plugin.</param>
    public IDictionary<string, IKernelFunction> this[string pluginName]
    {
        get => this._plugins[pluginName];
        set => this._plugins[pluginName] = value;
    }

    /// <inheritdoc/>
    public void Clear() => this._plugins.Clear();

    /// <summary>Gets whether the collection stores the specified plugin.</summary>
    /// <param name="pluginName">The name of the plugin.</param>
    /// <returns>true if the collection stores the specified plugin; otherwise, false.</returns>
    public bool ContainsPlugin(string pluginName) => this._plugins.ContainsKey(pluginName);

    /// <summary>Removes the specified plugin from the collection.</summary>
    /// <param name="pluginName"></param>
    /// <returns>true if the plugin was in the collection and has thus been removed; false if the plugin wasn't in the collection.</returns>
    public bool Remove(string pluginName) =>
        this._plugins.Remove(pluginName);

    /// <inheritdoc/>
    IReadOnlyDictionary<string, IKernelFunction> IReadOnlyDictionary<string, IReadOnlyDictionary<string, IKernelFunction>>.this[string pluginName]
    {
        get
        {
            IDictionary<string, IKernelFunction> functions = this._plugins[pluginName];
            return
                functions as IReadOnlyDictionary<string, IKernelFunction> ??
                new ReadOnlyDictionary<string, IKernelFunction>(functions);
        }
    }

    /// <inheritdoc/>
    ICollection<string> IDictionary<string, IDictionary<string, IKernelFunction>>.Keys => this._plugins.Keys;

    /// <inheritdoc/>
    ICollection<IDictionary<string, IKernelFunction>> IDictionary<string, IDictionary<string, IKernelFunction>>.Values => this._plugins.Values;

    /// <inheritdoc/>
    IEnumerable<string> IReadOnlyDictionary<string, IReadOnlyDictionary<string, IKernelFunction>>.Keys => this._plugins.Keys;

    /// <inheritdoc/>
    IEnumerable<IReadOnlyDictionary<string, IKernelFunction>> IReadOnlyDictionary<string, IReadOnlyDictionary<string, IKernelFunction>>.Values
    {
        get
        {
            foreach (KeyValuePair<string, IDictionary<string, IKernelFunction>> pair in this._plugins)
            {
                yield return
                    pair.Value as IReadOnlyDictionary<string, IKernelFunction> ??
                    new ReadOnlyDictionary<string, IKernelFunction>(pair.Value);
            }
        }
    }

    bool ICollection<KeyValuePair<string, IDictionary<string, IKernelFunction>>>.IsReadOnly => false;

    /// <inheritdoc/>
    void ICollection<KeyValuePair<string, IDictionary<string, IKernelFunction>>>.Add(KeyValuePair<string, IDictionary<string, IKernelFunction>> item)
    {
        Verify.NotNull(item.Value);
        this._plugins.Add(item.Key, item.Value);
    }

    /// <inheritdoc/>
    bool ICollection<KeyValuePair<string, IDictionary<string, IKernelFunction>>>.Contains(KeyValuePair<string, IDictionary<string, IKernelFunction>> item) =>
        ((ICollection<KeyValuePair<string, IDictionary<string, IKernelFunction>>>)this._plugins).Contains(item);

    /// <inheritdoc/>
    bool IDictionary<string, IDictionary<string, IKernelFunction>>.ContainsKey(string key) =>
        this._plugins.ContainsKey(key);

    /// <inheritdoc/>
    bool IReadOnlyDictionary<string, IReadOnlyDictionary<string, IKernelFunction>>.ContainsKey(string key) =>
        this._plugins.ContainsKey(key);

    /// <inheritdoc/>
    void ICollection<KeyValuePair<string, IDictionary<string, IKernelFunction>>>.CopyTo(KeyValuePair<string, IDictionary<string, IKernelFunction>>[] array, int arrayIndex) =>
        ((ICollection<KeyValuePair<string, IDictionary<string, IKernelFunction>>>)this._plugins).CopyTo(array, arrayIndex);

    /// <inheritdoc/>
    IEnumerator<KeyValuePair<string, IDictionary<string, IKernelFunction>>> IEnumerable<KeyValuePair<string, IDictionary<string, IKernelFunction>>>.GetEnumerator() =>
        this._plugins.GetEnumerator();

    /// <inheritdoc/>
    bool ICollection<KeyValuePair<string, IDictionary<string, IKernelFunction>>>.Remove(KeyValuePair<string, IDictionary<string, IKernelFunction>> item) =>
        ((ICollection<KeyValuePair<string, IDictionary<string, IKernelFunction>>>)this._plugins).Remove(item);

    /// <inheritdoc/>
    bool IDictionary<string, IDictionary<string, IKernelFunction>>.TryGetValue(string key, [NotNullWhen(true)] out IDictionary<string, IKernelFunction>? value) =>
        this._plugins.TryGetValue(key, out value);

    /// <inheritdoc/>
    bool IReadOnlyDictionary<string, IReadOnlyDictionary<string, IKernelFunction>>.TryGetValue(string key, [NotNullWhen(true)] out IReadOnlyDictionary<string, IKernelFunction>? value)
    {
        if (this._plugins.TryGetValue(key, out IDictionary<string, IKernelFunction>? functions))
        {
            value = functions as IReadOnlyDictionary<string, IKernelFunction> ?? new ReadOnlyDictionary<string, IKernelFunction>(functions);
            return true;
        }

        value = null;
        return false;
    }

    /// <inheritdoc/>
    IEnumerator IEnumerable.GetEnumerator() => this._plugins.GetEnumerator();

    /// <inheritdoc/>
    IEnumerator<KeyValuePair<string, IReadOnlyDictionary<string, IKernelFunction>>> IEnumerable<KeyValuePair<string, IReadOnlyDictionary<string, IKernelFunction>>>.GetEnumerator() =>
        ((IReadOnlyCollection<KeyValuePair<string, IReadOnlyDictionary<string, IKernelFunction>>>)this._plugins).GetEnumerator();
}
