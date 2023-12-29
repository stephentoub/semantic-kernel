﻿// Copyright (c) Microsoft. All rights reserved.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Numerics.Tensors;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using Microsoft.SemanticKernel.Memory;
using Microsoft.SemanticKernel.Text;

namespace Microsoft.SemanticKernel.Connectors.Sqlite;

/// <summary>
/// An implementation of <see cref="IMemoryStore"/> backed by a SQLite database.
/// </summary>
/// <remarks>The data is saved to a database file, specified in the constructor.
/// The data persists between subsequent instances. Only one instance may access the file at a time.
/// The caller is responsible for deleting the file.</remarks>
public class SqliteMemoryStore : IMemoryStore, IDisposable
{
    /// <summary>
    /// Connect a Sqlite database
    /// </summary>
    /// <param name="filename">Path to the database file. If file does not exist, it will be created.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> to monitor for cancellation requests. The default is <see cref="CancellationToken.None"/>.</param>
    public static async Task<SqliteMemoryStore> ConnectAsync(string filename,
        CancellationToken cancellationToken = default)
    {
        var memoryStore = new SqliteMemoryStore(filename);
        await memoryStore._dbConnection.OpenAsync(cancellationToken).ConfigureAwait(false);
        await Database.CreateTableAsync(memoryStore._dbConnection, cancellationToken).ConfigureAwait(false);
        return memoryStore;
    }

