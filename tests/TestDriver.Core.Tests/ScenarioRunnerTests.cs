using System;
using System.Linq;
using System.Threading.Tasks;
using TestDriver.Core;
using Xunit;

namespace TestDriver.Core.Tests
{
    public class ScenarioRunnerTests
    {
        private static Scenario Parse(string body) => ScenarioParser.Parse("Scenario: t\n" + body);

        [Fact]
        public async Task Passing_run_executes_every_step()
        {
            var driver = new FakeGameDriver()
                .WithElement("PlayButton")
                .WithElement("TutorialOverlay")
                .WithElement("ResourceBar", "100 wood, 50 stone");

            var scenario = Parse(@"
  Given the game is on the ""MainMenu"" screen
  When I tap ""PlayButton""
  And I wait for ""TutorialOverlay""
  Then ""ResourceBar"" should show ""100 wood""");

            var result = await new ScenarioRunner(driver).RunAsync(scenario);

            Assert.True(result.Passed);
            Assert.Equal(4, result.PassedCount);
            Assert.Equal(new[] { "PlayButton" }, driver.Taps);
        }

        [Fact]
        public async Task Failure_stops_the_run_and_marks_the_rest_skipped()
        {
            var driver = new FakeGameDriver()
                .WithElement("PlayButton")
                .WithMissingElement("TutorialOverlay")
                .WithElement("BuildFarmButton");

            var scenario = Parse(@"
  When I tap ""PlayButton""
  And I wait for ""TutorialOverlay"" for 2s
  And I tap ""BuildFarmButton""
  And I capture ""done""");

            var result = await new ScenarioRunner(driver).RunAsync(scenario);

            Assert.False(result.Passed);
            Assert.Equal(1, result.PassedCount);
            Assert.Equal(1, result.FailedCount);
            Assert.Equal(2, result.SkippedCount);

            // The step after the failure must not have run.
            Assert.DoesNotContain("BuildFarmButton", driver.Taps);
        }

        [Fact]
        public async Task Timeout_failure_names_the_element_and_the_budget()
        {
            var driver = new FakeGameDriver().WithMissingElement("TutorialOverlay");
            var scenario = Parse(@"  When I wait for ""TutorialOverlay"" for 7s");

            var result = await new ScenarioRunner(driver).RunAsync(scenario);

            Assert.Contains("TutorialOverlay", result.FirstFailure!.Message);
            Assert.Contains("7s", result.FirstFailure!.Message);
        }

        [Fact]
        public async Task Text_mismatch_reports_what_was_actually_on_screen()
        {
            var driver = new FakeGameDriver().WithElement("ResourceBar", "40 wood");
            var scenario = Parse(@"  Then ""ResourceBar"" should show ""100 wood""");

            var result = await new ScenarioRunner(driver).RunAsync(scenario);

            Assert.Contains("40 wood", result.FirstFailure!.Message);
            Assert.Contains("100 wood", result.FirstFailure!.Message);
        }

        [Fact]
        public async Task Wrong_screen_reports_both_expected_and_actual()
        {
            var driver = new FakeGameDriver { CurrentScreen = "LoadingScreen" };
            var scenario = Parse(@"  Given the game is on the ""MainMenu"" screen");

            var result = await new ScenarioRunner(driver).RunAsync(scenario);

            Assert.Contains("MainMenu", result.FirstFailure!.Message);
            Assert.Contains("LoadingScreen", result.FirstFailure!.Message);
        }

        [Fact]
        public async Task Every_failure_captures_a_screenshot_for_triage()
        {
            var driver = new FakeGameDriver().WithElement("ResourceBar", "nothing");
            var scenario = Parse(@"  Then ""ResourceBar"" should show ""100 wood""");

            var result = await new ScenarioRunner(driver).RunAsync(scenario);

            Assert.NotNull(result.FirstFailure!.ScreenshotPath);
            Assert.Contains("FAILED-line2", driver.Screenshots.Single());
        }

        [Fact]
        public async Task A_driver_exception_becomes_a_failed_step_not_a_crashed_run()
        {
            var driver = new FakeGameDriver().WithElement("PlayButton");
            driver.TapThrows = t => t == "PlayButton" ? new TimeoutException("device disconnected") : null;

            var scenario = Parse(@"  When I tap ""PlayButton""");

            var result = await new ScenarioRunner(driver).RunAsync(scenario);

            Assert.False(result.Passed);
            Assert.Contains("device disconnected", result.FirstFailure!.Message);
        }

        [Fact]
        public async Task Capture_step_attaches_its_screenshot_path_to_the_result()
        {
            var driver = new FakeGameDriver();
            var scenario = Parse(@"  And I capture ""tutorial-complete""");

            var result = await new ScenarioRunner(driver).RunAsync(scenario);

            Assert.True(result.Passed);
            Assert.Equal("/artifacts/tutorial-complete.png", result.Steps.Single().ScreenshotPath);
        }
    }
}
