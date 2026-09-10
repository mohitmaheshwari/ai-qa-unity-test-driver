using System;
using System.Security;
using System.Text;

namespace TestDriver.Core
{
    /// <summary>
    /// JUnit XML, so the run shows up natively in CI test summaries
    /// (GitHub Actions, TeamCity, Jenkins) with no bespoke parsing.
    /// </summary>
    public static class JUnitReport
    {
        public static string Build(ScenarioResult result, string suiteName = "UnitySmoke")
        {
            if (result == null) throw new ArgumentNullException(nameof(result));

            var sb = new StringBuilder();
            sb.AppendLine("<?xml version=\"1.0\" encoding=\"UTF-8\"?>");
            sb.AppendFormat(
                "<testsuite name=\"{0}\" tests=\"{1}\" failures=\"{2}\" skipped=\"{3}\" time=\"{4:0.000}\">",
                X(suiteName), result.Steps.Count, result.FailedCount, result.SkippedCount,
                result.Duration.TotalSeconds);
            sb.AppendLine();

            foreach (var step in result.Steps)
            {
                sb.AppendFormat(
                    "  <testcase classname=\"{0}\" name=\"{1}\" time=\"{2:0.000}\"",
                    X(result.Scenario.Name), X($"L{step.Step.LineNumber} {step.Step.SourceLine}"),
                    step.Duration.TotalSeconds);

                switch (step.Status)
                {
                    case StepStatus.Passed:
                        sb.AppendLine(" />");
                        break;
                    case StepStatus.Skipped:
                        sb.AppendLine(">");
                        sb.AppendLine("    <skipped />");
                        sb.AppendLine("  </testcase>");
                        break;
                    case StepStatus.Failed:
                        sb.AppendLine(">");
                        sb.AppendFormat("    <failure message=\"{0}\" />", X(step.Message ?? "failed"));
                        sb.AppendLine();
                        sb.AppendLine("  </testcase>");
                        break;
                }
            }

            sb.AppendLine("</testsuite>");
            return sb.ToString();
        }

        private static string X(string value) => SecurityElement.Escape(value) ?? string.Empty;
    }
}
