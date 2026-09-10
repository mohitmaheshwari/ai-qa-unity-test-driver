using System;
using System.Collections.Generic;
using System.Linq;

namespace TestDriver.Core
{
    public enum StepStatus
    {
        Passed,
        Failed,
        /// <summary>Not run because an earlier step failed.</summary>
        Skipped
    }

    public sealed class StepResult
    {
        public StepResult(Step step, StepStatus status, TimeSpan duration,
            string? message = null, string? screenshotPath = null)
        {
            Step = step;
            Status = status;
            Duration = duration;
            Message = message;
            ScreenshotPath = screenshotPath;
        }

        public Step Step { get; }
        public StepStatus Status { get; }
        public TimeSpan Duration { get; }

        /// <summary>Failure detail. Null when the step passed.</summary>
        public string? Message { get; }

        /// <summary>Screenshot captured for this step, if any.</summary>
        public string? ScreenshotPath { get; }
    }

    public sealed class ScenarioResult
    {
        public ScenarioResult(Scenario scenario, IReadOnlyList<StepResult> steps, TimeSpan duration)
        {
            Scenario = scenario;
            Steps = steps;
            Duration = duration;
        }

        public Scenario Scenario { get; }
        public IReadOnlyList<StepResult> Steps { get; }
        public TimeSpan Duration { get; }

        public bool Passed => Steps.All(s => s.Status == StepStatus.Passed);

        public StepResult? FirstFailure => Steps.FirstOrDefault(s => s.Status == StepStatus.Failed);

        public int PassedCount => Steps.Count(s => s.Status == StepStatus.Passed);
        public int FailedCount => Steps.Count(s => s.Status == StepStatus.Failed);
        public int SkippedCount => Steps.Count(s => s.Status == StepStatus.Skipped);
    }
}
