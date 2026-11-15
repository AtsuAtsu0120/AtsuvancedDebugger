using System.Linq;
using UnityEditor;
using UnityEditor.Build;

namespace Atsuvanced.Debugger.Editor
{
    /// <summary>Toggles ATSU_DEBUGGER_DISABLED for the active build target (FR-EDT-02).</summary>
    internal static class DefineSymbolMenu
    {
        private const string MenuPath = "Tools/Atsuvanced Debugger/Disable For Current Build Target";
        private const string Symbol = "ATSU_DEBUGGER_DISABLED";

        [MenuItem(MenuPath)]
        private static void Toggle()
        {
            SetDisabled(!IsDisabled());
        }

        /// <summary>Whether ATSU_DEBUGGER_DISABLED is defined for the active build target.</summary>
        public static bool IsDisabled()
        {
            PlayerSettings.GetScriptingDefineSymbols(CurrentTarget(), out var defines);
            return defines.Contains(Symbol);
        }

        public static void SetDisabled(bool disabled)
        {
            var target = CurrentTarget();
            PlayerSettings.GetScriptingDefineSymbols(target, out var defines);
            var list = defines.ToList();
            if (disabled == list.Contains(Symbol))
            {
                return;
            }

            if (disabled)
            {
                list.Add(Symbol);
            }
            else
            {
                list.Remove(Symbol);
            }

            PlayerSettings.SetScriptingDefineSymbols(target, list.ToArray());
        }

        [MenuItem(MenuPath, true)]
        private static bool ToggleValidate()
        {
            PlayerSettings.GetScriptingDefineSymbols(CurrentTarget(), out var defines);
            Menu.SetChecked(MenuPath, defines.Contains(Symbol));
            return true;
        }

        private static NamedBuildTarget CurrentTarget()
        {
            var group = BuildPipeline.GetBuildTargetGroup(EditorUserBuildSettings.activeBuildTarget);
            return NamedBuildTarget.FromBuildTargetGroup(group);
        }
    }
}