// Feature: reusable-api-infrastructure, Property 3: Response mapping matches the current inline branch
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using AspireWebAppTemplate.Application.Common;
using AspireWebAppTemplate.Web.Extensions;
using FsCheck;
using FsCheck.Fluent;
using FsCheck.Xunit;

namespace AspireWebAppTemplate.Tests.ReusableApiInfrastructure;

/// <summary>
/// Property-based tests verifying that <see cref="HttpResponseMessageExtensions.ToApiResultAsync{T}"/>
/// and <see cref="HttpResponseMessageExtensions.ToApiResultAsync(HttpResponseMessage)"/> produce
/// <see cref="ApiResult"/> / <see cref="ApiResult{T}"/> values whose <c>Succeeded</c>, <c>Error</c>,
/// and <c>Data</c> match a reference implementation that mirrors the success/failure branch the typed
/// API clients apply by hand.
/// </summary>
/// <remarks>
/// The reference implementation reproduces the current per-method mapping: on a 200-299 status the
/// body is deserialized (using the same web serializer defaults) and, when the deserialized value is
/// null, the supplied fallback default is substituted; on a non-success status the entire body is read
/// as text and returned as the failure error. Coverage spans a reference-type payload (with a
/// null-body fallback case) and the value-type <c>int</c> payload used by <c>GetUnreadCountAsync</c>.
/// **Validates: Requirements 2.2, 2.3, 2.4, 2.5, 2.6, 2.8, 4.7**
/// </remarks>
public class HttpResponseMappingTests
{
    #region Serializer Options

    /// <summary>
    /// The serializer options used by both the helper (via its <c>null</c> default) and the reference
    /// implementation, matching the web defaults that <c>ReadFromJsonAsync</c> applies.
    /// </summary>
    private static readonly JsonSerializerOptions WebOptions = new(JsonSerializerDefaults.Web);

    #endregion

    #region Payload Model

    /// <summary>
    /// A small reference-type payload used to exercise deserialization on success responses.
    /// </summary>
    private sealed class SamplePayload
    {
        /// <summary>The payload identifier.</summary>
        public int Id { get; set; }

        /// <summary>The payload name.</summary>
        public string? Name { get; set; }
    }

    #endregion

    #region Reference Implementation

    /// <summary>
    /// Reproduces the current inline success/failure branch for a value-returning mapping: on a success
    /// status deserialize the body and fall back to <paramref name="defaultValue"/> when null; on a
    /// non-success status read the whole body as text and return a failure carrying it.
    /// </summary>
    private static async Task<ApiResult<T>> ReferenceToApiResultAsync<T>(
        HttpResponseMessage response, T? defaultValue)
    {
        if (response.IsSuccessStatusCode)
        {
            var data = await response.Content.ReadFromJsonAsync<T>(WebOptions);
            return ApiResult<T>.Success(data ?? defaultValue!);
        }

        return ApiResult<T>.Failure(await response.Content.ReadAsStringAsync());
    }

    /// <summary>
    /// Reproduces the current inline success/failure branch for a no-data mapping.
    /// </summary>
    private static async Task<ApiResult> ReferenceToApiResultAsync(HttpResponseMessage response)
    {
        if (response.IsSuccessStatusCode)
            return ApiResult.Success();

        return ApiResult.Failure(await response.Content.ReadAsStringAsync());
    }

    #endregion

    #region Helpers

