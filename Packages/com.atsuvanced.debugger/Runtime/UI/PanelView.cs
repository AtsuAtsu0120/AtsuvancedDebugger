using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.TextCore.Text;
using UnityEngine.UIElements;
using Object = UnityEngine.Object;

namespace Atsuvanced.Debugger.UI
{
    /// <summary>UI resources of the panel (from the settings asset or <see cref="DebuggerBuilder" />).</summary>
    internal sealed class PanelViewResources
    {
        public PanelViewResources(ThemeStyleSheet theme, IReadOnlyList<StyleSheet> styleSheets, FontAsset font)
        {
            Theme = theme;
            StyleSheets = styleSheets ?? Array.Empty<StyleSheet>();
            Font = font;
        }

        public ThemeStyleSheet Theme { get; }
        public IReadOnlyList<StyleSheet> StyleSheets { get; }
        public FontAsset Font { get; }
    }

    /// <summary>
    ///     UI Toolkit host of the debugger: its own GameObject, UIDocument and PanelSettings (never touches the game's
    ///     EventSystem or UI documents), DPI scaling, the safe area, the PIN pad and screen capture. The panel frame is drawn
    ///     by
    ///     <see cref="PanelShell" /> and the overlays are placed by <see cref="OverlayLayer" />.
    ///     <code>
    /// .atsu-root                (full screen, picking ignored)
    ///   .atsu-safe-area         (padding = Screen.safeArea)
    ///     .atsu-panel           PanelShell (hidden when closed)
    ///     .atsu-overlays        OverlayLayer (edges and corner stacks)
    ///   .atsu-entry-code        PIN pad (while the entry code is being asked)
    /// </code>
    ///     The host is created on demand: when the panel opens, the PIN pad is asked, a screenshot is taken or an overlay is
    ///     shown.
    /// </summary>
    internal sealed class PanelView : IDebugPanelView
    {
        private const float BaseDpi = 160f;

        private readonly List<Action<byte[]>> _pendingCaptures = new();
        private readonly PanelViewResources _resources;
        private readonly DebugService _service;
        private bool _capturing;
        private UIDocument _document;
        private GameObject _host;
        private PanelHost _hostComponent;
        private float _lastDpi = -1f;
        private Vector2 _lastRootSize;
        private Rect _lastSafeArea;
        private Vector2Int _lastScreenSize;
        private PanelSettings _panelSettings;
        private VisualElement _root;
        private VisualElement _safeArea;
        private bool _subscribed;
        private bool _visible;

        public PanelView(DebugService service, PanelViewResources resources)
        {
            _service = service;
            _resources = resources ?? new PanelViewResources(null, null, null);
            Subscribe();
        }

        internal PanelShell Shell { get; private set; }

        internal OverlayLayer Overlays { get; private set; }

        internal EntryCodeView EntryCodeView { get; private set; }

        // ---- IDebugPanelView ------------------------------------------------------------

        public void Show()
        {
            _visible = true;
            if (!EnsureHost())
            {
                return;
            }

            Shell.Show();
        }

        public void Hide()
        {
            _visible = false;
            Shell?.Hide();
        }

        public void Destroy()
        {
            _visible = false;
            Unsubscribe();
            Shell?.Dispose();
            Overlays?.Dispose();
            FlushCaptures(null);
            // Destroy is deferred: detach the host so its OnDestroy does not clear a panel rebuilt in the same frame.
            if (_hostComponent)
            {
                _hostComponent.View = null;
            }

            if (_host)
            {
                Object.Destroy(_host);
            }

            if (_panelSettings)
            {
                Object.Destroy(_panelSettings);
            }

            ClearReferences();
        }

        public bool IsScreenPointOverPanel(Vector2 screenPosition)
        {
            var panel = _root?.panel;
            if (panel == null)
            {
                return false;
            }

            var p = RuntimePanelUtils.ScreenToPanel(panel,
                new Vector2(screenPosition.x, Screen.height - screenPosition.y));
            return panel.Pick(p) != null;
        }

        public void ShowEntryCode()
        {
            if (!EnsureHost())
            {
                return;
            }

            if (EntryCodeView == null)
            {
                EntryCodeView = new EntryCodeView(code => _service.PanelState.SubmitEntryCode(code),
                    _service.PanelState.CancelEntryCode);
                _root.Add(EntryCodeView);
            }

            EntryCodeView.Reset();
            EntryCodeView.RemoveFromClassList(UssClasses.Hidden);
            EntryCodeView.schedule.Execute(() => EntryCodeView?.Focus());
        }

