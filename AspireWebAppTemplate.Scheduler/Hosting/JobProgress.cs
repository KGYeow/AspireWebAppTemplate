using System.Diagnostics;
using AspireWebAppTemplate.Scheduler.Jobs;
using Microsoft.Extensions.Logging;

namespace AspireWebAppTemplate.Scheduler.Hosting;

/// <summary>
/// Default <see cref="IJobProgress"/> implementation that reports job progress to BOTH the structured
/// logger and the human-readable console (via <see cref="SchedulerConsole"/>) from a single call.
/// </summary>
/// <remarks>
/// Registered as a scoped service so it shares the per-run DI scope with the job and its dependencies.
/// The logger is a run-scoped logger created by <see cref="JobRunner"/>, so every message correlates
/// with the run id captured in the logging scope.
/// </remarks>
public sealed class JobProgress : IJobProgress
{
    #region Constructor

    /// <summary>The run-scoped logger used to emit the durable, structured record of each message.</summary>
    private readonly ILogger _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="JobProgress"/> class.
    /// </summary>
    /// <param name="loggerFactory">The logger factory used to create the "Scheduler.Job" logger.</param>
    public JobProgress(ILoggerFactory loggerFactory)
    {
        _logger = loggerFactory.CreateLogger("Scheduler.Job");
    }

    #endregion

    #region IJobProgress

    /// <inheritdoc />
    public void Info(string message)
    {
        _logger.LogInformation("{Message}", message);
        SchedulerConsole.WriteInfo(message);
    }

    /// <inheritdoc />
    public void Warn(string message)
    {
        _logger.LogWarning("{Message}", message);
        SchedulerConsole.WriteWarning(message);
    }

    /// <inheritdoc />
    public IDisposable Phase(string name)
    {
        _logger.LogInformation("Phase started: {Phase}", name);
        SchedulerConsole.WritePhaseStart(name);
        return new PhaseScope(name, _logger);
    }

    #endregion

    #region Phase timing

    /// <summary>
    /// A running phase: times from creation to disposal, then reports the phase end and its duration
    /// to the logger and console. Disposal happens whether the phase completes normally or throws.
    /// </summary>
    private sealed class PhaseScope : IDisposable
    {
        /// <summary>The phase name being timed.</summary>
        private readonly string _name;

        /// <summary>The logger used to record the phase end.</summary>
        private readonly ILogger _logger;

        /// <summary>The stopwatch measuring the phase duration.</summary>
        private readonly Stopwatch _stopwatch;

        /// <summary>Guards against double-reporting if <see cref="Dispose"/> is called more than once.</summary>
        private bool _completed;

        /// <summary>
        /// Initializes and starts timing a new phase.
        /// </summary>
        /// <param name="name">The phase name.</param>
        /// <param name="logger">The run-scoped logger.</param>
        public PhaseScope(string name, ILogger logger)
        {
            _name = name;
            _logger = logger;
            _stopwatch = Stopwatch.StartNew();
        }

        /// <summary>Stops timing and reports the phase end with its measured duration.</summary>
        public void Dispose()
        {
            if (_completed)
            {
                return;
            }

            _completed = true;
            _stopwatch.Stop();
            _logger.LogInformation("Phase completed: {Phase} ({DurationMs} ms)", _name, _stopwatch.ElapsedMilliseconds);
            SchedulerConsole.WritePhaseEnd(_name, _stopwatch.Elapsed);
        }
    }

    #endregion
}