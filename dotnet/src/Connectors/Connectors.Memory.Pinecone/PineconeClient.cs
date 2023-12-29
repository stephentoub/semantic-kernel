﻿// Copyright (c) Microsoft. All rights reserved.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.SemanticKernel.Http;

namespace Microsoft.SemanticKernel.Connectors.Pinecone;

/// <summary>
/// A client for the Pinecone API
/// </summary>
public sealed class PineconeClient : IPineconeClient
{
    /// <summary>
    /// Initializes a new instance of the <see cref="PineconeClient"/> class.
    /// </summary>
    /// <param name="pineconeEnvironment">The environment for Pinecone.</param>
    /// <param name="apiKey">The API key for accessing Pinecone services.</param>
    /// <param name="loggerFactory">The <see cref="ILoggerFactory"/> to use for logging. If null, no logging will be performed.</param>
    /// <param name="httpClient">An optional HttpClient instance for making HTTP requests.</param>
    public PineconeClient(string pineconeEnvironment, string apiKey, ILoggerFactory? loggerFactory = null, HttpClient? httpClient = null)
    {
        this._pineconeEnvironment = pineconeEnvironment;
        this._authHeader = new KeyValuePair<string, string>("Api-Key", apiKey);
        this._jsonSerializerOptions = PineconeUtils.DefaultSerializerOptions;
        this._logger = loggerFactory?.CreateLogger(typeof(PineconeClient)) ?? NullLogger.Instance;
        this._httpClient = HttpClientProvider.GetHttpClient(httpClient);
        this._indexHostMapping = new ConcurrentDictionary<string, string>();
    }

    /// <inheritdoc />
    public async IAsyncEnumerable<PineconeDocument?> FetchVectorsAsync(
        string indexName,
        IEnumerable<string> ids,
        string indexNamespace = "",
        bool includeValues = false,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        this._logger.LogDebug("Searching vectors by id");

        string basePath = await this.GetVectorOperationsApiBasePathAsync(indexName).ConfigureAwait(false);

        FetchRequest fetchRequest = FetchRequest.FetchVectors(ids)
            .FromNamespace(indexNamespace);

        using HttpRequestMessage request = fetchRequest.Build();

        string? responseContent = null;

        try
        {
            (_, responseContent) = await this.ExecuteHttpRequestAsync(basePath, request, cancellationToken).ConfigureAwait(false);
        }
        catch (HttpOperationException e)
        {
            this._logger.LogError(e, "Error occurred on Get Vectors request: {Message}", e.Message);
            yield break;
        }

        FetchResponse? data = JsonSerializer.Deserialize<FetchResponse>(responseContent, this._jsonSerializerOptions);

        if (data == null)
        {
            this._logger.LogWarning("Unable to deserialize Get response");
            yield break;
        }

        if (data.Vectors.Count == 0)
        {
            this._logger.LogWarning("Vectors not found");
            yield break;
        }

        IEnumerable<PineconeDocument> records = includeValues
            ? data.Vectors.Values
            : data.WithoutEmbeddings();

        foreach (PineconeDocument? record in records)
        {
            yield return record;
        }
    }

    /// <inheritdoc />
    public async IAsyncEnumerable<PineconeDocument?> QueryAsync(
        string indexName,
        Query query,
        bool includeValues = false,
        bool includeMetadata = true,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        this._logger.LogDebug("Querying top {TopK} nearest vectors", query.TopK);

        using HttpRequestMessage request = QueryRequest.QueryIndex(query)
            .WithMetadata(includeMetadata)
            .WithEmbeddings(includeValues)
            .Build();

        string basePath = await this.GetVectorOperationsApiBasePathAsync(indexName).ConfigureAwait(false);

        string? responseContent = null;

        try
        {
            (_, responseContent) = await this.ExecuteHttpRequestAsync(basePath, request, cancellationToken).ConfigureAwait(false);
        }
        catch (HttpOperationException e)
        {
            this._logger.LogError(e, "Error occurred on Query Vectors request: {Message}", e.Message);
            yield break;
        }

        QueryResponse? queryResponse = JsonSerializer.Deserialize<QueryResponse>(responseContent, this._jsonSerializerOptions);

        if (queryResponse == null)
        {
            this._logger.LogWarning("Unable to deserialize Query response");
            yield break;
        }

        if (queryResponse.Matches.Count == 0)
        {
            this._logger.LogWarning("No matches found");
            yield break;
        }

        foreach (PineconeDocument? match in queryResponse.Matches)
        {
            yield return match;
        }
    }