    /// <summary>
    /// Builds an <see cref="HttpResponseMessage"/> with the given status and a JSON string body.
    /// </summary>
    private static HttpResponseMessage BuildJsonResponse(HttpStatusCode status, string json) =>
        new(status)
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json")
        };

    /// <summary>
    /// Builds an <see cref="HttpResponseMessage"/> with the given status and a plain-text body.
    /// </summary>
    private static HttpResponseMessage BuildTextResponse(HttpStatusCode status, string body) =>
        new(status)
        {
            Content = new StringContent(body, Encoding.UTF8, "text/plain")
        };

    #endregion

    #region Properties

    /// <summary>
    /// Property: for a success or failure status paired with a reference-type payload (including a
    /// null body that triggers the default fallback) or an arbitrary failure body, the helper's
    /// <c>(Succeeded, Error, Data)</c> equals the reference implementation's.
    /// **Validates: Requirements 2.2, 2.3, 2.4, 2.5, 2.8, 4.7**
    /// </summary>
    [Property(MaxTest = 2)]
    public FsCheck.Property ReferenceType_Mapping_MatchesReferenceImplementation()
    {
        // Success statuses (200-299) and failure statuses (outside 200-299).
        var successStatusGen = Gen.Elements(
            HttpStatusCode.OK, HttpStatusCode.Created, HttpStatusCode.Accepted, HttpStatusCode.NoContent);
        var failureStatusGen = Gen.Elements(
            HttpStatusCode.BadRequest, HttpStatusCode.NotFound,
            HttpStatusCode.InternalServerError, HttpStatusCode.Forbidden);

        // Success bodies: a real payload, an explicit JSON null (fallback case), and an empty-name payload.
        var idGen = Gen.Elements(0, 1, 42, -7);
        var nameGen = Gen.Elements("alpha", "beta with space", "quote\"inside", "");
        var payloadJsonGen = from id in idGen
                             from name in nameGen
                             select JsonSerializer.Serialize(new SamplePayload { Id = id, Name = name }, WebOptions);
        var successBodyGen = Gen.OneOf(payloadJsonGen, Gen.Constant("null"));

        // Failure bodies: arbitrary text the server might return.
        var failureBodyGen = Gen.Elements(
            "Something went wrong.", "Role 'x' was not found.", "", "  ", "{\"detail\":\"bad\"}");

        // Fallback defaults: null (mirrors the "!" behavior) or a supplied instance (mirrors "?? new()").
        var defaultGen = Gen.Elements<SamplePayload?>(
            null, new SamplePayload { Id = 999, Name = "fallback" });

        var successGen = from status in successStatusGen
                         from body in successBodyGen
                         from def in defaultGen
                         select (IsSuccess: true, Status: status, Body: body, Default: def);

        var failureGen = from status in failureStatusGen
                         from body in failureBodyGen
                         from def in defaultGen
                         select (IsSuccess: false, Status: status, Body: body, Default: def);

        var inputGen = Gen.OneOf(successGen, failureGen);

        return Prop.ForAll(Arb.From(inputGen), input =>
        {
            var actualResponse = input.IsSuccess
                ? BuildJsonResponse(input.Status, input.Body)
                : BuildTextResponse(input.Status, input.Body);
            var referenceResponse = input.IsSuccess
                ? BuildJsonResponse(input.Status, input.Body)
                : BuildTextResponse(input.Status, input.Body);

            var actual = actualResponse.ToApiResultAsync(input.Default).GetAwaiter().GetResult();
            var expected = ReferenceToApiResultAsync(referenceResponse, input.Default).GetAwaiter().GetResult();

            var succeededMatch = actual.Succeeded == expected.Succeeded;
            var errorMatch = actual.Error == expected.Error;
            var dataMatch = SamplePayloadEquals(actual.Data, expected.Data);

            return (succeededMatch && errorMatch && dataMatch)
                .Label($"status={input.Status}, isSuccess={input.IsSuccess}, " +
                       $"succeededMatch={succeededMatch}, errorMatch={errorMatch}, dataMatch={dataMatch}");
        });
    }

    /// <summary>
    /// Property: for the value-type <c>int</c> payload (as used by <c>GetUnreadCountAsync</c>), the
    /// helper's <c>(Succeeded, Error, Data)</c> equals the reference implementation's for both success
    /// and failure statuses.
    /// **Validates: Requirements 2.2, 2.4, 2.6, 2.8, 4.7**
    /// </summary>
    [Property(MaxTest = 2)]
    public FsCheck.Property ValueType_Mapping_MatchesReferenceImplementation()
    {
        var successStatusGen = Gen.Elements(HttpStatusCode.OK, HttpStatusCode.Accepted);
        var failureStatusGen = Gen.Elements(
            HttpStatusCode.BadRequest, HttpStatusCode.NotFound, HttpStatusCode.InternalServerError);

        var countGen = Gen.Elements(0, 1, 5, 1000, -3);
        var failureBodyGen = Gen.Elements("boom", "", "unauthorized", "{\"detail\":\"nope\"}");

        var successGen = from status in successStatusGen
                         from count in countGen
                         select (IsSuccess: true, Status: status, Body: count.ToString(), ExpectedCount: count);

        var failureGen = from status in failureStatusGen
                         from body in failureBodyGen
                         select (IsSuccess: false, Status: status, Body: body, ExpectedCount: 0);

        var inputGen = Gen.OneOf(successGen, failureGen);

        return Prop.ForAll(Arb.From(inputGen), input =>
        {
            var actualResponse = input.IsSuccess
                ? BuildJsonResponse(input.Status, input.Body)
                : BuildTextResponse(input.Status, input.Body);
            var referenceResponse = input.IsSuccess
                ? BuildJsonResponse(input.Status, input.Body)
                : BuildTextResponse(input.Status, input.Body);

            // int cannot be null, so no defaultValue is supplied (Requirement 2.6).
            var actual = actualResponse.ToApiResultAsync<int>().GetAwaiter().GetResult();
            var expected = ReferenceToApiResultAsync<int>(referenceResponse, default).GetAwaiter().GetResult();

            var succeededMatch = actual.Succeeded == expected.Succeeded;
            var errorMatch = actual.Error == expected.Error;
            var dataMatch = actual.Data == expected.Data;

            return (succeededMatch && errorMatch && dataMatch)
                .Label($"status={input.Status}, isSuccess={input.IsSuccess}, " +
                       $"actualData={actual.Data}, expectedData={expected.Data}");
        });
    }

    /// <summary>
    /// Property: the non-generic <see cref="HttpResponseMessageExtensions.ToApiResultAsync(HttpResponseMessage)"/>
    /// mapping (no data) matches the reference implementation for both success and failure statuses.
    /// **Validates: Requirements 2.3, 2.4, 2.8, 4.7**
    /// </summary>
    [Property(MaxTest = 2)]
    public FsCheck.Property NonGeneric_Mapping_MatchesReferenceImplementation()
    {
        var successStatusGen = Gen.Elements(
            HttpStatusCode.OK, HttpStatusCode.Created, HttpStatusCode.NoContent);
        var failureStatusGen = Gen.Elements(
            HttpStatusCode.BadRequest, HttpStatusCode.NotFound, HttpStatusCode.InternalServerError);
        var failureBodyGen = Gen.Elements("failed", "", "  ", "Cannot delete system role.");

        var successGen = from status in successStatusGen
                         select (IsSuccess: true, Status: status, Body: string.Empty);
        var failureGen = from status in failureStatusGen
                         from body in failureBodyGen
                         select (IsSuccess: false, Status: status, Body: body);

        var inputGen = Gen.OneOf(successGen, failureGen);

        return Prop.ForAll(Arb.From(inputGen), input =>
        {
            var actualResponse = BuildTextResponse(input.Status, input.Body);
            var referenceResponse = BuildTextResponse(input.Status, input.Body);

            var actual = actualResponse.ToApiResultAsync().GetAwaiter().GetResult();
            var expected = ReferenceToApiResultAsync(referenceResponse).GetAwaiter().GetResult();

            var succeededMatch = actual.Succeeded == expected.Succeeded;
            var errorMatch = actual.Error == expected.Error;

            return (succeededMatch && errorMatch)
                .Label($"status={input.Status}, isSuccess={input.IsSuccess}, " +
                       $"succeededMatch={succeededMatch}, errorMatch={errorMatch}");
        });
    }

    #endregion

    #region Private Helpers

    /// <summary>
    /// Structurally compares two <see cref="SamplePayload"/> instances (or nulls) by their fields.
    /// </summary>
    private static bool SamplePayloadEquals(SamplePayload? left, SamplePayload? right)
    {
        if (left is null && right is null)
            return true;
        if (left is null || right is null)
            return false;
        return left.Id == right.Id && left.Name == right.Name;
    }

    #endregion
}
