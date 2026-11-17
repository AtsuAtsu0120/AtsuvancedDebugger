using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace Atsuvanced.Debugger.Editor
{
    /// <summary>
    ///     Project Settings &gt; Atsuvanced Debugger (FR-EDT-01): edits the settings asset (Preloaded Assets, no Resources)
    ///     and toggles <c>ATSU_DEBUGGER_DISABLED</c> for the active build target.
    /// </summary>
    internal sealed class DebuggerSettingsProvider : SettingsProvider
    {
        public const string SettingsPath = "Project/Atsuvanced Debugger";
        private Label _assetPath;
        private Toggle _disabledToggle;

        private SerializedObject _serialized;

        private DebuggerSettingsProvider() : base(SettingsPath, SettingsScope.Project, Keywords())
        {
        }

        [SettingsProvider]
        public static SettingsProvider Create()
        {
            return new DebuggerSettingsProvider();
        }

        [MenuItem("Tools/Atsuvanced Debugger/Project Settings", priority = 1)]
        private static void Open()
        {
            SettingsService.OpenProjectSettings(SettingsPath);
        }

        private static IEnumerable<string> Keywords()
        {
            return new[]
            {
                "Atsuvanced", "Debugger", "Trigger", "Entry Code", "PIN", "Console", "Profiler",
                "Bug Report",
                "Pinned", "Theme", "Language", "Shortcut"
            };
        }

        public override void OnActivate(string searchContext, VisualElement rootElement)
        {
            var asset = SettingsBootstrap.EnsureAsset();
            _serialized = new SerializedObject(asset);

            var root = new ScrollView(ScrollViewMode.Vertical)
                { horizontalScrollerVisibility = ScrollerVisibility.Hidden };
            root.style.paddingLeft = 10;
            root.style.paddingRight = 10;
            root.style.paddingTop = 4;
            root.AddToClassList(InspectorElement.ussClassName); // aligned label column like the Inspector
            rootElement.Add(root);

            var title = new Label("Atsuvanced Debugger");
            title.style.fontSize = 19;
            title.style.unityFontStyleAndWeight = FontStyle.Bold;
            title.style.marginBottom = 6;
            root.Add(title);

            _assetPath = new Label();
            _assetPath.style.color = new Color(0.6f, 0.6f, 0.6f);
            _assetPath.style.marginBottom = 4;
            root.Add(_assetPath);

            var buttons = new VisualElement { style = { flexDirection = FlexDirection.Row, marginBottom = 8 } };
            buttons.Add(new Button(() =>
            {
                EditorGUIUtility.PingObject(asset);
                Selection.activeObject = asset;
            }) { text = "Select Asset" });
            buttons.Add(new Button(() => EditorUtility.RevealInFinder(BugReportFolder(asset)))
                { text = "Open Bug Report Folder" });
            buttons.Add(new Button(() =>
            {
                if (!EditorUtility.DisplayDialog("Atsuvanced Debugger",
                        "Delete the debugger's saved state (pins, open tabs, saved option values) " +
                        "from PlayerPrefs?", "Delete", "Cancel"))
                {
                    return;
                }

                DeleteSavedState();
            }) { text = "Clear Saved State" });
            root.Add(buttons);

            _disabledToggle = new Toggle("Disable for current build target (ATSU_DEBUGGER_DISABLED)");
            _disabledToggle.tooltip =
                "Turns every API into a no-op and excludes the settings asset and theme from builds of this target.";
            _disabledToggle.RegisterValueChangedCallback(e => DefineSymbolMenu.SetDisabled(e.newValue));
            root.Add(_disabledToggle);

            var help = new HelpBox("Changes apply the next time the debugger initializes (enter Play Mode again). " +
                                   "The asset is registered as a Preloaded Asset, so it is available in players without a Resources folder.",
                HelpBoxMessageType.Info);
            help.style.marginTop = 6;
            help.style.marginBottom = 6;
            root.Add(help);

            // Settings first (expanded, without the "Settings" foldout), then the UI resources; no "Script" row.
            var settings = _serialized.FindProperty("_settings");
            var end = settings.GetEndProperty();
            var child = settings.Copy();
            for (var enter = true;
                 child.NextVisible(enter) && !SerializedProperty.EqualContents(child, end);
                 enter = false)
            {
                root.Add(Field(child));
            }

            var it = _serialized.GetIterator();
            for (var enter = true; it.NextVisible(enter); enter = false)
            {
                if (it.name == "m_Script" || it.name == "_settings")
                {
                    continue;
                }

                root.Add(Field(it));
            }

            root.schedule.Execute(Refresh).Every(1000);
            Refresh();
        }

        private PropertyField Field(SerializedProperty property)
        {
            var field = new PropertyField(property.Copy());
            field.Bind(_serialized);
            return field;
        }

        private void Refresh()
        {
            if (_serialized?.targetObject == null)
            {
                return;
            }

            _assetPath.text = AssetDatabase.GetAssetPath(_serialized.targetObject);
            _disabledToggle.SetValueWithoutNotify(DefineSymbolMenu.IsDisabled());
        }

        private static string BugReportFolder(DebuggerSettingsAsset asset)
        {
            var folder = asset.Settings.BugReport.Folder;
            if (string.IsNullOrEmpty(folder))
            {
                folder = "AtsuvancedDebugger/BugReports";
            }

            var path = Path.IsPathRooted(folder) ? folder : Path.Combine(Application.persistentDataPath, folder);
            Directory.CreateDirectory(path);
            return path;
        }

        /// <summary>
        ///     Deletes the debugger's keys from the Editor's PlayerPrefs (the same as
        ///     <see cref="AtsuDebug.ClearSavedState" />).
        /// </summary>
        private static void DeleteSavedState()
        {
            var removed = new DebuggerState(new PlayerPrefsDebuggerStorage()).Clear();
            Debug.Log($"[AtsuvancedDebugger] Deleted {removed} saved keys.");
        }
    }
}