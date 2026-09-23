// Bugfix: scheduler-dependency-cleanup, Unit tests: JobRunner exit-code mapping
using AspireWebAppTemplate.Scheduler.Constants;
using AspireWebAppTemplate.Scheduler.Hosting;
using AspireWebAppTemplate.Scheduler.Jobs;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace AspireWebAppTemplate.Tests.Scheduler;

/// <summary>
/// Unit tests verifying that <see cref="JobRunner.RunAsync(IHost, string[])"/> maps job outcomes to the
/// correct <see cref="ExitCodes"/> values.
/// </summary>
/// <remarks>
/// Bugfix: scheduler-dependency-cleanup. Covers the exit-code contract that Windows Task Scheduler keys on:
/// <list type="bullet">
///   <item>no job name / unknown job name â†’ <see cref="ExitCodes.InvalidUsage"/>;</item>
///   <item>an unhandled exception thrown by the job â†’ <see cref="ExitCodes.JobFailed"/>;</item>
///   <item>a success (or any code returned by the job) is passed through verbatim.</item>
/// </list>
/// The <see cref="ExitCodes.Cancelled"/> path is not exercised here: <see cref="JobRunner"/> only maps to
/// <c>Cancelled</c> when its internal <see cref="CancellationTokenSource"/> reports cancellation, and that
/// source is signalled exclusively from the process-wide <see cref="Console.CancelKeyPress"/> handler wired
/// inside <c>RunAsync</c>. A stub job receives only the token (not the source) so it cannot request
/// cancellation on that specific source from a unit test; the cancellation path is covered by the Scheduler
/// integration tests (Task 7).
/// </remarks>
public class JobRunnerExitCodeTests
{
    #region Test doubles

    /// <summary>
    /// A configurable <see cref="IScheduledJob"/> test double whose <see cref="RunAsync"/> either returns a
    /// fixed exit code or throws a supplied exception, letting each test drive one exit-code path.
    /// </summary>
    private sealed class StubJob : IScheduledJob
    {
        /// <summary>The exit code returned by <see cref="RunAsync"/> when <see cref="_throw"/> is null.</summary>
        private readonly int _exitCode;

        /// <summary>An optional exception thrown by <see cref="RunAsync"/> to exercise the failure path.</summary>
        private readonly Exception? _throw;

        /// <summary>
        /// Initializes a new instance of the <see cref="StubJob"/> class.
        /// </summary>
        /// <param name="name">The job name used for command-line selection.</param>
        /// <param name="exitCode">The exit code to return on success (ignored when <paramref name="throw"/> is set).</param>
        /// <param name="throw">An optional exception to throw instead of returning an exit code.</param>
        public StubJob(string name, int exitCode = ExitCodes.Success, Exception? @throw = null)
        {
            Name = name;
            _exitCode = exitCode;
            _throw = @throw;
        }

        /// <inheritdoc />
        public string Name { get; }

        /// <inheritdoc />
        public string Description => "Stub job for JobRunner exit-code tests.";

        /// <inheritdoc />
        public Task<int> RunAsync(CancellationToken cancellationToken)
        {
            if (_throw is not null)
            {
                throw _throw;
            }

            return Task.FromResult(_exitCode);
        }
    }

    #endregion

    #region Host builder

    /// <summary>
    /// Builds a minimal <see cref="IHost"/> whose service provider registers logging (required by
    /// <see cref="JobRunner"/>) and the supplied jobs. Mirrors how the real Scheduler host exposes
    /// <see cref="IScheduledJob"/> registrations to <c>RunAsync</c>.
    /// </summary>
    /// <param name="jobs">The jobs to register in the host.</param>
    /// <returns>A built host ready to pass to <see cref="JobRunner.RunAsync"/>.</returns>
    private static IHost BuildHost(params IScheduledJob[] jobs)
    {
        var builder = Host.CreateApplicationBuilder();

        foreach (var job in jobs)
        {
            builder.Services.AddScoped<IScheduledJob>(_ => job);
        }

        return builder.Build();
    }