        public void HideEntryCode()
        {
            if (EntryCodeView == null)
            {
                return;
            }

            EntryCodeView.AddToClassList(UssClasses.Hidden);
            EntryCodeView.Blur();
        }

        public void CaptureScreenshot(Action<byte[]> onCaptured)
        {
            if (onCaptured == null)
            {
                return;
            }

            if (!EnsureHost() || _hostComponent == null)
            {
                onCaptured(null);
                return;
            }

            _pendingCaptures.Add(onCaptured);
            if (_capturing)
            {
                return;
            }

            _capturing = true;
            _hostComponent.StartCoroutine(CaptureRoutine());
        }

        public void Tick(float unscaledDeltaTime)
        {
            if (_host == null)
            {
                return;
            }

            UpdateScale();
            UpdateSafeArea();
            Shell.Tick();
        }

        // ---- Host -----------------------------------------------------------------------

        private void Subscribe()
        {
            if (_subscribed)
            {
                return;
            }

            _subscribed = true;
            // An overlay that appears needs the host even when the panel was never opened.
            _service.Overlays.OverlayShown += OnOverlayShown;
        }

        private void Unsubscribe()
        {
            if (!_subscribed)
            {
                return;
            }

            _subscribed = false;
            _service.Overlays.OverlayShown -= OnOverlayShown;
        }

        private void OnOverlayShown(DebugOverlay overlay)
        {
            EnsureHost();
        }

        private bool EnsureHost()
        {
            if (_host)
            {
                return true;
            }

            if (!Application.isPlaying)
            {
                return false;
            }

            Subscribe(); // after Destroy (Panel.Destroy), the next use rebuilds everything

            var general = _service.Settings.General;
            _panelSettings = ScriptableObject.CreateInstance<PanelSettings>();
            _panelSettings.name = "AtsuvancedDebugger PanelSettings";
            _panelSettings.themeStyleSheet = _resources.Theme;
            _panelSettings.scaleMode = PanelScaleMode.ConstantPixelSize;
            _panelSettings.scale = ComputeScale();
            _panelSettings.sortingOrder = general.SortingOrder;
            _panelSettings.clearColor = false;

            if (!_panelSettings.themeStyleSheet)
            {
                Debug.LogWarning(
                    "[AtsuvancedDebugger] No theme assigned (DebuggerSettingsAsset missing?). The panel will be unstyled.");
            }

            // Lives under the service's runner object (one root object in the hierarchy); stands alone without a runner.
            _host = new GameObject("Panel");
            var parent = _service.RunnerTransform;
            if (parent)
            {
                _host.transform.SetParent(parent, false);
            }
            else
            {
                Object.DontDestroyOnLoad(_host);
            }

            _document = _host.AddComponent<UIDocument>();
            _document.panelSettings = _panelSettings;
            _hostComponent = _host.AddComponent<PanelHost>();
            _hostComponent.View = this;

            BuildTree(_document.rootVisualElement);
            if (_visible)
            {
                Shell.Show();
            }
            else
            {
                Shell.Hide();
            }

            if (_service.PanelState.IsEntryCodeShown)
            {
                ShowEntryCode();
            }

            return true;
        }

        private void BuildTree(VisualElement documentRoot)
        {
            documentRoot.pickingMode = PickingMode.Ignore;

            _root = new VisualElement { name = "atsu-root", pickingMode = PickingMode.Ignore };
            _root.AddToClassList("atsu-root");
            _root.EnableInClassList("atsu-theme--light", _service.Settings.General.Theme == DebuggerTheme.Light);
            foreach (var sheet in _resources.StyleSheets)
            {
                if (sheet)
                {
                    _root.styleSheets.Add(sheet);
                }
            }

            if (_resources.Font)
            {
                _root.style.unityFontDefinition = FontDefinition.FromSDFFont(_resources.Font);
            }

            documentRoot.Add(_root);

            _safeArea = new VisualElement { pickingMode = PickingMode.Ignore };
            _safeArea.AddToClassList("atsu-safe-area");
            _root.Add(_safeArea);

            Shell = new PanelShell(_service);
            _safeArea.Add(Shell.Root);

            Overlays = new OverlayLayer(_service.Overlays);
            _safeArea.Add(Overlays.Root);
        }

