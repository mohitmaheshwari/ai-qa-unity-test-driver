using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using TestDriver.Core;

namespace TestDriver.Core.Tests
{
    /// <summary>
    /// A scripted stand-in for a running game, so the parser, runner and reporting
    /// are all covered without an engine, a device or a build.
    /// </summary>
    internal sealed class FakeGameDriver : IGameDriver
    {
        private readonly Dictionary<string, string> _text = new Dictionary<string, string>();
        private readonly HashSet<string> _visible = new HashSet<string>();
        private readonly HashSet<string> _neverAppears = new HashSet<string>();

        public string CurrentScreen { get; set; } = "MainMenu";
        public List<string> Taps { get; } = new List<string>();
        public List<string> Screenshots { get; } = new List<string>();
        public Func<string, Exception?>? TapThrows { get; set; }

        public FakeGameDriver WithElement(string name, string? text = null)
        {
            _visible.Add(name);
            if (text != null) _text[name] = text;
            return this;
        }

        public FakeGameDriver WithMissingElement(string name)
        {
            _neverAppears.Add(name);
            return this;
        }

        public Task<string> GetCurrentScreenAsync(CancellationToken ct) => Task.FromResult(CurrentScreen);

        public Task TapAsync(string target, CancellationToken ct)
        {
            var ex = TapThrows?.Invoke(target);
            if (ex != null) throw ex;

            if (!_visible.Contains(target))
                throw new InvalidOperationException($"No element named '{target}'.");

            Taps.Add(target);
            return Task.CompletedTask;
        }

        public Task<bool> WaitForAsync(string target, TimeSpan timeout, CancellationToken ct)
        {
            if (_neverAppears.Contains(target)) return Task.FromResult(false);
            return Task.FromResult(_visible.Contains(target));
        }

        public Task<string?> GetTextAsync(string target, CancellationToken ct) =>
            Task.FromResult(_text.TryGetValue(target, out var v) ? v : null);

        public Task<bool> IsVisibleAsync(string target, CancellationToken ct) =>
            Task.FromResult(_visible.Contains(target));

        public Task<string> CaptureScreenshotAsync(string name, CancellationToken ct)
        {
            Screenshots.Add(name);
            return Task.FromResult($"/artifacts/{name}.png");
        }
    }
}
