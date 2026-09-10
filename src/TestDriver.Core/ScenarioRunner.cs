using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

namespace TestDriver.Core
{
    /// <summary>
    /// Executes a parsed scenario against a driver.
    ///
    /// Two behaviours here exist because of how these suites rot in practice:
    ///  1. On failure it stops and marks the rest Skipped, rather than reporting a
    ///     cascade of downstream failures that all trace to one broken step.
    ///  2. It always tries to capture a screenshot at the point of failure. A red
    ///     result nobody can diagnose gets muted, and a muted suite is a dead suite.
    /// </summary>
    public sealed class ScenarioRunner
    {
        private readonly IGameDriver _driver;

        public ScenarioRunner(IGameDriver driver)
        {
            _driver = driver ?? throw new ArgumentNullException(nameof(driver));
        }

        public async Task<ScenarioResult> RunAsync(Scenario scenario, CancellationToken cancellationToken = default)
        {
            if (scenario == null) throw new ArgumentNullException(nameof(scenario));

            var results = new List<StepResult>(scenario.Steps.Count);
            var overall = Stopwatch.StartNew();
            var failed = false;

            foreach (var step in scenario.Steps)
            {
                if (failed)
                {
                    results.Add(new StepResult(step, StepStatus.Skipped, TimeSpan.Zero));
                    continue;
                }

                var result = await RunStepAsync(step, scenario.DefaultTimeout, cancellationToken)
                    .ConfigureAwait(false);

                results.Add(result);
                if (result.Status == StepStatus.Failed) failed = true;
            }

            overall.Stop();
            return new ScenarioResult(scenario, results, overall.Elapsed);
        }

        private async Task<StepResult> RunStepAsync(Step step, TimeSpan defaultTimeout, CancellationToken ct)
        {
            var sw = Stopwatch.StartNew();
            try
            {
                switch (step.Kind)
                {
                    case StepKind.AssertScreen:
                    {
                        var actual = await _driver.GetCurrentScreenAsync(ct).ConfigureAwait(false);
                        if (!string.Equals(actual, step.Target, StringComparison.Ordinal))
                            return await FailAsync(step, sw,
                                $"Expected screen \"{step.Target}\" but the game was on \"{actual}\".", ct)
                                .ConfigureAwait(false);
                        break;
                    }

                    case StepKind.Tap:
                    {
                        await _driver.TapAsync(step.Target, ct).ConfigureAwait(false);
                        break;
                    }

                    case StepKind.WaitFor:
                    {
                        var timeout = step.Expected != null
                            ? TimeSpan.FromSeconds(int.Parse(step.Expected))
                            : defaultTimeout;

                        var appeared = await _driver.WaitForAsync(step.Target, timeout, ct).ConfigureAwait(false);
                        if (!appeared)
                            return await FailAsync(step, sw,
                                $"\"{step.Target}\" did not appear within {timeout.TotalSeconds:0.#}s.", ct)
                                .ConfigureAwait(false);
                        break;
                    }

                    case StepKind.AssertText:
                    {
                        var actual = await _driver.GetTextAsync(step.Target, ct).ConfigureAwait(false);
                        if (actual == null)
                            return await FailAsync(step, sw,
                                $"\"{step.Target}\" was not present, so its text could not be read.", ct)
                                .ConfigureAwait(false);

                        if (actual.IndexOf(step.Expected ?? string.Empty, StringComparison.OrdinalIgnoreCase) < 0)
                            return await FailAsync(step, sw,
                                $"\"{step.Target}\" showed \"{actual}\", which does not contain \"{step.Expected}\".", ct)
                                .ConfigureAwait(false);
                        break;
                    }

                    case StepKind.AssertVisible:
                    {
                        var visible = await _driver.IsVisibleAsync(step.Target, ct).ConfigureAwait(false);
                        if (!visible)
                            return await FailAsync(step, sw, $"\"{step.Target}\" was not visible.", ct)
                                .ConfigureAwait(false);
                        break;
                    }

                    case StepKind.Capture:
                    {
                        var path = await _driver.CaptureScreenshotAsync(step.Target, ct).ConfigureAwait(false);
                        sw.Stop();
                        return new StepResult(step, StepStatus.Passed, sw.Elapsed, null, path);
                    }

                    default:
                        return await FailAsync(step, sw, $"Unhandled step kind {step.Kind}.", ct)
                            .ConfigureAwait(false);
                }

                sw.Stop();
                return new StepResult(step, StepStatus.Passed, sw.Elapsed);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                // A driver blowing up is a test failure, not a runner crash — one bad
                // element lookup should not take the whole build report down with it.
                return await FailAsync(step, sw, $"{ex.GetType().Name}: {ex.Message}", ct).ConfigureAwait(false);
            }
        }

        private async Task<StepResult> FailAsync(Step step, Stopwatch sw, string message, CancellationToken ct)
        {
            string? shot = null;
            try
            {
                shot = await _driver.CaptureScreenshotAsync($"FAILED-line{step.LineNumber}", ct)
                    .ConfigureAwait(false);
            }
            catch
            {
                // Never let a screenshot failure mask the real failure message.
            }

            sw.Stop();
            return new StepResult(step, StepStatus.Failed, sw.Elapsed, message, shot);
        }
    }
}
