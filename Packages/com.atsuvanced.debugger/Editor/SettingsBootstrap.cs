using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;
using UnityEngine.UIElements;

namespace Atsuvanced.Debugger.Editor
{
    /// <summary>
    ///     Makes sure a <see cref="DebuggerSettingsAsset" /> exists, has the package theme, and is registered as a
    ///     Preloaded Asset (so players can find it without Resources). Runs before entering play mode and building.
    /// </summary>
    [InitializeOnLoad]
    internal static class SettingsBootstrap
    {
        public const string DefaultAssetPath = "Assets/AtsuvancedDebugger/AtsuDebuggerSettings.asset";
        public const string ThemePath = "Packages/com.atsuvanced.debugger/Runtime/UI/Styles/AtsuDebuggerTheme.tss";

        static SettingsBootstrap()
        {
            EditorApplication.playModeStateChanged += state =>
            {
                if (state == PlayModeStateChange.ExitingEditMode)
                {
                    EnsureAsset();
                }
            };
        }

        [MenuItem("Tools/Atsuvanced Debugger/Settings", priority = 0)]
        private static void SelectSettings()
        {
            Selection.activeObject = EnsureAsset();
            EditorGUIUtility.PingObject(Selection.activeObject);
        }

        public static DebuggerSettingsAsset EnsureAsset()
        {
            var asset = Find();
            if (asset == null)
            {
                Directory.CreateDirectory(Path.GetDirectoryName(DefaultAssetPath)!);
                asset = ScriptableObject.CreateInstance<DebuggerSettingsAsset>();
                AssetDatabase.CreateAsset(asset, DefaultAssetPath);
                Debug.Log($"[AtsuvancedDebugger] Created settings asset at {DefaultAssetPath}");
            }

            if (asset.Theme == null)
            {
                asset.Theme = AssetDatabase.LoadAssetAtPath<ThemeStyleSheet>(ThemePath);
                EditorUtility.SetDirty(asset);
                AssetDatabase.SaveAssetIfDirty(asset);
            }

            SetPreloaded(asset, true);
            return asset;
        }

        public static DebuggerSettingsAsset Find()
        {
            var guid = AssetDatabase.FindAssets("t:" + nameof(DebuggerSettingsAsset)).FirstOrDefault();
            return guid == null
                ? null
                : AssetDatabase.LoadAssetAtPath<DebuggerSettingsAsset>(AssetDatabase.GUIDToAssetPath(guid));
        }

        public static void SetPreloaded(Object asset, bool preloaded)
        {
            var list = PlayerSettings.GetPreloadedAssets().Where(a => a != null).ToList();
            var contains = list.Contains(asset);
            if (preloaded == contains)
            {
                return;
            }

            if (preloaded)
            {
                list.Add(asset);
            }
            else
            {
                list.Remove(asset);
            }

            PlayerSettings.SetPreloadedAssets(list.ToArray());
            AssetDatabase.SaveAssets();
        }
    }

    /// <summary>Excludes the settings asset (and with it the theme) from builds where the debugger is disabled.</summary>
    internal sealed class SettingsBuildProcessor : IPreprocessBuildWithReport, IPostprocessBuildWithReport
    {
        private const string DisabledSymbol = "ATSU_DEBUGGER_DISABLED";

        public void OnPostprocessBuild(BuildReport report)
        {
            var asset = SettingsBootstrap.Find();
            if (asset != null)
            {
                SettingsBootstrap.SetPreloaded(asset, true);
            }
        }

        public int callbackOrder => 0;

        public void OnPreprocessBuild(BuildReport report)
        {
            var asset = SettingsBootstrap.EnsureAsset();
            if (IsDisabled(report))
            {
                SettingsBootstrap.SetPreloaded(asset, false);
            }
        }

        private static bool IsDisabled(BuildReport report)
        {
            var target = NamedBuildTarget.FromBuildTargetGroup(report.summary.platformGroup);
            PlayerSettings.GetScriptingDefineSymbols(target, out var defines);
            return defines.Contains(DisabledSymbol);
        }
    }
}