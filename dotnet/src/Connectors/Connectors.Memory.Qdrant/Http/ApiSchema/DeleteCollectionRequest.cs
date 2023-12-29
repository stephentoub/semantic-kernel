﻿// Copyright (c) Microsoft. All rights reserved.

using System.Net.Http;

namespace Microsoft.SemanticKernel.Connectors.Qdrant;

internal sealed class DeleteCollectionRequest
{
    public static DeleteCollectionRequest Create(string collectionName)
    {
        return new DeleteCollectionRequest(collectionName);
    }

    public HttpRequestMessage Build()
    {
        Verify.NotNullOrWhiteSpace(this._collectionName, "collectionName");
        return HttpRequest.CreateDeleteRequest($"collections/{this._collectionName}?timeout=30");
    }

    #region private ================================================================================

    private readonly string _collectionName;

    private DeleteCollectionRequest(string collectionName)
    {
        this._collectionName = collectionName;
    }

    #endregion
}
