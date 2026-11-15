using System;
using System.Collections.Generic;
using Atsuvanced.Debugger.UI;
using UnityEngine.TextCore.Text;
using UnityEngine.UIElements;

namespace Atsuvanced.Debugger
{
    /// <summary>
    ///     Composes a debugger: settings, storage, the features to install and UI resources, as a method chain.
    ///     <see cref="CreateDefault" /> starts from the settings asset and every built-in feature; register changes with
    ///     <see cref="AtsuDebug.Configure" /> (or pass them to
    ///     <see cref="AtsuDebug.Init(System.Action{Atsuvanced.Debugger.DebuggerBuilder}(Atsuvanced.Debugger.DebuggerBuilder))" />
    ///     ):
    ///     <code>
    /// [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterAssembliesLoaded)]
    /// static void ConfigureDebugger() => AtsuDebug.Configure(b => b
    ///     .ConfigureSettings(s => s.Trigger.Position = TriggerPosition.BottomRight)
    ///     .UseStorage(new MyCloudStorage())
    ///     .Remove&lt;ProfilerFeature&gt;()
    ///     .Configure&lt;OptionsFeature&gt;(f => f.Layout = OptionsLayout.SingleTab)
    ///     .AddOptionView&lt;ItemId&gt;(() => new ItemIdView())
    ///     .Add(new NetworkFeature()));
    /// </code>
    /// </summary>
    public sealed class DebuggerBuilder
    {
        private readonly List<DebuggerFeature> _features = new();
        private readonly List<(Type type, Func<OptionView> factory)> _optionViews = new();
        private readonly List<StyleSheet> _styleSheets = new();
        private IBugReporter _bugReporter;
        private FontAsset _font;
        private ThemeStyleSheet _theme;

        /// <summary>An empty builder (no features) over <paramref name="settings" /> (a copy is not made).</summary>
        public DebuggerBuilder(DebuggerSettings settings = null)
        {
            Settings = settings ?? new DebuggerSettings();
        }

        /// <summary>The settings the debugger will be built with.</summary>
        public DebuggerSettings Settings { get; }

        /// <summary>Features in installation order.</summary>
        public IReadOnlyList<DebuggerFeature> Features => _features;

        // ---- Build --------------------------------------------------------------------------

        internal IDebuggerStorage Storage { get; private set; }

        /// <summary>
        ///     The default composition: a copy of the asset's settings (built-in defaults without an asset), its UI resources and
        ///     UXML templates, and every built-in feature.
        /// </summary>
        public static DebuggerBuilder CreateDefault(DebuggerSettingsAsset asset = null)
        {
            var builder = new DebuggerBuilder(asset ? asset.Settings.Clone() : new DebuggerSettings());
            if (asset)
            {
                builder.UseTheme(asset.Theme);
                foreach (var sheet in asset.StyleSheets)
                {
                    builder.AddStyleSheet(sheet);
                }

                builder.UseFont(asset.Font);
            }

            foreach (var feature in BuiltInFeatures.Create())
            {
                builder.Add(feature);
            }

            return builder;
        }

        // ---- Settings / services ------------------------------------------------------------

        public DebuggerBuilder ConfigureSettings(Action<DebuggerSettings> configure)
        {
            if (configure == null)
            {
                throw new ArgumentNullException(nameof(configure));
            }

            configure(Settings);
            return this;
        }

        /// <summary>Language of the built-in UI. The string table is shared by the whole process (there is one UI).</summary>
        public DebuggerBuilder UseLanguage(DebuggerLanguage language)
        {
            Settings.General.Language = language;
            return this;
        }

        /// <summary>Where the persisted state is saved (default <see cref="PlayerPrefsDebuggerStorage" />).</summary>
        public DebuggerBuilder UseStorage(IDebuggerStorage storage)
        {
            Storage = storage;
            return this;
        }

        /// <summary>Where bug reports are sent (default <see cref="LocalBugReporter" />).</summary>
        public DebuggerBuilder UseBugReporter(IBugReporter reporter)
        {
            _bugReporter = reporter;
            return this;
        }

        /// <summary>Protects the panel with a 4-digit PIN.</summary>
        public DebuggerBuilder UseEntryCode(string code, bool everyTime = false)
        {
            if (!EntryCodeGate.IsValid(code))
            {
                throw new ArgumentException($"The entry code must be {EntryCodeGate.Length} digits (0-9).",
                    nameof(code));
            }

            Settings.EntryCode.Required = true;
            Settings.EntryCode.Code = code;
            Settings.EntryCode.EveryTime = everyTime;
            return this;
        }

        // ---- Features -----------------------------------------------------------------------

