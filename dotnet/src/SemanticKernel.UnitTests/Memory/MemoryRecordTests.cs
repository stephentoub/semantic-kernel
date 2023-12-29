﻿// Copyright (c) Microsoft. All rights reserved.

using System;
using System.Text.Json;
using Microsoft.SemanticKernel.Memory;
using Xunit;

namespace SemanticKernel.UnitTests.Memory;

public class MemoryRecordTests
{
    private readonly string _id = "Id";
    private readonly string _text = "text";
    private readonly string _description = "description";
    private readonly string _externalSourceName = "externalSourceName";
    private readonly string _additionalMetadata = "value";
    private readonly ReadOnlyMemory<float> _embedding = new(new float[] { 1, 2, 3 });

    [Fact]
    public void ItCanBeConstructedFromMetadataAndVector()
    {
        // Arrange
        var metadata = new MemoryRecordMetadata(
            isReference: false,
            id: this._id,
            text: this._text,
            description: this._description,
            externalSourceName: this._externalSourceName,
            additionalMetadata: this._additionalMetadata);

        // Act
        var memoryRecord = new MemoryRecord(metadata, this._embedding, "key", DateTimeOffset.Now);

        // Assert
        Assert.False(memoryRecord.Metadata.IsReference);
        Assert.Equal(this._id, memoryRecord.Metadata.Id);
        Assert.Equal(this._text, memoryRecord.Metadata.Text);
        Assert.Equal(this._description, memoryRecord.Metadata.Description);
        Assert.Equal(this._externalSourceName, memoryRecord.Metadata.ExternalSourceName);
        Assert.True(this._embedding.Span.SequenceEqual(memoryRecord.Embedding.Span));
    }

    [Fact]
    public void ItCanBeCreatedToRepresentLocalData()
    {
        // Arrange
        var memoryRecord = MemoryRecord.LocalRecord(
            id: this._id,
            text: this._text,
            description: this._description,
            embedding: this._embedding);

        // Assert
        Assert.False(memoryRecord.Metadata.IsReference);
        Assert.Equal(this._id, memoryRecord.Metadata.Id);
        Assert.Equal(this._text, memoryRecord.Metadata.Text);
        Assert.Equal(this._description, memoryRecord.Metadata.Description);
        Assert.Equal(string.Empty, memoryRecord.Metadata.ExternalSourceName);
        Assert.True(this._embedding.Span.SequenceEqual(memoryRecord.Embedding.Span));
    }

    [Fact]
    public void ItCanBeCreatedToRepresentExternalData()
    {
        // Arrange
        var memoryRecord = MemoryRecord.ReferenceRecord(
            externalId: this._id,
            sourceName: this._externalSourceName,
            description: this._description,
            embedding: this._embedding);

        // Assert
        Assert.True(memoryRecord.Metadata.IsReference);
        Assert.Equal(this._id, memoryRecord.Metadata.Id);
        Assert.Equal(string.Empty, memoryRecord.Metadata.Text);
        Assert.Equal(this._description, memoryRecord.Metadata.Description);
        Assert.Equal(this._externalSourceName, memoryRecord.Metadata.ExternalSourceName);
        Assert.True(this._embedding.Span.SequenceEqual(memoryRecord.Embedding.Span));
    }

    [Fact]
    public void ItCanBeCreatedFromSerializedMetadata()
    {
        // Arrange
        string jsonString = @"{
            ""is_reference"": false,
            ""id"": ""Id"",
            ""text"": ""text"",
            ""description"": ""description"",
            ""external_source_name"": ""externalSourceName"",
            ""additional_metadata"": ""value""
        }";

        // Act
        var memoryRecord = MemoryRecord.FromJsonMetadata(jsonString, this._embedding);