        private void ClearReferences()
        {
            _host = null;
            _document = null;
            _panelSettings = null;
            _hostComponent = null;
            _root = _safeArea = null;
            Shell = null;
            Overlays = null;
            EntryCodeView = null;
            _lastRootSize = default;
        }

        internal void OnHostDestroyed()
        {
            // Destroyed from outside (exiting play mode).
            Shell?.Dispose();
            Overlays?.Dispose();
            FlushCaptures(null);
            ClearReferences();
        }

        // ---- Screenshot -----------------------------------------------------------------

        private IEnumerator CaptureRoutine()
        {
            // Hide everything of ours, let one frame render without it, then read the back buffer at the end of the next frame.
            _root.style.display = DisplayStyle.None;
            yield return null;
            yield return new WaitForEndOfFrame();

            byte[] png = null;
            try
            {
                var texture = ScreenCapture.CaptureScreenshotAsTexture();
                if (texture)
                {
                    png = texture.EncodeToPNG();
                    Object.Destroy(texture);
                }
            }
            catch (Exception e)
            {
                Debug.LogException(e);
            }

            if (_root != null)
            {
                _root.style.display = StyleKeyword.Null;
            }

            FlushCaptures(png);
        }

        private void FlushCaptures(byte[] png)
        {
            _capturing = false;
            if (_pendingCaptures.Count == 0)
            {
                return;
            }

            var callbacks = _pendingCaptures.ToArray();
            _pendingCaptures.Clear();
            foreach (var callback in callbacks)
            {
                try
                {
                    callback(png);
                }
                catch (Exception e)
                {
                    Debug.LogException(e);
                }
            }
        }

        // ---- Layout helpers -------------------------------------------------------------

        /// <summary>
        ///     Density-independent scaling with a floor: 1 UI point = 1 px at 160 dpi or lower (desktop monitors stay
        ///     at 1:1), and grows with density on phones / tablets / retina so touch targets keep their physical size.
        /// </summary>
        private float ComputeScale()
        {
            var dpi = Screen.dpi;
            _lastDpi = dpi;
            var density = dpi > 0f ? Mathf.Max(1f, dpi / BaseDpi) : 1f;
            return Mathf.Max(0.1f, density * _service.Settings.General.UiScale);
        }

        private void UpdateScale()
        {
            if (_panelSettings && !Mathf.Approximately(Screen.dpi, _lastDpi))
            {
                _panelSettings.scale = ComputeScale();
            }
        }

        private void UpdateSafeArea()
        {
            var panel = _root?.panel;
            if (panel == null)
            {
                return;
            }

            var safe = Screen.safeArea;
            var screen = new Vector2Int(Screen.width, Screen.height);
            var rootSize = _root.layout.size;
            if (safe == _lastSafeArea && screen == _lastScreenSize && rootSize == _lastRootSize)
            {
                return;
            }

            if (float.IsNaN(rootSize.x) || rootSize.x <= 0f)
            {
                return;
            }

            _lastSafeArea = safe;
            _lastScreenSize = screen;
            _lastRootSize = rootSize;

            // Screen space is bottom-left origin; ScreenToPanel expects top-left.
            var topLeft = RuntimePanelUtils.ScreenToPanel(panel, new Vector2(safe.xMin, screen.y - safe.yMax));
            var bottomRight = RuntimePanelUtils.ScreenToPanel(panel, new Vector2(safe.xMax, screen.y - safe.yMin));
            _safeArea.style.paddingLeft = Mathf.Max(0f, topLeft.x);
            _safeArea.style.paddingTop = Mathf.Max(0f, topLeft.y);
            _safeArea.style.paddingRight = Mathf.Max(0f, rootSize.x - bottomRight.x);
            _safeArea.style.paddingBottom = Mathf.Max(0f, rootSize.y - bottomRight.y);
        }
    }

    /// <summary>Component on the panel's GameObject: runs the screenshot coroutine and notices when the object is destroyed.</summary>
    [AddComponentMenu("")]
    internal sealed class PanelHost : MonoBehaviour
    {
        internal PanelView View;

        private void OnDestroy()
        {
            View?.OnHostDestroyed();
        }
    }
}