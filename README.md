# AI-Assisted Unity Smoke Test Driver

[![CI](https://github.com/mohitmaheshwari/ai-qa-unity-test-driver/actions/workflows/ci.yml/badge.svg)](https://github.com/mohitmaheshwari/ai-qa-unity-test-driver/actions/workflows/ci.yml)

A small instrumentation layer that turns a scenario written in plain English into an
executable smoke test for a Unity game, runs it on a real build, and reports the result
to Slack and CI.

Built to explore one question: **if an AI drafts your tests, what has to be true for a
human to trust the result?**

```
Scenario: New player can complete the first build step
timeout: 15s

  Given the game is on the "MainMenu" screen
  When I tap "PlayButton"
  And I wait for "TutorialOverlay" for 30s
  And I tap "BuildFarmButton"
  Then "ResourceBar" should show "100 wood"
  And "TutorialOverlay" should be visible
  And I capture "tutorial-complete"
```

That file is the test. QA writes it — or an LLM drafts it and QA reviews it — and the
driver executes it verbatim against a running build.

---

## The design argument

The interesting problem with AI-generated tests isn't generating them. It's that a
generated test which *silently does nothing* still goes green, and a green suite nobody
trusts is worse than no suite at all.

Three decisions follow from that:

**1. Every element a step touches must be quoted.**
`tap "PlayButton"` parses; `tap PlayButton` is rejected. It looks pedantic until you're
reviewing forty AI-drafted scenarios — then the quotes are the only thing letting you
see, at a glance, exactly which objects a test will touch without reading any code.

**2. An unrecognised line is a hard error, never a skip.**
If the parser can't map a line to a step it throws, naming the line and number. A parser
that quietly ignores what it doesn't understand will happily report a green run for a
scenario where half the steps never executed. That's the exact failure mode that makes a
team stop believing the suite.

**3. Every failure captures a screenshot before it returns.**
A red result nobody can diagnose gets muted, and a muted suite is a dead suite. The
runner also stops on first failure and marks the rest skipped, so you get one honest
failure instead of a cascade of downstream noise all tracing to the same broken step.

Same logic in the Slack reporter: green is one quiet line, red leads with the sentence
QA actually wrote. Channels that shout on every green build get muted within a week.

---

## Architecture

```
scenarios/*.scenario          plain-language input, editable without an IDE
        |
        v
TestDriver.Core               netstandard2.1, zero dependencies, 32 tests
  ScenarioParser              text -> steps, strict
  ScenarioRunner              steps -> results, screenshot on failure
  SlackReport / JUnitReport   results -> Slack blocks / CI XML
        |
        v
IGameDriver                   six methods. the entire engine surface
        |
        v
UnityGameDriver               the only file that imports UnityEngine
```

The core targets `netstandard2.1` — what Unity itself consumes — so the same code runs
in CI on plain .NET and inside the player, with no `#if` branching and no second
implementation to keep in sync.

The payoff is that the part carrying the logic never needs a device to verify, and the
part most likely to break on a UI change is one small file you can read in a sitting.

---

## What is verified, and what is not

I'd rather be precise about this than let a reader assume more than is true.

| Component | Status |
|---|---|
| `TestDriver.Core` — parser, runner, reporters | **32 tests, green in CI** (see badge/Actions) |
| The shipped `tutorial-smoke.scenario` | **Covered** — CI parses and runs it against a fake game, in both passing and stalled-tutorial states |
| `UnityGameDriver` | **Compiles against Unity 2021.2+; not exercised in CI** — it needs an editor and a build, so it is verified by hand |
| Real-device execution (Android/iOS) | **Not included** — the driver is device-agnostic by design, but no device lab is wired up here |

The Unity adapter is guarded by `#if UNITY_2021_2_OR_NEWER` so the .NET solution builds
without it.

---

## Running it

```bash
# core suite — no Unity required
dotnet test
```

In Unity: drop `src/TestDriver.Core` and `unity/Runtime` into `Assets/`, put your
scenario files under `Assets/Tests/Scenarios/`, and a PlayMode test becomes three lines:

```csharp
var driver   = new UnityGameDriver(() => GameState.CurrentScreenName);
var scenario = ScenarioParser.Parse(File.ReadAllText(path));
var result   = await new ScenarioRunner(driver).RunAsync(scenario);
```

`unity/Tests/SmokeTests.cs` shows the full version, including writing `junit.xml` and
`slack.json` artifacts on every run regardless of outcome.

---

## How this was built with Claude Code

<!--
  TODO (Shobhit) — this section is the one Goodgame will actually read, and it has to be
  yours. Do not ship the placeholder text below. Rewrite it in your own words, and only
  claim what actually happened. Specifics beat adjectives; one real correction you made
  is worth more than three sentences about how much time it saved.

  Worth covering:
    - What you had Claude Code write end-to-end, and what you specified up front
      (e.g. the strict-parser and screenshot-on-failure rules were design decisions,
      not something the model volunteered)
    - Where it got things wrong and you caught it. Real examples from this build:
        * The first Slack reporter hand-rolled JSON escaping and produced a payload
          with an unescaped quote — caught by adding a balanced-JSON assertion rather
          than by reading the code.
        * The first pass targeted a framework the machine could not run, so the suite
          compiled but no test ever executed. A green build log that runs zero tests
          is exactly the failure this project is about.
    - What you would not let it decide: the six-method IGameDriver surface, and the
      netstandard2.1 target for Unity compatibility
    - Roughly what share of the code it wrote versus what you rewrote
-->

_(section to be written by hand — see comment above)_

---

## What I would build next

- **Real-device execution** on a small Android/iOS farm, triggered per build
- **A scenario linter** that flags steps referencing elements absent from the scene, so a
  broken AI draft fails at review time rather than on device
- **Failure clustering**, so twenty tests broken by one UI change report as one incident
- **Bug-to-regression flow**: paste a bug report, get a draft scenario, review and commit
