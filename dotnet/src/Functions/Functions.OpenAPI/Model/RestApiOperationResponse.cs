// Copyright (c) Microsoft. All rights reserved.

using System.ComponentModel;

namespace Microsoft.SemanticKernel.Functions.OpenAPI.Model;

/// <summary>
/// The REST API operation response.
/// </summary>
[TypeConverterAttribute(typeof(RestApiOperationResponseConverter))]
public sealed class RestApiOperationResponse
{
    private static readonly RestApiOperationResponseConverter s_typeConverter = new();

    /// <summary>
    /// Gets the content of the response.
    /// </summary>
    public object Content { get; }

    /// <summary>
    /// Gets the content type of the response.
    /// </summary>
    public string ContentType { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="RestApiOperationResponse"/> class.
    /// </summary>
    /// <param name="content">The content of the response.</param>
    /// <param name="contentType">The content type of the response.</param>
    public RestApiOperationResponse(object content, string contentType)
    {
        this.Content = content;
        this.ContentType = contentType;
    }

    /// <summary>Gets a string representation of the contents of the response.</summary>
    public override string? ToString() => s_typeConverter.ConvertToString(this.Content);
}