    /// <inheritdoc />
    public async IAsyncEnumerable<(PineconeDocument, double)> GetMostRelevantAsync(
        string indexName,
        ReadOnlyMemory<float> vector,
        double threshold,
        int topK,
        bool includeValues,
        bool includeMetadata,
        string indexNamespace = "",
        Dictionary<string, object>? filter = default,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        this._logger.LogDebug("Searching top {TopK} nearest vectors with threshold {Threshold}", topK, threshold);

        List<(PineconeDocument document, float score)> documents = new();

        Query query = Query.Create(topK)
            .WithVector(vector)
            .InNamespace(indexNamespace)
            .WithFilter(filter);

        IAsyncEnumerable<PineconeDocument?> matches = this.QueryAsync(
            indexName, query,
            includeValues,
            includeMetadata, cancellationToken);

        await foreach (PineconeDocument? match in matches.WithCancellation(cancellationToken))
        {
            if (match == null)
            {
                continue;
            }

            if (match.Score > threshold)
            {
                documents.Add((match, match.Score ?? 0));
            }
        }

        if (documents.Count == 0)
        {
            this._logger.LogWarning("No relevant documents found");
            yield break;
        }

        // sort documents by score, and order by descending
        documents = documents.OrderByDescending(x => x.score).ToList();

        foreach ((PineconeDocument document, float score) in documents)
        {
            yield return (document, score);
        }
    }

    /// <inheritdoc />
    public async Task<int> UpsertAsync(
        string indexName,
        IEnumerable<PineconeDocument> vectors,
        string indexNamespace = "",
        CancellationToken cancellationToken = default)
    {
        this._logger.LogDebug("Upserting vectors");

        int totalUpserted = 0;
        int totalBatches = 0;

        string basePath = await this.GetVectorOperationsApiBasePathAsync(indexName).ConfigureAwait(false);
        IAsyncEnumerable<PineconeDocument> validVectors = PineconeUtils.EnsureValidMetadataAsync(vectors.ToAsyncEnumerable());

        await foreach (UpsertRequest? batch in PineconeUtils.GetUpsertBatchesAsync(validVectors, MaxBatchSize).WithCancellation(cancellationToken))
        {
            totalBatches++;

            using HttpRequestMessage request = batch.ToNamespace(indexNamespace).Build();

            string? responseContent = null;

            try
            {
                (_, responseContent) = await this.ExecuteHttpRequestAsync(basePath, request, cancellationToken).ConfigureAwait(false);
            }
            catch (HttpOperationException e)
            {
                this._logger.LogError(e, "Failed to upsert vectors {Message}", e.Message);
                throw;
            }

            UpsertResponse? data = JsonSerializer.Deserialize<UpsertResponse>(responseContent, this._jsonSerializerOptions);

            if (data == null)
            {
                this._logger.LogWarning("Unable to deserialize Upsert response");
                continue;
            }

            totalUpserted += data.UpsertedCount;

            this._logger.LogDebug("Upserted batch {Batches} with {Upserted} vectors", totalBatches, data.UpsertedCount);
        }

        this._logger.LogDebug("Upserted {Upserted} vectors in {Batches} batches", totalUpserted, totalBatches);

        return totalUpserted;
    }