    /// <inheritdoc/>
    public async Task CreateCollectionAsync(string collectionName, CancellationToken cancellationToken = default)
    {
        await Database.CreateCollectionAsync(this._dbConnection, collectionName, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async Task<bool> DoesCollectionExistAsync(string collectionName, CancellationToken cancellationToken = default)
    {
        return await Database.DoesCollectionExistsAsync(this._dbConnection, collectionName, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async IAsyncEnumerable<string> GetCollectionsAsync([EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        await foreach (var collection in Database.GetCollectionsAsync(this._dbConnection, cancellationToken))
        {
            yield return collection;
        }
    }

    /// <inheritdoc/>
    public async Task DeleteCollectionAsync(string collectionName, CancellationToken cancellationToken = default)
    {
        await Database.DeleteCollectionAsync(this._dbConnection, collectionName, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async Task<string> UpsertAsync(string collectionName, MemoryRecord record, CancellationToken cancellationToken = default)
    {
        return await InternalUpsertAsync(this._dbConnection, collectionName, record, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async IAsyncEnumerable<string> UpsertBatchAsync(string collectionName, IEnumerable<MemoryRecord> records,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        foreach (var record in records)
        {
            yield return await InternalUpsertAsync(this._dbConnection, collectionName, record, cancellationToken).ConfigureAwait(false);
        }
    }

    /// <inheritdoc/>
    public async Task<MemoryRecord?> GetAsync(string collectionName, string key, bool withEmbedding = false, CancellationToken cancellationToken = default)
    {
        return await InternalGetAsync(this._dbConnection, collectionName, key, withEmbedding, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async IAsyncEnumerable<MemoryRecord> GetBatchAsync(string collectionName, IEnumerable<string> keys, bool withEmbeddings = false,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        foreach (var key in keys)
        {
            var result = await InternalGetAsync(this._dbConnection, collectionName, key, withEmbeddings, cancellationToken).ConfigureAwait(false);
            if (result != null)
            {
                yield return result;
            }
            else
            {
                yield break;
            }
        }
    }

    /// <inheritdoc/>
    public async Task RemoveAsync(string collectionName, string key, CancellationToken cancellationToken = default)
    {
        await Database.DeleteAsync(this._dbConnection, collectionName, key, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async Task RemoveBatchAsync(string collectionName, IEnumerable<string> keys, CancellationToken cancellationToken = default)
    {
        await Task.WhenAll(keys.Select(k => Database.DeleteAsync(this._dbConnection, collectionName, k, cancellationToken))).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async IAsyncEnumerable<(MemoryRecord, double)> GetNearestMatchesAsync(
        string collectionName,
        ReadOnlyMemory<float> embedding,
        int limit,
        double minRelevanceScore = 0,
        bool withEmbeddings = false,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        if (limit <= 0)
        {
            yield break;
        }

        var collectionMemories = new List<MemoryRecord>();
        List<(MemoryRecord Record, double Score)> embeddings = new();

        await foreach (var record in this.GetAllAsync(collectionName, cancellationToken))
        {
            if (record != null)
            {
                double similarity = TensorPrimitives.CosineSimilarity(embedding.Span, record.Embedding.Span);
                if (similarity >= minRelevanceScore)
                {
                    var entry = withEmbeddings ? record : MemoryRecord.FromMetadata(record.Metadata, ReadOnlyMemory<float>.Empty, record.Key, record.Timestamp);
                    embeddings.Add(new(entry, similarity));
                }
            }
        }

        foreach (var item in embeddings.OrderByDescending(l => l.Score).Take(limit))
        {
            yield return (item.Record, item.Score);
        }
    }

    /// <inheritdoc/>
    public async Task<(MemoryRecord, double)?> GetNearestMatchAsync(string collectionName, ReadOnlyMemory<float> embedding, double minRelevanceScore = 0, bool withEmbedding = false,
        CancellationToken cancellationToken = default)
    {
        return await this.GetNearestMatchesAsync(
            collectionName: collectionName,
            embedding: embedding,
            limit: 1,
            minRelevanceScore: minRelevanceScore,
            withEmbeddings: withEmbedding,
            cancellationToken: cancellationToken).FirstOrDefaultAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        this.Dispose(true);
        GC.SuppressFinalize(this);
    }

    #region protected ================================================================================
    /// <summary>
    /// Disposes the resources used by the <see cref="SqliteMemoryStore"/> instance.
    /// </summary>
    /// <param name="disposing">True to release both managed and unmanaged resources; false to release only unmanaged resources.</param>
    protected virtual void Dispose(bool disposing)
    {
        if (!this._disposedValue)
        {
            if (disposing)
            {
                this._dbConnection.Close();
                SqliteConnection.ClearAllPools();
                this._dbConnection.Dispose();
            }

            this._disposedValue = true;
        }
    }

    #endregion

    #region private ================================================================================

    private readonly SqliteConnection _dbConnection;
    private bool _disposedValue;

    /// <summary>
    /// Constructor
    /// </summary>
    /// <param name="filename">Sqlite db filename.</param>
    private SqliteMemoryStore(string filename)
    {
        this._dbConnection = new SqliteConnection($"Data Source={filename};");
        this._disposedValue = false;
    }

    private static string? ToTimestampString(DateTimeOffset? timestamp)
    {
        return timestamp?.ToString("u", CultureInfo.InvariantCulture);
    }

    private static DateTimeOffset? ParseTimestamp(string? str)
    {
        if (!string.IsNullOrEmpty(str)
            && DateTimeOffset.TryParse(str, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out DateTimeOffset timestamp))
        {
            return timestamp;
        }

        return null;
    }

    private async IAsyncEnumerable<MemoryRecord> GetAllAsync(string collectionName, [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        // delete empty entry in the database if it exists (see CreateCollection)
        await Database.DeleteEmptyAsync(this._dbConnection, collectionName, cancellationToken).ConfigureAwait(false);

        await foreach (DatabaseEntry dbEntry in Database.ReadAllAsync(this._dbConnection, collectionName, cancellationToken))
        {
            ReadOnlyMemory<float> vector = JsonSerializer.Deserialize<ReadOnlyMemory<float>>(dbEntry.EmbeddingString, JsonOptionsCache.Default);

            var record = MemoryRecord.FromJsonMetadata(dbEntry.MetadataString, vector, dbEntry.Key, ParseTimestamp(dbEntry.Timestamp));

            yield return record;
        }
    }

    private static async Task<string> InternalUpsertAsync(SqliteConnection connection, string collectionName, MemoryRecord record, CancellationToken cancellationToken)
    {
        record.Key = record.Metadata.Id;

        // Update
        await Database.UpdateAsync(
            conn: connection,
            collection: collectionName,
            key: record.Key,
            metadata: record.GetSerializedMetadata(),
            embedding: JsonSerializer.Serialize(record.Embedding, JsonOptionsCache.Default),
            timestamp: ToTimestampString(record.Timestamp),
            cancellationToken: cancellationToken).ConfigureAwait(false);

        // Insert if entry does not exists
        await Database.InsertOrIgnoreAsync(
            conn: connection,
            collection: collectionName,
            key: record.Key,
            metadata: record.GetSerializedMetadata(),
            embedding: JsonSerializer.Serialize(record.Embedding, JsonOptionsCache.Default),
            timestamp: ToTimestampString(record.Timestamp),
            cancellationToken: cancellationToken).ConfigureAwait(false);

        return record.Key;
    }

    private static async Task<MemoryRecord?> InternalGetAsync(
        SqliteConnection connection,
        string collectionName,
        string key, bool withEmbedding,
        CancellationToken cancellationToken)
    {
        DatabaseEntry? entry = await Database.ReadAsync(connection, collectionName, key, cancellationToken).ConfigureAwait(false);

        if (!entry.HasValue) { return null; }

        if (withEmbedding)
        {
            return MemoryRecord.FromJsonMetadata(
                json: entry.Value.MetadataString,
                JsonSerializer.Deserialize<ReadOnlyMemory<float>>(entry.Value.EmbeddingString, JsonOptionsCache.Default),
                entry.Value.Key,
                ParseTimestamp(entry.Value.Timestamp));
        }

        return MemoryRecord.FromJsonMetadata(
            json: entry.Value.MetadataString,
            ReadOnlyMemory<float>.Empty,
            entry.Value.Key,
            ParseTimestamp(entry.Value.Timestamp));
    }

    #endregion
}