        /// <summary>Adds a feature. A feature of the same type is replaced in place (keeping its position).</summary>
        public DebuggerBuilder Add(DebuggerFeature feature)
        {
            if (feature == null)
            {
                throw new ArgumentNullException(nameof(feature));
            }

            var index = _features.FindIndex(f => f.GetType() == feature.GetType());
            if (index >= 0)
            {
                _features[index] = feature;
            }
            else
            {
                _features.Add(feature);
            }

            return this;
        }

        /// <summary>Removes the feature of type <typeparamref name="T" /> (and subclasses). No-op when absent.</summary>
        public DebuggerBuilder Remove<T>() where T : DebuggerFeature
        {
            _features.RemoveAll(f => f is T);
            return this;
        }

        /// <summary>Removes every feature (start from an empty debugger and add what you need).</summary>
        public DebuggerBuilder ClearFeatures()
        {
            _features.Clear();
            return this;
        }

        /// <summary>Changes the feature of type <typeparamref name="T" />. Throws if it is not in the builder.</summary>
        public DebuggerBuilder Configure<T>(Action<T> configure) where T : DebuggerFeature
        {
            if (configure == null)
            {
                throw new ArgumentNullException(nameof(configure));
            }

            var feature = Get<T>() ?? throw new InvalidOperationException(
                $"{typeof(T).Name} is not in the builder (removed, or not a built-in feature). Add it first.");
            configure(feature);
            return this;
        }

        public bool Has<T>() where T : DebuggerFeature
        {
            return Get<T>() != null;
        }

        public T Get<T>() where T : DebuggerFeature
        {
            foreach (var f in _features)
            {
                if (f is T t)
                {
                    return t;
                }
            }

            return null;
        }

        // ---- Option views -------------------------------------------------------------------

        /// <summary>Draws every option of <typeparamref name="T" /> with the view created by <paramref name="factory" />.</summary>
        public DebuggerBuilder AddOptionView<T>(Func<OptionView> factory)
        {
            return AddOptionView(typeof(T), factory);
        }

        /// <summary>
        ///     Draws every option of <paramref name="valueType" /> with the view created by <paramref name="factory" />.
        ///     This is the only way to customize how an option is drawn.
        /// </summary>
        public DebuggerBuilder AddOptionView(Type valueType, Func<OptionView> factory)
        {
            if (valueType == null)
            {
                throw new ArgumentNullException(nameof(valueType));
            }

            if (factory == null)
            {
                throw new ArgumentNullException(nameof(factory));
            }

            _optionViews.Add((valueType, factory));
            return this;
        }

        // ---- UI resources -------------------------------------------------------------------

        public DebuggerBuilder UseTheme(ThemeStyleSheet theme)
        {
            _theme = theme;
            return this;
        }

        /// <summary>Adds a style sheet applied after the theme (restyle the panel without modifying the package).</summary>
        public DebuggerBuilder AddStyleSheet(StyleSheet sheet)
        {
            if (sheet)
            {
                _styleSheets.Add(sheet);
            }

            return this;
        }

        /// <summary>Font of the panel (e.g. one with CJK glyphs). Null = theme default.</summary>
        public DebuggerBuilder UseFont(FontAsset font)
        {
            _font = font;
            return this;
        }

        /// <param name="enabled">False for <c>ATSU_DEBUGGER_DISABLED</c>: a passive service with no features, logs or UI.</param>
        /// <param name="playing">Play mode: state is persisted, the runner and the UI are created.</param>
        /// <param name="captureLogs">Listen to Unity's log (off in tests).</param>
        /// <param name="persistInEditMode">Use the configured storage outside play mode (tests).</param>
        internal DebugService Build(bool enabled, bool playing, bool captureLogs = true, bool persistInEditMode = false)
        {
            var persist = enabled && (playing || persistInEditMode);
            var service = new DebugService(Settings, enabled && captureLogs,
                // Edit mode (editor tools touching AtsuDebug.Instance) must not overwrite the player's saved state.
                persist ? Storage : new MemoryDebuggerStorage());
            if (!enabled)
            {
                return service;
            }

            DebuggerStrings.Language = Settings.General.Language;
            foreach (var (type, factory) in _optionViews)
            {
                service.OptionViews.Register(type, factory);
            }

            if (_bugReporter != null)
            {
                service.BugReports.Reporter = _bugReporter;
            }

            service.Install(_features);

            if (playing)
            {
                service.StartRunner();
                service.SetView(new PanelView(service,
                    new PanelViewResources(_theme, _styleSheets.ToArray(), _font)));
            }

            return service;
        }
    }
}