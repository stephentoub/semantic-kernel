﻿// Copyright (c) Microsoft. All rights reserved.

using System;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel.Http;

namespace Microsoft.SemanticKernel.Plugins.OpenApi;

internal static class DocumentLoader
{
    internal static async Task<string> LoadDocumentFromUriAsync(
        Uri uri,
        ILogger logger,
        HttpClient httpClient,
        AuthenticateRequestAsyncCallback? authCallback,
        string? userAgent,
        CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, uri.ToString());
        request.Headers.UserAgent.Add(ProductInfoHeaderValue.Parse(userAgent ?? HttpHeaderValues.UserAgent));

        if (authCallback is not null)
        {
            await authCallback(request, cancellationToken).ConfigureAwait(false);
        }

        logger.LogTrace("Importing document from {Uri}", uri);

        using var response = await httpClient.SendWithSuccessCheckAsync(request, cancellationToken).ConfigureAwait(false);
        return await response.Content.ReadAsStringWithExceptionMappingAsync(cancellationToken).ConfigureAwait(false);
    }

    internal static async Task<string> LoadDocumentFromFilePathAsync(
        string filePath,
        ILogger logger,
        CancellationToken cancellationToken)
    {
        var pluginJson = string.Empty;

        if (!File.Exists(filePath))
        {
            throw new FileNotFoundException($"Invalid URI. The specified path '{filePath}' does not exist.");
        }

        logger.LogTrace("Importing document from {Path}", filePath);

        using (var sr = File.OpenText(filePath))
        {
            return await sr.ReadToEndAsync(
#if NET6_0_OR_GREATER
                cancellationToken
#endif
                ).ConfigureAwait(false);
        }
    }

    internal static async Task<string> LoadDocumentFromStreamAsync(Stream stream, CancellationToken cancellationToken)
    {
        using StreamReader reader = new(stream);
        return await reader.ReadToEndAsync(
#if NET6_0_OR_GREATER
            cancellationToken
#endif
            ).ConfigureAwait(false);
    }
}
