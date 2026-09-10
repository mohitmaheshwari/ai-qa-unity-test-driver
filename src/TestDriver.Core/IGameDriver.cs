using System;
using System.Threading;
using System.Threading.Tasks;

namespace TestDriver.Core
{
    /// <summary>
    /// The whole surface the runner needs from a game.
    ///
    /// Keeping this at six methods is the point: the Unity binding is small enough to
    /// read in one sitting, and the runner, parser and reporting are all testable
    /// against a fake without an engine, a device or a build.
    /// </summary>
    public interface IGameDriver
    {
        /// <summary>Name of the screen/state the game is currently showing.</summary>
        Task<string> GetCurrentScreenAsync(CancellationToken cancellationToken);

        /// <summary>Send a tap to a named element. Should throw if the element is absent.</summary>
        Task TapAsync(string target, CancellationToken cancellationToken);

        /// <summary>Poll until the element exists and is visible. False on timeout.</summary>
        Task<bool> WaitForAsync(string target, TimeSpan timeout, CancellationToken cancellationToken);

        /// <summary>Read an element's displayed text. Null when the element is absent.</summary>
        Task<string?> GetTextAsync(string target, CancellationToken cancellationToken);

        /// <summary>Whether the element exists and is visible right now.</summary>
        Task<bool> IsVisibleAsync(string target, CancellationToken cancellationToken);

        /// <summary>Capture a screenshot; returns the path or URL the report should link.</summary>
        Task<string> CaptureScreenshotAsync(string name, CancellationToken cancellationToken);
    }
}