        // Assert
        Assert.False(memoryRecord.Metadata.IsReference);
        Assert.Equal(this._id, memoryRecord.Metadata.Id);
        Assert.Equal(this._text, memoryRecord.Metadata.Text);
        Assert.Equal(this._description, memoryRecord.Metadata.Description);
        Assert.Equal(this._externalSourceName, memoryRecord.Metadata.ExternalSourceName);
        Assert.Equal(this._additionalMetadata, memoryRecord.Metadata.AdditionalMetadata);
        Assert.True(this._embedding.Span.SequenceEqual(memoryRecord.Embedding.Span));
    }

    [Fact]
    public void ItCanBeDeserializedFromJson()
    {
        // Arrange
        string jsonString = @"{
            ""metadata"": {
                ""is_reference"": false,
                ""id"": ""Id"",
                ""text"": ""text"",
                ""description"": ""description"",
                ""external_source_name"": ""externalSourceName"",
                ""additional_metadata"": ""value""
            },
            ""embedding"":
            [
                1,
                2,
                3
            ]
        }";

        // Act
        var memoryRecord = JsonSerializer.Deserialize<MemoryRecord>(jsonString);

        // Assert
        Assert.NotNull(memoryRecord);
        Assert.False(memoryRecord.Metadata.IsReference);
        Assert.Equal(this._id, memoryRecord.Metadata.Id);
        Assert.Equal(this._text, memoryRecord.Metadata.Text);
        Assert.Equal(this._description, memoryRecord.Metadata.Description);
        Assert.Equal(this._externalSourceName, memoryRecord.Metadata.ExternalSourceName);
        Assert.Equal(this._externalSourceName, memoryRecord.Metadata.ExternalSourceName);
        Assert.True(this._embedding.Span.SequenceEqual(memoryRecord.Embedding.Span));
    }

    [Fact]
    public void ItCanBeSerialized()
    {
        // Arrange
        string jsonString = @"{
            ""embedding"":
            [
                1,
                2,
                3
            ],
            ""metadata"": {
                ""is_reference"": false,
                ""external_source_name"": ""externalSourceName"",
                ""id"": ""Id"",
                ""description"": ""description"",
                ""text"": ""text"",
                ""additional_metadata"": ""value""
            },
            ""key"": ""key"",
            ""timestamp"": null
        }";
        var metadata = new MemoryRecordMetadata(
            isReference: false,
            id: this._id,
            text: this._text,
            description: this._description,
            externalSourceName: this._externalSourceName,
            additionalMetadata: this._additionalMetadata);
        var memoryRecord = new MemoryRecord(metadata, this._embedding, "key");

        // Act
        string serializedRecord = JsonSerializer.Serialize(memoryRecord);
#pragma warning disable CA1307 // Specify StringComparison for clarity; overload not available on .NET Framework
        jsonString = jsonString.Replace("\n", string.Empty);
        jsonString = jsonString.Replace(" ", string.Empty);
#pragma warning restore CA1307

        // Assert
        Assert.Equal(jsonString, serializedRecord);
    }

    [Fact]
    public void ItsMetadataCanBeSerialized()
    {
        // Arrange
        string jsonString = @"{
                ""is_reference"": false,
                ""external_source_name"": ""externalSourceName"",
                ""id"": ""Id"",
                ""description"": ""description"",
                ""text"": ""text"",
                ""additional_metadata"": ""value""
            }";

        var metadata = new MemoryRecordMetadata(
            isReference: false,
            id: this._id,
            text: this._text,
            description: this._description,
            externalSourceName: this._externalSourceName,
            additionalMetadata: this._additionalMetadata);
        var memoryRecord = new MemoryRecord(metadata, this._embedding, "key");

        // Act
        string serializedMetadata = memoryRecord.GetSerializedMetadata();
#pragma warning disable CA1307 // Specify StringComparison for clarity; overload not available on .NET Framework
        jsonString = jsonString.Replace("\n", string.Empty);
        jsonString = jsonString.Replace(" ", string.Empty);
#pragma warning restore CA1307

        // Assert
        Assert.Equal(jsonString, serializedMetadata);
    }
}
