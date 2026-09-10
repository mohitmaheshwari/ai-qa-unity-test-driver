using System.Threading.Tasks;
using TestDriver.Core;
using Xunit;

namespace TestDriver.Core.Tests
{
    public class ReportTests
    {
        private const char Backslash = '\\';
        private const char Quote = '"';

        private static async Task<ScenarioResult> RunFailing()
        {
            var driver = new FakeGameDriver().WithElement("ResourceBar", "40 wood");
            var scenario = ScenarioParser.Parse(
                "Scenario: Tutorial smoke\n" +
                "  Then \"ResourceBar\" should show \"100 wood\"\n" +
                "  And I capture \"end\"");
            return await new ScenarioRunner(driver).RunAsync(scenario);
        }

        private static async Task<ScenarioResult> RunPassing()
        {
            var driver = new FakeGameDriver().WithElement("ResourceBar", "100 wood");
            var scenario = ScenarioParser.Parse(
                "Scenario: Tutorial smoke\n" +
                "  Then \"ResourceBar\" should show \"100 wood\"");
            return await new ScenarioRunner(driver).RunAsync(scenario);
        }

        [Fact]
        public async Task Slack_payload_is_quiet_on_green()
        {
            var payload = SlackReport.BuildPayload(await RunPassing(), "build-4821");

            Assert.Contains("white_check_mark", payload);
            Assert.Contains("Tutorial smoke", payload);
            Assert.DoesNotContain("Failed at line", payload);
        }

        [Fact]
        public async Task Slack_payload_leads_with_the_line_QA_wrote()
        {
            var payload = SlackReport.BuildPayload(await RunFailing(), "build-4821");

            Assert.Contains(":x:", payload);
            Assert.Contains("Failed at line 2", payload);
            Assert.Contains("should show", payload);
            Assert.Contains("40 wood", payload);
            Assert.Contains("build-4821", payload);
        }

        [Fact]
        public async Task Slack_payload_escapes_quotes_so_it_stays_valid_json()
        {
            var payload = SlackReport.BuildPayload(await RunFailing(), "build-4821");

            // The scenario text contains double quotes; they must arrive escaped, not raw.
            var escapedQuotedName = string.Concat(Backslash, Quote, "ResourceBar", Backslash, Quote);
            Assert.Contains(escapedQuotedName, payload);

            Assert.True(IsBalancedJson(payload), "payload should be balanced JSON");
        }

        [Fact]
        public async Task JUnit_marks_failures_and_skips()
        {
            var xml = JUnitReport.Build(await RunFailing());

            Assert.Contains("<testsuite", xml);
            Assert.Contains("failures=\"1\"", xml);
            Assert.Contains("skipped=\"1\"", xml);
            Assert.Contains("<failure message=", xml);
        }

        [Fact]
        public async Task JUnit_escapes_the_quotes_in_step_names()
        {
            var xml = JUnitReport.Build(await RunFailing());

            Assert.Contains("&quot;ResourceBar&quot;", xml);
        }

        /// <summary>
        /// Cheap structural check that the hand-rolled payload is still well-formed,
        /// which is the risk you take by not using a serializer.
        /// </summary>
        private static bool IsBalancedJson(string s)
        {
            var depth = 0;
            var inString = false;
            var escaped = false;

            foreach (var c in s)
            {
                if (inString)
                {
                    if (escaped)
                    {
                        escaped = false;
                    }
                    else if (c == Backslash)
                    {
                        escaped = true;
                    }
                    else if (c == Quote)
                    {
                        inString = false;
                    }
                    continue;
                }

                if (c == Quote) inString = true;
                else if (c == '{' || c == '[') depth++;
                else if (c == '}' || c == ']') depth--;

                if (depth < 0) return false;
            }

            return depth == 0 && !inString;
        }
    }
}
