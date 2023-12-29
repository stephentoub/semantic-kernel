﻿// Copyright (c) Microsoft. All rights reserved.

using System;
using System.IO;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.SemanticKernel.Http;
using Xunit;

namespace SemanticKernel.UnitTests.Utilities;

public sealed class HttpContentExtensionsTests : IDisposable
{
    /// <summary>
    /// An instance of HttpMessageHandlerStub class used to get access to various properties of HttpRequestMessage sent by HTTP client.
    /// </summary>
    private readonly HttpMessageHandlerStub _httpMessageHandlerStub;

    /// <summary>
    /// An instance of HttpClient class used by the tests.
    /// </summary>
    private readonly HttpClient _httpClient;

    /// <summary>
    /// Creates an instance of a <see cref="HttpClientExtensionsTests"/> class.
    /// </summary>
    public HttpContentExtensionsTests()
    {
        this._httpMessageHandlerStub = new HttpMessageHandlerStub();

        this._httpClient = new HttpClient(this._httpMessageHandlerStub);
    }

    [Fact]
    public async Task ShouldReturnHttpContentAsStringAsync()
    {
        //Arrange
        this._httpMessageHandlerStub.ResponseToReturn.Content = new StringContent("{\"details\": \"fake-response-content\"}", Encoding.UTF8, "application/json");

        using var requestMessage = new HttpRequestMessage(HttpMethod.Get, "https://fake-random-test-host");

        using var responseMessage = await this._httpClient.SendAsync(requestMessage, CancellationToken.None);

        //Act
        var result = await responseMessage.Content.ReadAsStringWithExceptionMappingAsync(CancellationToken.None);

        //Assert
        Assert.False(string.IsNullOrEmpty(result));

        Assert.Equal("{\"details\": \"fake-response-content\"}", result);
    }

    [Fact]
    public async Task ShouldReturnHttpContentAsStreamAsync()
    {
        //Arrange
        using var expectedStream = new MemoryStream(Encoding.Default.GetBytes("{\"details\": \"fake-response-content\"}"));

        this._httpMessageHandlerStub.ResponseToReturn.Content = new StreamContent(expectedStream);

        using var requestMessage = new HttpRequestMessage(HttpMethod.Get, "https://fake-random-test-host");

        using var responseMessage = await this._httpClient.SendAsync(requestMessage, CancellationToken.None);

        //Act
        var actualStream = await responseMessage.Content.ReadAsStreamAndTranslateExceptionAsync(CancellationToken.None);

        //Assert
        Assert.NotNull(actualStream);

        using var streamReader = new StreamReader(actualStream);
        var content = await streamReader.ReadToEndAsync();
        Assert.Equal("{\"details\": \"fake-response-content\"}", content);
    }

    [Fact]
    public async Task ShouldReturnHttpContentAsByteArrayAsync()
    {
        //Arrange
        this._httpMessageHandlerStub.ResponseToReturn.Content = new ByteArrayContent(new byte[] { 1, 2, 3 });

        using var requestMessage = new HttpRequestMessage(HttpMethod.Get, "https://fake-random-test-host");

        using var responseMessage = await this._httpClient.SendAsync(requestMessage, CancellationToken.None);

        //Act
        var bytes = await responseMessage.Content.ReadAsByteArrayAsync();

        //Assert
        Assert.NotNull(bytes);

        Assert.Equal(new byte[] { 1, 2, 3 }, bytes);
    }

    /// <summary>
    /// Disposes resources used by this class.
    /// </summary>
    public void Dispose()
    {
        this._httpMessageHandlerStub.Dispose();

        this._httpClient.Dispose();
    }
}
