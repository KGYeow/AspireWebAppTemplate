using Microsoft.AspNetCore.SignalR.Client;

namespace AspireWebAppTemplate.Web.Services;

/// <summary>
/// SignalR reconnection policy using exponential backoff with a maximum delay cap.
/// Delays: 1s, 2s, 4s, 8s, 16s (capped at 30s), up to 5 attempts total.
/// Returns null after all attempts are exhausted to signal the connection should be abandoned.
/// </summary>
/// <remarks>
/// Prevents thundering herd on transient network issues while recovering automatically
/// from short blips. After 5 failed attempts, the client falls back to the existing
/// navigation-based badge refresh behavior.
/// </remarks>
public class ExponentialBackoffRetryPolicy : IRetryPolicy
{
    #region Constructor

    /// <summary>
    /// Maximum number of reconnection attempts before giving up.
    /// </summary>
    private const int MaxAttempts = 5;

    /// <summary>
    /// Maximum delay between reconnection attempts (30 seconds).
    /// </summary>
    private static readonly TimeSpan MaxDelay = TimeSpan.FromSeconds(30);

    #endregion

    #region Retry Logic

    /// <summary>
    /// Computes the next retry delay using exponential backoff (2^N seconds),
    /// capped at <see cref="MaxDelay"/>. Returns null after <see cref="MaxAttempts"/>
    /// to stop reconnecting.
    /// </summary>
    /// <param name="retryContext">The context providing information about the current retry attempt.</param>
    /// <returns>The delay before the next retry, or null to stop reconnecting.</returns>
    public TimeSpan? NextRetryDelay(RetryContext retryContext)
    {
        if (retryContext.PreviousRetryCount >= MaxAttempts)
            return null; // Stop reconnecting — fall back to navigation-based refresh.

        var delay = TimeSpan.FromSeconds(Math.Pow(2, retryContext.PreviousRetryCount));
        return delay > MaxDelay ? MaxDelay : delay;
    }

    #endregion
}
