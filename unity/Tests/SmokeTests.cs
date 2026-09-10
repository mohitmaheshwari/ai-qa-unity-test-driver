#if UNITY_2021_2_OR_NEWER
using System.Collections;
using System.IO;
using NUnit.Framework;
using TestDriver.Core;
using TestDriver.Unity;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace TestDriver.Unity.Tests
{
    /// <summary>
    /// The whole point of the design: a PlayMode test is now three lines, and the
    /// interesting content lives in a .scenario file a QA engineer can edit without
    /// opening the IDE.
    /// </summary>
    public class SmokeTests
    {
        [UnityTest]
        public IEnumerator Tutorial_smoke_passes_on_a_fresh_boot()
        {
            yield return SceneManager.LoadSceneAsync("Bootstrap");

            var driver = new UnityGameDriver(() => GameState.CurrentScreenName);
            var scenario = ScenarioParser.Parse(
                File.ReadAllText("Assets/Tests/Scenarios/tutorial-smoke.scenario"));

            var run = new ScenarioRunner(driver).RunAsync(scenario);
            while (!run.IsCompleted) yield return null;

            var result = run.Result;

            // Write the CI-facing artifacts regardless of outcome.
            File.WriteAllText(
                Path.Combine(Application.persistentDataPath, "junit.xml"),
                JUnitReport.Build(result));

            File.WriteAllText(
                Path.Combine(Application.persistentDataPath, "slack.json"),
                SlackReport.BuildPayload(result, Application.version));

            if (!result.Passed)
            {
                var failure = result.FirstFailure;
                Assert.Fail(
                    $"Line {failure.Step.LineNumber}: {failure.Step.SourceLine}\n" +
                    $"{failure.Message}\n" +
                    $"Screenshot: {failure.ScreenshotPath}");
            }
        }
    }

    /// <summary>Stand-in for whatever the real project uses to name its current screen.</summary>
    internal static class GameState
    {
        public static string CurrentScreenName { get; set; } = "MainMenu";
    }
}
#endif
