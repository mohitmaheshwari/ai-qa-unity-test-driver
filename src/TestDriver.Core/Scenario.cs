using System;
using System.Collections.Generic;

namespace TestDriver.Core
{
    /// <summary>What a single line of a scenario asks the driver to do.</summary>
    public enum StepKind
    {
        /// <summary>Assert the game is on a named screen.</summary>
        AssertScreen,
        /// <summary>Tap a UI element by name.</summary>
        Tap,
        /// <summary>Block until an element appears, or time out.</summary>
        WaitFor,
        /// <summary>Assert an element's text contains an expected value.</summary>
        AssertText,
        /// <summary>Assert an element is currently visible.</summary>
        AssertVisible,
        /// <summary>Capture a screenshot and attach it to the report.</summary>
        Capture
    }

    public sealed class Step
    {
        public Step(StepKind kind, string target, string? expected, string sourceLine, int lineNumber)
        {
            Kind = kind;
            Target = target;
            Expected = expected;
            SourceLine = sourceLine;
            LineNumber = lineNumber;
        }

        public StepKind Kind { get; }

        /// <summary>Element or screen name the step acts on.</summary>
        public string Target { get; }

        /// <summary>Expected value, for assertion steps. Null otherwise.</summary>
        public string? Expected { get; }

        /// <summary>The original plain-language line, kept so failures quote what QA wrote.</summary>
        public string SourceLine { get; }

        public int LineNumber { get; }

        public override string ToString() => SourceLine;
    }

    public sealed class Scenario
    {
        public Scenario(string name, IReadOnlyList<Step> steps, TimeSpan defaultTimeout)
        {
            Name = name;
            Steps = steps;
            DefaultTimeout = defaultTimeout;
        }

        public string Name { get; }
        public IReadOnlyList<Step> Steps { get; }
        public TimeSpan DefaultTimeout { get; }
    }
}
