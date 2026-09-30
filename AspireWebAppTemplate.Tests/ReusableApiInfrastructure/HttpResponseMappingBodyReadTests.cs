// Feature: reusable-api-infrastructure, Task 3.3: single body-read structure
using System.Net.Http;
using System.Text;
using System.Threading;
using AspireWebAppTemplate.Application.Common;
using AspireWebAppTemplate.Web.Extensions;

namespace AspireWebAppTemplate.Tests.ReusableApiInfrastructure;

/// <summary>
/// Unit tests verifying the single body-read structure of
/// <see cref="HttpResponseMessageExtensions"/> (Requirement 2.8): the helper introduces no
/// additional body reads beyond those the current code performs — exactly one read per branch.
/// The response body is wrapped in a <see cref="CountingHttpContent"/> that increments a counter
/// each time its content stream is materialized, so the test can assert the number of reads.
/// </summary>
public class HttpResponseMappingBodyReadTests
{
    #region Generic Overload (ToApiResultAsync&lt;T&gt;)

    /// <summary>
    /// On a success status, the generic overload deserializes the body and SHALL read it exactly once.
    /// </summary>
    [Fact]
    public async Task GenericOverload_SuccessBranch_ReadsBodyExactlyOnce()
    {
        // Arrange: a 200 response whose body is a JSON payload wrapped in a counting content.
        var content = new CountingHttpContent("\"hello\"", "application/json");
        var response = new HttpResponseMessage(HttpStatusCode.OK) { Content = content };

        // Act
        var result = await response.ToApiResultAsync<string>();

        // Assert: success mapped correctly and the body was materialized exactly once.
        Assert.True(result.Succeeded);
        Assert.Equal("hello", result.Data);
        Assert.Equal(1, content.ReadCount);
    }

    /// <summary>
    /// On a non-success status, the generic overload reads the body as text (the error) and
    /// SHALL read it exactly once, without also attempting a deserialization read.
    /// </summary>
    [Fact]
    public async Task GenericOverload_FailureBranch_ReadsBodyExactlyOnce()
    {
        // Arrange: a 400 response whose body is the error text wrapped in a counting content.
        var content = new CountingHttpContent("something went wrong", "text/plain");
        var response = new HttpResponseMessage(HttpStatusCode.BadRequest) { Content = content };

        // Act
        var result = await response.ToApiResultAsync<string>();

        // Assert: failure carries the body text and the body was materialized exactly once.
        Assert.False(result.Succeeded);
        Assert.Equal("something went wrong", result.Error);
        Assert.Equal(1, content.ReadCount);
    }

    #endregion

    #region Non-Generic Overload (ToApiResultAsync)

    /// <summary>
    /// On a success status, the non-generic overload maps to a data-less success result and
    /// SHALL NOT read the body at all (no deserialization is performed).
    /// </summary>
    [Fact]
    public async Task NonGenericOverload_SuccessBranch_DoesNotReadBody()
    {
        // Arrange: a 204 response with a counting content that must remain untouched on success.
        var content = new CountingHttpContent(string.Empty, "text/plain");
        var response = new HttpResponseMessage(HttpStatusCode.NoContent) { Content = content };

        // Act
        var result = await response.ToApiResultAsync();

        // Assert: success with no body read (the non-generic success branch inspects only the status).
        Assert.True(result.Succeeded);
        Assert.Null(result.Error);
        Assert.Equal(0, content.ReadCount);
    }

    /// <summary>
    /// On a non-success status, the non-generic overload reads the body as text (the error) and
    /// SHALL read it exactly once.
    /// </summary>
    [Fact]
    public async Task NonGenericOverload_FailureBranch_ReadsBodyExactlyOnce()
    {
        // Arrange: a 500 response whose body is the error text wrapped in a counting content.
        var content = new CountingHttpContent("server error", "text/plain");
        var response = new HttpResponseMessage(HttpStatusCode.InternalServerError) { Content = content };

        // Act
        var result = await response.ToApiResultAsync();

        // Assert: failure carries the body text and the body was materialized exactly once.
        Assert.False(result.Succeeded);
        Assert.Equal("server error", result.Error);
        Assert.Equal(1, content.ReadCount);
    }

    #endregion

    #region Test Doubles

    /// <summary>
    /// An <see cref="HttpContent"/> that carries a fixed UTF-8 payload and counts how many times
    /// its content stream is materialized, so a test can assert the exact number of body reads.
    /// </summary>
    private sealed class CountingHttpContent : HttpContent
    {
        /// <summary>The raw payload bytes served on each read.</summary>
        private readonly byte[] _payload;

        /// <summary>The running count of body materializations.</summary>
        private int _readCount;

        /// <summary>
        /// Initializes the content with the given text payload and media type.
        /// </summary>
        /// <param name="payload">The body text served on each read.</param>
        /// <param name="mediaType">The media type reported by the content headers.</param>
        public CountingHttpContent(string payload, string mediaType)
        {
            _payload = Encoding.UTF8.GetBytes(payload);
            Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue(mediaType)
            {
                CharSet = "utf-8"
            };
        }

        /// <summary>The number of times the body has been materialized.</summary>
        public int ReadCount => _readCount;

        /// <summary>
        /// Serializes the payload to the target stream, counting the read. This path is used by
        /// <c>ReadAsStringAsync</c> (failure branch) and by JSON deserialization (success branch).
        /// </summary>
        protected override async Task SerializeToStreamAsync(Stream stream, TransportContext? context)
        {
            Interlocked.Increment(ref _readCount);
            await stream.WriteAsync(_payload);
        }

        /// <summary>
        /// Provides the payload as a readable stream, counting the read. Some consumers (including
        /// JSON deserialization) materialize the body through this path.
        /// </summary>
        protected override Task<Stream> CreateContentReadStreamAsync()
        {
            Interlocked.Increment(ref _readCount);
            return Task.FromResult<Stream>(new MemoryStream(_payload, writable: false));
        }

        /// <summary>
        /// Reports the known payload length so the content need not be buffered to compute it.
        /// </summary>
        protected override bool TryComputeLength(out long length)
        {
            length = _payload.Length;
            return true;
        }
    }

    #endregion
}