    /// <inheritdoc />
    public async Task DeleteAsync(
        string indexName,
        IEnumerable<string>? ids = null,
        string indexNamespace = "",
        Dictionary<string, object>? filter = null,
        bool deleteAll = false,
        CancellationToken cancellationToken = default)
    {
        if (ids == null && string.IsNullOrEmpty(indexNamespace) && filter == null && !deleteAll)
        {
            throw new ArgumentException("Must provide at least one of ids, filter, or deleteAll");
        }

        ids = ids?.ToList();

        DeleteRequest deleteRequest = deleteAll
            ? string.IsNullOrEmpty(indexNamespace)
                ? DeleteRequest.GetDeleteAllVectorsRequest()
                : DeleteRequest.ClearNamespace(indexNamespace)
            : DeleteRequest.DeleteVectors(ids)
                .FromNamespace(indexNamespace)
                .FilterBy(filter);

        this._logger.LogDebug("Delete operation for Index {Index}: {Request}", indexName, deleteRequest.ToString());

        string basePath = await this.GetVectorOperationsApiBasePathAsync(indexName).ConfigureAwait(false);

        using HttpRequestMessage request = deleteRequest.Build();

        try
        {
            await this.ExecuteHttpRequestAsync(basePath, request, cancellationToken).ConfigureAwait(false);
        }
        catch (HttpOperationException e)
        {
            this._logger.LogError(e, "Delete operation failed: {Message}", e.Message);
            throw;
        }
    }

    /// <inheritdoc />
    public async Task UpdateAsync(string indexName, PineconeDocument document, string indexNamespace = "", CancellationToken cancellationToken = default)
    {
        this._logger.LogDebug("Updating vector: {Id}", document.Id);

        string basePath = await this.GetVectorOperationsApiBasePathAsync(indexName).ConfigureAwait(false);

        using HttpRequestMessage request = UpdateVectorRequest
            .FromPineconeDocument(document)
            .InNamespace(indexNamespace)
            .Build();

        try
        {
            await this.ExecuteHttpRequestAsync(basePath, request, cancellationToken).ConfigureAwait(false);
        }
        catch (HttpOperationException e)
        {
            this._logger.LogError(e, "Vector update for Document {Id} failed. {Message}", document.Id, e.Message);
            throw;
        }
    }

    /// <inheritdoc />
    public async Task<IndexStats?> DescribeIndexStatsAsync(
        string indexName,
        Dictionary<string, object>? filter = default,
        CancellationToken cancellationToken = default)
    {
        this._logger.LogDebug("Getting index stats for index {Index}", indexName);

        string basePath = await this.GetVectorOperationsApiBasePathAsync(indexName).ConfigureAwait(false);

        using HttpRequestMessage request = DescribeIndexStatsRequest.GetIndexStats()
            .WithFilter(filter)
            .Build();

        string? responseContent = null;

        try
        {
            (_, responseContent) = await this.ExecuteHttpRequestAsync(basePath, request, cancellationToken).ConfigureAwait(false);
        }
        catch (HttpOperationException e)
        {
            this._logger.LogError(e, "Index not found {Message}", e.Message);
            throw;
        }

        IndexStats? result = JsonSerializer.Deserialize<IndexStats>(responseContent, this._jsonSerializerOptions);

        if (result != null)
        {
            this._logger.LogDebug("Index stats retrieved");
        }
        else
        {
            this._logger.LogWarning("Index stats retrieval failed");
        }

        return result;
    }

