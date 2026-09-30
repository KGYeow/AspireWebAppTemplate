using System.Net.Http.Json;
using System.Text.Json;
using AspireWebAppTemplate.Application.Common;

namespace AspireWebAppTemplate.Web.Extensions;

/// <summary>
/// Extension methods that convert an <see cref="HttpResponseMessage"/> into an
/// <see cref="ApiResult"/> or <see cref="ApiResult{T}"/>, applying the standard mapping:
/// a success status (200-299) yields a success result (deserializing the body for the generic
/// overload); a non-success status yields a failure result carrying the entire response body as text.
/// </summary>
public static class HttpResponseMessageExtensions
{
    /// <summary>
    /// Maps a response to an <see cref="ApiResult{T}"/>. On success the body is deserialized to
    /// <typeparamref name="T"/>; when the deserialized value is null the supplied
    /// <paramref name="defaultValue"/> is used as <see cref="ApiResult{T}.Data"/>. On failure the
    /// entire response body is read as text and returned as the error.
    /// </summary>
    /// <typeparam name="T">The payload type to deserialize on success.</typeparam>
    /// <param name="response">The HTTP response to map.</param>
    /// <param name="defaultValue">Fallback used when the deserialized payload is null (for example an empty list).</param>
    /// <param name="options">Optional serializer options; when null the default web options are used.</param>
    /// <returns>A success result carrying the deserialized payload, or a failure result carrying the response body text.</returns>
    public static async Task<ApiResult<T>> ToApiResultAsync<T>(this HttpResponseMessage response, T? defaultValue = default, JsonSerializerOptions? options = null)
    {
        // On a success status, read the body once and deserialize it; fall back to the supplied default when null.
        if (response.IsSuccessStatusCode)
        {
            var data = await response.Content.ReadFromJsonAsync<T>(options);
            return ApiResult<T>.Success(data ?? defaultValue!);
        }

        // On a non-success status, read the body once as text and surface it as the error.
        return ApiResult<T>.Failure(await response.Content.ReadAsStringAsync());
    }

    /// <summary>
    /// Maps a response to a non-generic <see cref="ApiResult"/>: success when the status is 200-299,
    /// otherwise failure carrying the entire response body as text.
    /// </summary>
    /// <param name="response">The HTTP response to map.</param>
    /// <returns>A success result on a 200-299 status, or a failure result carrying the response body text.</returns>
    public static async Task<ApiResult> ToApiResultAsync(this HttpResponseMessage response)
    {
        // A success status maps to a data-less success result.
        if (response.IsSuccessStatusCode)
            return ApiResult.Success();

        // A non-success status reads the body once as text and surfaces it as the error.
        return ApiResult.Failure(await response.Content.ReadAsStringAsync());
    }
}
