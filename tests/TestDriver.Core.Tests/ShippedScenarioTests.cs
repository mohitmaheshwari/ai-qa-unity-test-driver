using System.IO;
using System.Linq;
using System.Threading.Tasks;
using TestDriver.Core;
using Xunit;

namespace TestDriver.Core.Tests
{
    /// <summary>
    /// Runs the scenario file that ships in the repo, so the documented example is
    /// covered rather than just described.
    /// </summary>
    public class ShippedScenarioTests
    {
        private static string Load(string name) =>
            File.ReadAllText(Path.Combine("Scenarios", name));

        [Fact]
        public void The_example_scenario_parses()
        {
            var scenario = ScenarioParser.Parse(Load("tutorial-smoke.scenario"));

            Assert.Equal("New player can complete the first build step", scenario.Name);
            Assert.Equal(7, scenario.Steps.Count);
        }

        [Fact]
        public async Task The_example_scenario_passes_against_a_healthy_game()
        {
            var driver = new FakeGameDriver()
                .WithElement("PlayButton")
                .WithElement("TutorialOverlay")
                .WithElement("BuildFarmButton")
                .WithElement("ResourceBar", "100 wood, 20 stone");

            var scenario = ScenarioParser.Parse(Load("tutorial-smoke.scenario"));
            var result = await new ScenarioRunner(driver).RunAsync(scenario);

            Assert.True(result.Passed, result.FirstFailure?.Message);
            Assert.Equal(new[] { "PlayButton", "BuildFarmButton" }, driver.Taps);
        }

        [Fact]
        public async Task The_example_scenario_fails_loudly_when_the_tutorial_stalls()
        {
            var driver = new FakeGameDriver()
                .WithElement("PlayButton")
                .WithMissingElement("TutorialOverlay")
                .WithElement("BuildFarmButton")
                .WithElement("ResourceBar", "100 wood");

            var scenario = ScenarioParser.Parse(Load("tutorial-smoke.scenario"));
            var result = await new ScenarioRunner(driver).RunAsync(scenario);

            Assert.False(result.Passed);
            Assert.Contains("TutorialOverlay", result.FirstFailure!.Message);

            // The build step must not have been attempted after the overlay never showed.
            Assert.DoesNotContain("BuildFarmButton", driver.Taps);

            var slack = SlackReport.BuildPayload(result, "build-4821");
            Assert.Contains("TutorialOverlay", slack);
            Assert.Contains(":x:", slack);
        }
    }
}
