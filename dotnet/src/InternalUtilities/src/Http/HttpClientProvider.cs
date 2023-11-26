// Copyright (c) Microsoft. All rights reserved.

using System;
using System.Net.Http;
using Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Provides functionality for retrieving instances of HttpClient.
/// </summary>
internal static class HttpClientProvider
{
    private static readonly HttpClientHandler s_sharedHandler = new();

    /// <summary>
    /// Retrieves an instance of HttpClient.
    /// </summary>
    /// <returns>An instance of HttpClient.</returns>
    public static HttpClient GetHttpClient(IServiceProvider? serviceProvider = null) =>
        serviceProvider?.GetService<HttpClient>() ??
        CreateHttpClient();

    /// <summary>
    /// Retrieves an instance of HttpClient.
    /// </summary>
    /// <returns>An instance of HttpClient.</returns>
    public static HttpClient GetHttpClient(HttpClient? httpClient) =>
        httpClient ??
        CreateHttpClient();

    public static HttpClient CreateHttpClient() => new(s_sharedHandler, disposeHandler: false);
}
