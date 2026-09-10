using System;
using System.Collections.Generic;
using System.Text;

namespace TestDriver.Core
{
    /// <summary>
    /// Renders a run as a Slack Block Kit payload.
    ///
    /// Written by hand rather than with a JSON package so the core stays
    /// dependency-free — Unity dependency conflicts are not worth one serializer.
    ///
    /// The formatting rule is deliberate: a passing run is one quiet line, a failing
    /// run leads with the QA-authored sentence that broke. Teams mute channels that
    /// shout on green, and a muted channel makes the whole suite pointless.
    /// </summary>
    public static class SlackReport
    {
        public static string BuildPayload(ScenarioResult result, string buildLabel)
        {
            if (result == null) throw new ArgumentNullException(nameof(result));

            var blocks = new List<string>();
            var icon = result.Passed ? ":white_check_mark:" : ":x:";
            var verdict = result.Passed ? "passed" : "FAILED";

            blocks.Add(Section($"{icon} *{Esc(result.Scenario.Name)}* {verdict} on `{Esc(buildLabel)}`"));

            if (result.Passed)
            {
                blocks.Add(Context($"{result.PassedCount} steps in {result.Duration.TotalSeconds:0.0}s"));
            }
            else
            {
                var failure = result.FirstFailure!;

                // The literal characters backslash-n, so Slack renders a line break
                // inside the JSON string rather than the payload containing a raw newline.
                var newline = "\\n";

                blocks.Add(Section(
                    "*Failed at line " + failure.Step.LineNumber + ":*" + newline +
                    ">" + Esc(failure.Step.SourceLine)));

                blocks.Add(Section("```" + Esc(failure.Message ?? "no detail") + "```"));

                if (!string.IsNullOrEmpty(failure.ScreenshotPath))
                    blocks.Add(Context($"screenshot: {Esc(failure.ScreenshotPath!)}"));

                blocks.Add(Context(
                    $"{result.PassedCount} passed · {result.FailedCount} failed · " +
                    $"{result.SkippedCount} skipped · {result.Duration.TotalSeconds:0.0}s"));
            }

            return "{\"blocks\":[" + string.Join(",", blocks) + "]}";
        }

        private static string Section(string markdown) =>
            "{\"type\":\"section\",\"text\":{\"type\":\"mrkdwn\",\"text\":\"" + markdown + "\"}}";

        private static string Context(string markdown) =>
            "{\"type\":\"context\",\"elements\":[{\"type\":\"mrkdwn\",\"text\":\"" + markdown + "\"}]}";

        /// <summary>Escape a value for embedding inside a JSON string literal.</summary>
        private static string Esc(string value)
        {
            var sb = new StringBuilder(value.Length + 16);
            foreach (var c in value)
            {
                switch (c)
                {
                    case '"':
                        sb.Append('\\').Append('"');
                        break;
                    case '\\':
                        sb.Append('\\').Append('\\');
                        break;
                    case '\n':
                        sb.Append('\\').Append('n');
                        break;
                    case '\r':
                        sb.Append('\\').Append('r');
                        break;
                    case '\t':
                        sb.Append('\\').Append('t');
                        break;
                    default:
                        if (c < 0x20)
                            sb.Append('\\').Append('u').Append(((int)c).ToString("x4"));
                        else
                            sb.Append(c);
                        break;
                }
            }
            return sb.ToString();
        }
    }
}