    /// <inheritdoc />
    public async IAsyncEnumerable<string?> ListIndexesAsync([EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        using HttpRequestMessage request = ListIndexesRequest.Build();

        (HttpResponseMessage _, string responseContent) = await this.ExecuteHttpRequestAsync(this.GetIndexOperationsApiBasePath(), request, cancellationToken).ConfigureAwait(false);

        string[]? indices = JsonSerializer.Deserialize<string[]?>(responseContent, this._jsonSerializerOptions);

        if (indices == null)
        {
            yield break;
        }

        foreach (string? index in indices)
        {
            yield return index;
        }
    }

    /// <inheritdoc />
    public async Task CreateIndexAsync(IndexDefinition indexDefinition, CancellationToken cancellationToken = default)
    {
        this._logger.LogDebug("Creating index {Index}", indexDefinition.ToString());

        using HttpRequestMessage request = indexDefinition.Build();

        try
        {
            await this.ExecuteHttpRequestAsync(this.GetIndexOperationsApiBasePath(), request, cancellationToken).ConfigureAwait(false);
        }
        catch (HttpOperationException e) when (e.StatusCode == HttpStatusCode.BadRequest)
        {
            this._logger.LogError(e, "Bad Request: {StatusCode}, {Response}", e.StatusCode, e.ResponseContent);
            throw;
        }
        catch (HttpOperationException e) when (e.StatusCode == HttpStatusCode.Conflict)
        {
            this._logger.LogError(e, "Index of given name already exists: {StatusCode}, {Response}", e.StatusCode, e.ResponseContent);
            throw;
        }
        catch (HttpOperationException e)
        {
            this._logger.LogError(e, "Creating index failed: {Message}, {Response}", e.Message, e.ResponseContent);
            throw;
        }
    }

    /// <inheritdoc />
    public async Task DeleteIndexAsync(string indexName, CancellationToken cancellationToken = default)
    {
        this._logger.LogDebug("Deleting index {Index}", indexName);

        using HttpRequestMessage request = DeleteIndexRequest.Create(indexName).Build();

        try
        {
            await this.ExecuteHttpRequestAsync(this.GetIndexOperationsApiBasePath(), request, cancellationToken).ConfigureAwait(false);
        }
        catch (HttpOperationException e) when (e.StatusCode == HttpStatusCode.NotFound)
        {
            this._logger.LogError(e, "Index Not Found: {StatusCode}, {Response}", e.StatusCode, e.ResponseContent);
            throw;
        }
        catch (HttpOperationException e)
        {
            this._logger.LogError(e, "Deleting index failed: {Message}, {Response}", e.Message, e.ResponseContent);
            throw;
        }

        this._logger.LogDebug("Index: {Index} has been successfully deleted.", indexName);
    }

    /// <inheritdoc />
    public async Task<bool> DoesIndexExistAsync(string indexName, CancellationToken cancellationToken = default)
    {
        this._logger.LogDebug("Checking for index {Index}", indexName);

        List<string?>? indexNames = await this.ListIndexesAsync(cancellationToken).ToListAsync(cancellationToken).ConfigureAwait(false);

        if (indexNames == null || !indexNames.Any(name => name == indexName))
        {
            return false;
        }

        PineconeIndex? index = await this.DescribeIndexAsync(indexName, cancellationToken).ConfigureAwait(false);

        return index != null && index.Status.State == IndexState.Ready;
    }

    /// <inheritdoc />
    public async Task<PineconeIndex?> DescribeIndexAsync(string indexName, CancellationToken cancellationToken = default)
    {
        this._logger.LogDebug("Getting Description for Index: {Index}", indexName);

        using HttpRequestMessage request = DescribeIndexRequest.Create(indexName).Build();

        string? responseContent = null;

        try
        {
            (_, responseContent) = await this.ExecuteHttpRequestAsync(this.GetIndexOperationsApiBasePath(), request, cancellationToken).ConfigureAwait(false);
        }
        catch (HttpOperationException e) when (e.StatusCode == HttpStatusCode.BadRequest)
        {
            this._logger.LogError(e, "Bad Request: {StatusCode}, {Response}", e.StatusCode, e.ResponseContent);
            throw;
        }
        catch (HttpOperationException e)
        {
            this._logger.LogError(e, "Describe index failed: {Message}, {Response}", e.Message, e.ResponseContent);
            throw;
        }

        PineconeIndex? indexDescription = JsonSerializer.Deserialize<PineconeIndex>(responseContent, this._jsonSerializerOptions);

        if (indexDescription == null)
        {
            this._logger.LogDebug("Deserialized index description is null");
        }

        return indexDescription;
    }

    /// <inheritdoc />
    public async Task ConfigureIndexAsync(string indexName, int replicas = 1, PodType podType = PodType.P1X1, CancellationToken cancellationToken = default)
    {
        this._logger.LogDebug("Configuring index {Index}", indexName);

        using HttpRequestMessage request = ConfigureIndexRequest
            .Create(indexName)
            .WithPodType(podType)
            .NumberOfReplicas(replicas)
            .Build();

        try
        {
            await this.ExecuteHttpRequestAsync(this.GetIndexOperationsApiBasePath(), request, cancellationToken).ConfigureAwait(false);
        }
        catch (HttpOperationException e) when (e.StatusCode == HttpStatusCode.BadRequest)
        {
            this._logger.LogError(e, "Request exceeds quota or collection name is invalid. {Index}", indexName);
            throw;
        }
        catch (HttpOperationException e) when (e.StatusCode == HttpStatusCode.NotFound)
        {
            this._logger.LogError(e, "Index not found. {Index}", indexName);
            throw;
        }
        catch (HttpOperationException e)
        {
            this._logger.LogError(e, "Index configuration failed: {Message}, {Response}", e.Message, e.ResponseContent);
            throw;
        }

        this._logger.LogDebug("Collection created. {Index}", indexName);
    }

    #region private ================================================================================

    private readonly string _pineconeEnvironment;
    private readonly ILogger _logger;
    private readonly HttpClient _httpClient;

    private readonly KeyValuePair<string, string> _authHeader;
    private readonly JsonSerializerOptions _jsonSerializerOptions;
    private readonly ConcurrentDictionary<string, string> _indexHostMapping;
    private const int MaxBatchSize = 100;

    private async Task<string> GetVectorOperationsApiBasePathAsync(string indexName)
    {
        string indexHost = await this.GetIndexHostAsync(indexName).ConfigureAwait(false);

        return $"https://{indexHost}";
    }

    private string GetIndexOperationsApiBasePath()
    {
        return $"https://controller.{this._pineconeEnvironment}.pinecone.io";
    }

    private async Task<(HttpResponseMessage response, string responseContent)> ExecuteHttpRequestAsync(
        string baseURL,
        HttpRequestMessage request,
        CancellationToken cancellationToken = default)
    {
        request.Headers.Add(this._authHeader.Key, this._authHeader.Value);
        request.RequestUri = new Uri(baseURL + request.RequestUri);

        using HttpResponseMessage response = await this._httpClient.SendWithSuccessCheckAsync(request, cancellationToken).ConfigureAwait(false);

        string responseContent = await response.Content.ReadAsStringWithExceptionMappingAsync(cancellationToken).ConfigureAwait(false);

        return (response, responseContent);
    }

    private async Task<string> GetIndexHostAsync(string indexName, CancellationToken cancellationToken = default)
    {
        if (this._indexHostMapping.TryGetValue(indexName, out string? indexHost))
        {
            return indexHost;
        }

        this._logger.LogDebug("Getting index host from Pinecone.");

        PineconeIndex? pineconeIndex = await this.DescribeIndexAsync(indexName, cancellationToken).ConfigureAwait(false);

        if (pineconeIndex == null)
        {
            throw new KernelException("Index not found in Pinecone. Create index to perform operations with vectors.");
        }

        if (string.IsNullOrWhiteSpace(pineconeIndex.Status.Host))
        {
            throw new KernelException($"Host of index {indexName} is unknown.");
        }

        this._logger.LogDebug("Found host {Host} for index {Index}", pineconeIndex.Status.Host, indexName);

        this._indexHostMapping.TryAdd(indexName, pineconeIndex.Status.Host);

        return pineconeIndex.Status.Host;
    }

    #endregion
}
