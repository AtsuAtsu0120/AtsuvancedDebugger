using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.TextCore.Text;
using UnityEngine.UIElements;

namespace Atsuvanced.Debugger
{
    /// <summary>
    ///     Project-wide settings and UI resources: the serialized defaults that <c>DebuggerBuilder.CreateDefault</c> starts
    ///     from (like a Render Pipeline Asset). Which features run is decided in code with the builder. Created automatically
    ///     by the editor
    ///     (Assets/AtsuvancedDebugger/AtsuDebuggerSettings.asset) and registered as a Preloaded Asset,
    ///     so it is available in players without a Resources folder.
    /// </summary>
    [CreateAssetMenu(menuName = "Atsuvanced Debugger/Settings", fileName = "AtsuDebuggerSettings")]
    public sealed class DebuggerSettingsAsset : ScriptableObject
    {
        private static DebuggerSettingsAsset s_instance;
        [SerializeField] private DebuggerSettings _settings = new();

        [Header("UI Resources")] [Tooltip("Theme of the debug panel. Defaults to the package theme.")] [SerializeField]
        private ThemeStyleSheet _theme;

        [Tooltip(
            "Extra style sheets applied after the theme. Use these to restyle the panel without modifying the package.")]
        [SerializeField]
        private List<StyleSheet> _styleSheets = new();

        [Tooltip("Font used by the panel (e.g. a font asset with CJK glyphs). Empty = theme default.")] [SerializeField]
        private FontAsset _font;

        public DebuggerSettings Settings => _settings;

        public ThemeStyleSheet Theme
        {
            get => _theme;
            set => _theme = value;
        }

        public List<StyleSheet> StyleSheets => _styleSheets;
        public FontAsset Font => _font;

        /// <summary>The active settings asset, or null if none exists.</summary>
        public static DebuggerSettingsAsset Instance
        {
            get
            {
                if (s_instance)
                {
                    return s_instance;
                }

                var loaded = Resources.FindObjectsOfTypeAll<DebuggerSettingsAsset>();
                if (loaded.Length > 0)
                {
                    return s_instance = loaded[0];
                }
#if UNITY_EDITOR
                var guids = AssetDatabase.FindAssets("t:" + nameof(DebuggerSettingsAsset));
                if (guids.Length > 0)
                {
                    s_instance = AssetDatabase.LoadAssetAtPath<DebuggerSettingsAsset>(
                        AssetDatabase.GUIDToAssetPath(guids[0]));
                }
#endif
                return s_instance;
            }
        }

        private void OnEnable()
        {
            if (!s_instance)
            {
                s_instance = this;
            }
        }

        private void OnDisable()
        {
            if (s_instance == this)
            {
                s_instance = null;
            }
        }
    }
}