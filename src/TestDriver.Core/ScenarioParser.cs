using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text.RegularExpressions;

namespace TestDriver.Core
{
    /// <summary>
    /// Turns a plain-language scenario written by QA into executable steps.
    ///
    /// The grammar is deliberately small and strict about one thing only: every element
    /// a step acts on must be "quoted". That single rule is what makes an AI-drafted
    /// scenario reviewable — a human can see exactly which objects the test will touch
    /// without reading any code.
    /// </summary>
    public static class ScenarioParser
    {
        private static readonly TimeSpan DefaultTimeout = TimeSpan.FromSeconds(10);

        // Leading Gherkin-ish keyword, stripped before matching the verb.
        private static readonly Regex KeywordPrefix = new Regex(
            @"^\s*(given|when|then|and|but)\s+", RegexOptions.IgnoreCase | RegexOptions.Compiled);

        private static readonly Regex ScenarioHeader = new Regex(
            @"^\s*scenario\s*:\s*(?<name>.+?)\s*$", RegexOptions.IgnoreCase | RegexOptions.Compiled);

        private static readonly Regex TimeoutHeader = new Regex(
            @"^\s*timeout\s*:\s*(?<seconds>\d+)\s*s?\s*$", RegexOptions.IgnoreCase | RegexOptions.Compiled);

        // the game is on the "MainMenu" screen
        private static readonly Regex OnScreen = new Regex(
            @"^(?:the\s+)?(?:game\s+)?is\s+on\s+(?:the\s+)?""(?<target>[^""]+)""\s*(?:screen)?\s*$",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);

        // I tap "PlayButton"  /  tap "PlayButton"  /  I click "PlayButton"
        private static readonly Regex Tap = new Regex(
            @"^(?:i\s+)?(?:tap|click|press)\s+(?:on\s+)?""(?<target>[^""]+)""\s*$",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);

        // I wait for "TutorialOverlay"  /  wait for "X" for 30s
        private static readonly Regex WaitFor = new Regex(
            @"^(?:i\s+)?wait\s+for\s+""(?<target>[^""]+)""(?:\s+(?:for|up\s+to)\s+(?<seconds>\d+)\s*s(?:econds)?)?\s*$",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);

        // "ResourceBar" should show "100 wood"  /  "X" should contain "y"
        private static readonly Regex ShouldShow = new Regex(
            @"^""(?<target>[^""]+)""\s+should\s+(?:show|contain|display|read)\s+""(?<expected>[^""]*)""\s*$",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);

        // "TutorialOverlay" should be visible
        private static readonly Regex ShouldBeVisible = new Regex(
            @"^""(?<target>[^""]+)""\s+should\s+be\s+visible\s*$",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);

        // I capture "tutorial-complete"  /  screenshot "x"
        private static readonly Regex Capture = new Regex(
            @"^(?:i\s+)?(?:capture|screenshot|snap)\s+""(?<target>[^""]+)""\s*$",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);

        /// <summary>
        /// Parse a scenario. Throws <see cref="ScenarioParseException"/> on the first line
        /// that cannot be mapped to a step — silently skipping unknown lines would let a
        /// green run hide a step nobody actually executed.
        /// </summary>
        public static Scenario Parse(string text)
        {
            if (text == null) throw new ArgumentNullException(nameof(text));

            var name = "Unnamed scenario";
            var timeout = DefaultTimeout;
            var steps = new List<Step>();

            var lines = text.Replace("\r\n", "\n").Split('\n');
            for (var i = 0; i < lines.Length; i++)
            {
                var raw = lines[i];
                var lineNumber = i + 1;
                var line = StripComment(raw).Trim();

                if (line.Length == 0) continue;

                var header = ScenarioHeader.Match(line);
                if (header.Success)
                {
                    name = header.Groups["name"].Value;
                    continue;
                }

                var timeoutHeader = TimeoutHeader.Match(line);
                if (timeoutHeader.Success)
                {
                    timeout = TimeSpan.FromSeconds(
                        int.Parse(timeoutHeader.Groups["seconds"].Value, CultureInfo.InvariantCulture));
                    continue;
                }

                steps.Add(ParseStep(line, raw.Trim(), lineNumber, timeout));
            }

            if (steps.Count == 0)
                throw new ScenarioParseException("Scenario contains no executable steps.", 0, text);

            return new Scenario(name, steps, timeout);
        }

        private static Step ParseStep(string line, string sourceLine, int lineNumber, TimeSpan defaultTimeout)
        {
            var body = KeywordPrefix.Replace(line, string.Empty).Trim();

            var m = OnScreen.Match(body);
            if (m.Success)
                return new Step(StepKind.AssertScreen, m.Groups["target"].Value, null, sourceLine, lineNumber);

            m = Tap.Match(body);
            if (m.Success)
                return new Step(StepKind.Tap, m.Groups["target"].Value, null, sourceLine, lineNumber);

            m = WaitFor.Match(body);
            if (m.Success)
            {
                var seconds = m.Groups["seconds"];
                var expected = seconds.Success ? seconds.Value : null;
                return new Step(StepKind.WaitFor, m.Groups["target"].Value, expected, sourceLine, lineNumber);
            }

            m = ShouldShow.Match(body);
            if (m.Success)
                return new Step(StepKind.AssertText, m.Groups["target"].Value,
                    m.Groups["expected"].Value, sourceLine, lineNumber);

            m = ShouldBeVisible.Match(body);
            if (m.Success)
                return new Step(StepKind.AssertVisible, m.Groups["target"].Value, null, sourceLine, lineNumber);

            m = Capture.Match(body);
            if (m.Success)
                return new Step(StepKind.Capture, m.Groups["target"].Value, null, sourceLine, lineNumber);

            throw new ScenarioParseException(
                $"Line {lineNumber} is not a step this driver understands: \"{sourceLine}\". " +
                "Supported: is on \"Screen\" / tap \"X\" / wait for \"X\" [for Ns] / " +
                "\"X\" should show \"y\" / \"X\" should be visible / capture \"name\".",
                lineNumber, sourceLine);
        }

        private static string StripComment(string line)
        {
            // Only treat # as a comment when it is not inside quotes.
            var inQuotes = false;
            for (var i = 0; i < line.Length; i++)
            {
                if (line[i] == '"') inQuotes = !inQuotes;
                else if (line[i] == '#' && !inQuotes) return line.Substring(0, i);
            }
            return line;
        }
    }

    public sealed class ScenarioParseException : Exception
    {
        public ScenarioParseException(string message, int lineNumber, string sourceLine)
            : base(message)
        {
            LineNumber = lineNumber;
            SourceLine = sourceLine;
        }

        public int LineNumber { get; }
        public string SourceLine { get; }
    }
}
