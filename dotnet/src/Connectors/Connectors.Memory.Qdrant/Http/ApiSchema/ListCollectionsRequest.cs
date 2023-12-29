﻿// Copyright (c) Microsoft. All rights reserved.

using System.Net.Http;

namespace Microsoft.SemanticKernel.Connectors.Qdrant;

internal static class ListCollectionsRequest
{
    public static HttpRequestMessage Build() => HttpRequest.CreateGetRequest("collections");
}