    #endregion

    #region Exit-code mapping

    /// <summary>
    /// When no job name is supplied, <see cref="JobRunner.RunAsync"/> returns
    /// <see cref="ExitCodes.InvalidUsage"/>.
    /// </summary>
    [Fact]
    public async Task RunAsync_WithNoJobName_ReturnsInvalidUsage()
    {
        using var host = BuildHost(new StubJob("purge-audit-logs"));

        var exitCode = await JobRunner.RunAsync(host, []);

        Assert.Equal(ExitCodes.InvalidUsage, exitCode);
    }

    /// <summary>
    /// When only whitespace arguments are supplied, <see cref="JobRunner.RunAsync"/> treats it as no job
    /// name and returns <see cref="ExitCodes.InvalidUsage"/>.
    /// </summary>
    [Fact]
    public async Task RunAsync_WithWhitespaceJobName_ReturnsInvalidUsage()
    {
        using var host = BuildHost(new StubJob("purge-audit-logs"));

        var exitCode = await JobRunner.RunAsync(host, ["   "]);

        Assert.Equal(ExitCodes.InvalidUsage, exitCode);
    }

    /// <summary>
    /// When the supplied job name does not match any registered job,
    /// <see cref="JobRunner.RunAsync"/> returns <see cref="ExitCodes.InvalidUsage"/>.
    /// </summary>
    [Fact]
    public async Task RunAsync_WithUnknownJobName_ReturnsInvalidUsage()
    {
        using var host = BuildHost(new StubJob("purge-audit-logs"));

        var exitCode = await JobRunner.RunAsync(host, ["does-not-exist"]);

        Assert.Equal(ExitCodes.InvalidUsage, exitCode);
    }

    /// <summary>
    /// When the selected job throws an unhandled exception, <see cref="JobRunner.RunAsync"/> catches it and
    /// returns <see cref="ExitCodes.JobFailed"/>.
    /// </summary>
    [Fact]
    public async Task RunAsync_WhenJobThrows_ReturnsJobFailed()
    {
        using var host = BuildHost(
            new StubJob("failing-job", @throw: new InvalidOperationException("boom")));

        var exitCode = await JobRunner.RunAsync(host, ["failing-job"]);

        Assert.Equal(ExitCodes.JobFailed, exitCode);
    }

    /// <summary>
    /// When the selected job completes successfully, <see cref="JobRunner.RunAsync"/> passes the job's own
    /// exit code through unchanged (success case).
    /// </summary>
    [Fact]
    public async Task RunAsync_WhenJobSucceeds_ReturnsSuccessCode()
    {
        using var host = BuildHost(new StubJob("ok-job", exitCode: ExitCodes.Success));

        var exitCode = await JobRunner.RunAsync(host, ["ok-job"]);

        Assert.Equal(ExitCodes.Success, exitCode);
    }

    /// <summary>
    /// When the selected job returns a non-zero code of its own, <see cref="JobRunner.RunAsync"/> passes it
    /// through verbatim rather than remapping it â€” proving success passthrough is the job's value, not a
    /// hard-coded constant.
    /// </summary>
    [Fact]
    public async Task RunAsync_WhenJobReturnsCustomCode_PassesItThrough()
    {
        const int customCode = 42;
        using var host = BuildHost(new StubJob("custom-job", exitCode: customCode));

        var exitCode = await JobRunner.RunAsync(host, ["custom-job"]);

        Assert.Equal(customCode, exitCode);
    }

    /// <summary>
    /// Job selection is case-insensitive: a name differing only in case still resolves and runs.
    /// </summary>
    [Fact]
    public async Task RunAsync_JobNameMatchIsCaseInsensitive()
    {
        using var host = BuildHost(new StubJob("purge-audit-logs", exitCode: ExitCodes.Success));

        var exitCode = await JobRunner.RunAsync(host, ["PURGE-AUDIT-LOGS"]);

        Assert.Equal(ExitCodes.Success, exitCode);
    }

    #endregion
}
