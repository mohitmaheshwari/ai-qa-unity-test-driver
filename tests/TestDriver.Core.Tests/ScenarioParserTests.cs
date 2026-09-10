using System;
using System.Linq;
using TestDriver.Core;
using Xunit;

namespace TestDriver.Core.Tests
{
    public class ScenarioParserTests
    {
        private const string TutorialScenario = @"
Scenario: New player can complete the first build step
timeout: 20s

  Given the game is on the ""MainMenu"" screen
  When I tap ""PlayButton""
  And I wait for ""TutorialOverlay"" for 30s
  And I tap ""BuildFarmButton""
  Then ""ResourceBar"" should show ""100 wood""
  And ""TutorialOverlay"" should be visible
  And I capture ""tutorial-complete""
";

        [Fact]
        public void Parses_scenario_name_and_timeout_header()
        {
            var scenario = ScenarioParser.Parse(TutorialScenario);

            Assert.Equal("New player can complete the first build step", scenario.Name);
            Assert.Equal(TimeSpan.FromSeconds(20), scenario.DefaultTimeout);
        }

        [Fact]
        public void Parses_every_supported_step_kind_in_order()
        {
            var steps = ScenarioParser.Parse(TutorialScenario).Steps;

            Assert.Equal(
                new[]
                {
                    StepKind.AssertScreen, StepKind.Tap, StepKind.WaitFor, StepKind.Tap,
                    StepKind.AssertText, StepKind.AssertVisible, StepKind.Capture
                },
                steps.Select(s => s.Kind));
        }

        [Fact]
        public void Captures_targets_and_expected_values()
        {
            var steps = ScenarioParser.Parse(TutorialScenario).Steps;

            Assert.Equal("MainMenu", steps[0].Target);
            Assert.Equal("PlayButton", steps[1].Target);
            Assert.Equal("TutorialOverlay", steps[2].Target);
            Assert.Equal("30", steps[2].Expected);
            Assert.Equal("ResourceBar", steps[4].Target);
            Assert.Equal("100 wood", steps[4].Expected);
        }

        [Fact]
        public void Keeps_the_original_line_so_failures_quote_what_QA_wrote()
        {
            var steps = ScenarioParser.Parse(TutorialScenario).Steps;

            Assert.Equal(@"When I tap ""PlayButton""", steps[1].SourceLine);
            Assert.Equal(6, steps[1].LineNumber);
        }

        [Theory]
        [InlineData(@"I click ""PlayButton""", StepKind.Tap)]
        [InlineData(@"press ""PlayButton""", StepKind.Tap)]
        [InlineData(@"tap on ""PlayButton""", StepKind.Tap)]
        [InlineData(@"wait for ""X""", StepKind.WaitFor)]
        [InlineData(@"wait for ""X"" up to 5 seconds", StepKind.WaitFor)]
        [InlineData(@"""X"" should contain ""y""", StepKind.AssertText)]
        [InlineData(@"""X"" should read ""y""", StepKind.AssertText)]
        [InlineData(@"screenshot ""x""", StepKind.Capture)]
        public void Accepts_natural_phrasing_variants(string line, StepKind expected)
        {
            var scenario = ScenarioParser.Parse("Scenario: v\n" + line);
            Assert.Equal(expected, scenario.Steps.Single().Kind);
        }

        [Fact]
        public void Rejects_a_line_it_cannot_execute_rather_than_skipping_it()
        {
            var ex = Assert.Throws<ScenarioParseException>(() =>
                ScenarioParser.Parse("Scenario: v\n  When I somehow win the game"));

            Assert.Equal(2, ex.LineNumber);
            Assert.Contains("not a step this driver understands", ex.Message);
        }

        [Fact]
        public void Requires_targets_to_be_quoted_so_an_AI_draft_is_reviewable()
        {
            Assert.Throws<ScenarioParseException>(() =>
                ScenarioParser.Parse("Scenario: v\n  When I tap PlayButton"));
        }

        [Fact]
        public void Strips_comments_but_not_hashes_inside_quotes()
        {
            var scenario = ScenarioParser.Parse(
                "Scenario: v\n  When I tap \"Item#3\"   # pick the third one\n");

            Assert.Equal("Item#3", scenario.Steps.Single().Target);
        }

        [Fact]
        public void Rejects_a_scenario_with_no_steps()
        {
            Assert.Throws<ScenarioParseException>(() => ScenarioParser.Parse("Scenario: empty\n\n"));
        }
    }
}
