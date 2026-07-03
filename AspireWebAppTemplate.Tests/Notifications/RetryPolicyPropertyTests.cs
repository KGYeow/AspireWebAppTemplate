// Feature: realtime-notifications, Property 7: Exponential backoff retry delays
using AspireWebAppTemplate.Web.Services;
using FsCheck;
using FsCheck.Fluent;
using FsCheck.Xunit;
using Microsoft.AspNetCore.SignalR.Client;

namespace AspireWebAppTemplate.Tests.Notifications;

/// <summary>
/// Property-based tests verifying that the ExponentialBackoffRetryPolicy correctly
/// computes retry delays using exponential backoff (2^N seconds, capped at 30s)
/// and stops reconnecting after 5 attempts.
/// </summary>
/// <remarks>
/// **Validates: Requirements 6.1**
/// </remarks>
public class RetryPolicyPropertyTests
{
    /// <summary>
    /// The policy instance under test.
    /// </summary>
    private readonly ExponentialBackoffRetryPolicy _policy = new();

    /// <summary>
    /// Property: For any retry attempt number N in [0, 4], the NextRetryDelay SHALL return
    /// a delay of min(2^N seconds, 30 seconds).
    /// **Validates: Requirements 6.1**
    /// </summary>
    [Property(MaxTest = 2)]
    public FsCheck.Property ValidAttempts_ReturnExponentialBackoffDelay()
    {
        var attemptGen = Gen.Choose(0, 4);

        return Prop.ForAll(
            Arb.From(attemptGen),
            (int attemptNumber) =>
            {
                var retryContext = new RetryContext
                {
                    PreviousRetryCount = (long)attemptNumber,
                    ElapsedTime = TimeSpan.Zero,
                    RetryReason = null
                };

                var result = _policy.NextRetryDelay(retryContext);
                var expectedSeconds = Math.Min(Math.Pow(2, attemptNumber), 30);
                var expectedDelay = TimeSpan.FromSeconds(expectedSeconds);

                return (result != null && result.Value == expectedDelay).Label(
                    $"For attempt {attemptNumber}, expected delay {expectedDelay.TotalSeconds}s " +
                    $"but got {(result?.TotalSeconds.ToString() ?? "null")}s");
            });
    }

    /// <summary>
    /// Property: For any retry attempt number N >= 5, the NextRetryDelay SHALL return null
    /// (stop reconnecting).
    /// **Validates: Requirements 6.1**
    /// </summary>
    [Property(MaxTest = 2)]
    public FsCheck.Property ExhaustedAttempts_ReturnNull()
    {
        var attemptGen = Gen.Choose(5, 100);

        return Prop.ForAll(
            Arb.From(attemptGen),
            (int attemptNumber) =>
            {
                var retryContext = new RetryContext
                {
                    PreviousRetryCount = (long)attemptNumber,
                    ElapsedTime = TimeSpan.Zero,
                    RetryReason = null
                };

                var result = _policy.NextRetryDelay(retryContext);

                return (result == null).Label(
                    $"For attempt {attemptNumber} (>= 5), expected null " +
                    $"but got {result?.TotalSeconds}s");
            });
    }
}
