#if UNITY_2021_2_OR_NEWER
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using TestDriver.Core;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace TestDriver.Unity
{
    /// <summary>
    /// The Unity binding for <see cref="IGameDriver"/> — the only file in this repo
    /// that knows Unity exists.
    ///
    /// Everything above it (parsing, running, reporting) is engine-agnostic and covered
    /// by tests that run on plain .NET in CI. That split is deliberate: the part most
    /// likely to break on a UI change is small and readable, and the part carrying the
    /// logic never needs a device to verify.
    /// </summary>
    public sealed class UnityGameDriver : IGameDriver
    {
        private readonly Func<string> _currentScreen;
        private readonly string _screenshotDirectory;
        private readonly float _pollIntervalSeconds;

        /// <param name="currentScreen">
        /// How the game reports its own state. Injected rather than guessed, because every
        /// project names screens differently and sniffing the scene graph for it is the
        /// first thing that breaks.
        /// </param>
        public UnityGameDriver(
            Func<string> currentScreen,
            string screenshotDirectory = null,
            float pollIntervalSeconds = 0.1f)
        {
            _currentScreen = currentScreen ?? throw new ArgumentNullException(nameof(currentScreen));
            _screenshotDirectory = screenshotDirectory ?? Path.Combine(Application.persistentDataPath, "test-artifacts");
            _pollIntervalSeconds = pollIntervalSeconds;

            Directory.CreateDirectory(_screenshotDirectory);
        }

        public Task<string> GetCurrentScreenAsync(CancellationToken cancellationToken)
            => Task.FromResult(_currentScreen());

        public Task TapAsync(string target, CancellationToken cancellationToken)
        {
            var go = Find(target);
            if (go == null)
                throw new InvalidOperationException($"No active GameObject named '{target}'.");

            // Prefer a real pointer event over calling onClick directly: a Button behind a
            // blocking raycast is a genuine bug, and invoking onClick would hide it.
            var pointer = new PointerEventData(EventSystem.current)
            {
                position = ScreenPointOf(go)
            };

            var handler = ExecuteEvents.GetEventHandler<IPointerClickHandler>(go);
            if (handler == null)
                throw new InvalidOperationException($"'{target}' has no component that handles a click.");

            ExecuteEvents.Execute(handler, pointer, ExecuteEvents.pointerClickHandler);
            return Task.CompletedTask;
        }

        public async Task<bool> WaitForAsync(string target, TimeSpan timeout, CancellationToken cancellationToken)
        {
            var deadline = Time.realtimeSinceStartup + (float)timeout.TotalSeconds;

            while (Time.realtimeSinceStartup < deadline)
            {
                cancellationToken.ThrowIfCancellationRequested();

                if (IsVisible(Find(target))) return true;

                // Task.Yield resumes on Unity's main thread via UnitySynchronizationContext,
                // so this polls once per frame without blocking the player.
                await Task.Yield();

                var waited = Time.realtimeSinceStartup;
                while (Time.realtimeSinceStartup - waited < _pollIntervalSeconds)
                    await Task.Yield();
            }

            return IsVisible(Find(target));
        }

        public Task<string> GetTextAsync(string target, CancellationToken cancellationToken)
        {
            var go = Find(target);
            if (go == null) return Task.FromResult<string>(null);

            var uiText = go.GetComponentInChildren<Text>(true);
            if (uiText != null) return Task.FromResult(uiText.text);

            // TextMeshPro read via reflection so this file does not hard-depend on the
            // TMP package; projects without it still compile.
            foreach (var component in go.GetComponentsInChildren<Component>(true))
            {
                if (component == null) continue;

                var type = component.GetType();
                if (type.Name != "TextMeshProUGUI" && type.Name != "TextMeshPro") continue;

                var property = type.GetProperty("text");
                if (property != null)
                    return Task.FromResult((string)property.GetValue(component));
            }

            return Task.FromResult<string>(null);
        }

        public Task<bool> IsVisibleAsync(string target, CancellationToken cancellationToken)
            => Task.FromResult(IsVisible(Find(target)));

        public Task<string> CaptureScreenshotAsync(string name, CancellationToken cancellationToken)
        {
            var safe = string.Join("_", name.Split(Path.GetInvalidFileNameChars()));
            var path = Path.Combine(_screenshotDirectory, $"{safe}.png");

            ScreenCapture.CaptureScreenshot(path);
            return Task.FromResult(path);
        }

        private static GameObject Find(string name)
        {
            // GameObject.Find skips inactive objects, which is exactly the case a failing
            // test needs to distinguish: "not there at all" versus "there but hidden".
            var active = GameObject.Find(name);
            if (active != null) return active;

            foreach (var go in Resources.FindObjectsOfTypeAll<GameObject>())
            {
                if (go.name != name) continue;
                if (go.hideFlags != HideFlags.None) continue;
                if (!go.scene.IsValid()) continue; // skip prefab assets
                return go;
            }

            return null;
        }

        private static bool IsVisible(GameObject go)
        {
            if (go == null || !go.activeInHierarchy) return false;

            foreach (var group in go.GetComponentsInParent<CanvasGroup>(true))
            {
                if (group.alpha <= 0.01f) return false;
                if (!group.interactable && group.blocksRaycasts == false) return false;
            }

            return true;
        }

        private static Vector2 ScreenPointOf(GameObject go)
        {
            var rect = go.transform as RectTransform;
            if (rect == null) return Vector2.zero;

            var corners = new Vector3[4];
            rect.GetWorldCorners(corners);
            var center = (corners[0] + corners[2]) * 0.5f;

            var canvas = go.GetComponentInParent<Canvas>();
            if (canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay && canvas.worldCamera != null)
                return RectTransformUtility.WorldToScreenPoint(canvas.worldCamera, center);

            return center;
        }
    }
}
#endif
