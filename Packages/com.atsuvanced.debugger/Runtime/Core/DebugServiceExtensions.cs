using System;

namespace Atsuvanced.Debugger
{
    /// <summary>Shortcuts for common calls.</summary>
    public static class DebugServiceExtensions
    {
        // ---- Panel ----------------------------------------------------------------------

        public static bool IsDebugPanelVisible(this IDebugService service)
        {
            return service.Panel.IsVisible;
        }

        /// <inheritdoc cref="IDebugPanel.Show" />
        public static void ShowDebugPanel(this IDebugService service, string tabId = null, bool requireEntryCode = true)
        {
            service.Panel.Show(tabId, requireEntryCode);
        }

        public static void HideDebugPanel(this IDebugService service)
        {
            service.Panel.Hide();
        }

        /// <inheritdoc cref="IDebugPanel.Destroy" />
        public static void DestroyDebugPanel(this IDebugService service)
        {
            service.Panel.Destroy();
        }

        /// <summary>Opens the panel (if hidden) and pushes the page on the current tab.</summary>
        public static void PushPage(this IDebugService service, DebugPage page)
        {
            if (page == null)
            {
                throw new ArgumentNullException(nameof(page));
            }

            if (!service.Panel.IsVisible)
            {
                service.Panel.Show();
            }

            service.Pages.Push(page);
        }

        // ---- Options --------------------------------------------------------------------

        /// <inheritdoc cref="OptionRegistry.AddContainer" />
        public static void AddOptionContainer(this IDebugService service, object container,
            string categoryPrefix = null)
        {
            service.Options.AddContainer(container, categoryPrefix);
        }

        public static bool RemoveOptionContainer(this IDebugService service, object container)
        {
            return service.Options.RemoveContainer(container);
        }

        public static void AddOption(this IDebugService service, OptionDefinition option)
        {
            service.Options.AddOption(option);
        }

        public static bool RemoveOption(this IDebugService service, OptionDefinition option)
        {
            return service.Options.RemoveOption(option);
        }

        /// <summary>Removes every option in the category and its sub-categories. Returns the removed count.</summary>
        public static int RemoveOptionCategory(this IDebugService service, string categoryPath)
        {
            return service.Options.RemoveCategory(categoryPath);
        }

        public static void PinOption(this IDebugService service, string name)
        {
            service.Options.PinByName(name);
        }

        public static void UnpinOption(this IDebugService service, string name)
        {
            service.Options.UnpinByName(name);
        }

        public static void PinAllOptions(this IDebugService service, string category)
        {
            service.Options.PinCategory(category);
        }

        public static void UnpinAllOptions(this IDebugService service, string category)
        {
            service.Options.UnpinCategory(category);
        }

        public static void ClearPinnedOptions(this IDebugService service)
        {
            service.Options.ClearPinned();
        }

        // ---- System info / bug report ---------------------------------------------------

        public static void AddSystemInfo(this IDebugService service, InfoEntry entry,
            string category = SystemInfoRegistry.DefaultCategory)
        {
            service.SystemInfo.Add(entry, category);
        }

        /// <inheritdoc cref="BugReportService.ShowSheet" />
        public static void ShowBugReportSheet(this IDebugService service, Action<bool> onComplete = null,
            bool takeScreenshot = true, string description = null)
        {
            service.BugReports.ShowSheet(onComplete, takeScreenshot, description);
        }
    }
}