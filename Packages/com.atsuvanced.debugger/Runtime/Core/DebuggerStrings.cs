using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;

namespace Atsuvanced.Debugger
{
    /// <summary>Language of the built-in UI.</summary>
    public enum DebuggerLanguage
    {
        /// <summary>Japanese when the system language is Japanese, otherwise English.</summary>
        Auto,
        English,
        Japanese
    }

    /// <summary>
    ///     String table of the built-in UI (English and Japanese are bundled, NFR-11).
    ///     The language is taken from <see cref="GeneralSettings.Language" /> (or <c>DebuggerBuilder.UseLanguage</c>) when the
    ///     debugger initializes.
    ///     Any string can be replaced with <see cref="Override" /> (call it before the panel is first shown;
    ///     tab and section titles are read when the debugger initializes).
    /// </summary>
    public static class DebuggerStrings
    {
        private static DebuggerLanguage s_language = DebuggerLanguage.Auto;
        private static Dictionary<string, string> s_active;

        private static readonly Dictionary<string, string>[] Overrides =
        {
            new(), new()
        };

        // ---- Tables -------------------------------------------------------------------------

        private static readonly Dictionary<string, string> English = new()
        {
            // Tabs / sections
            ["tab.system"] = "System",
            ["tab.search"] = "Search",
            ["tab.options"] = "Options",
            ["tab.pinned"] = "Pinned",
            ["tab.console"] = "Console",
            ["tab.profiler"] = "Profiler",
            ["tab.bugReport"] = "Bug Report",
            ["section.logs"] = "Logs",
            ["section.general"] = "General",
            ["section.empty"] = "Nothing here yet.",
            ["section.overview"] = "Overview",

            // Common
            ["common.copy"] = "Copy",
            ["common.copyAll"] = "Copy All",
            ["common.clear"] = "Clear",
            ["common.open"] = "Open",
            ["common.close"] = "Close",
            ["common.cancel"] = "Cancel",
            ["common.back"] = "Back",

            // Options
            ["options.search"] = "Search",
            ["options.searchPlaceholder"] = "Search all options (name, category, description)",
            ["options.searchHit"] = "1 hit",
            ["options.searchHits"] = "{0} hits",
            ["options.noMatches"] = "No matching options.",
            ["options.emptySection"] = "No options in this section.",
            ["options.pin"] = "Pin",
            ["options.pending"] = "Pending",
            ["options.revert"] = "Revert",
            ["options.unsupported"] = "Unsupported type: {0}",
            ["pinned.title"] = "Pinned",
            ["pinned.empty"] = "Nothing is pinned. Use the round button on the right of an option to pin it.",
            ["pinned.unpinAll"] = "Unpin All",
            ["pinned.unpinConfirm"] = "Unpin {0}? Tap again",

            // Console
            ["console.filter"] = "Filter",
            ["console.regex"] = "Regular expression",
            ["console.collapse"] = "Collapse",
            ["console.log"] = "Log {0}",
            ["console.warn"] = "Warn {0}",
            ["console.error"] = "Error {0}",
            ["console.dock"] = "Dock",
            ["console.undock"] = "Undock",
            ["console.dockTooltip"] = "Show the latest logs on the game screen while the panel is closed",
            ["console.save"] = "Save",
            ["console.saveTooltip"] = "Write every log to a text file",
            ["console.saved"] = "Saved: {0}",
            ["console.saveFailed"] = "Could not save the log: {0}",
            ["console.noStackTrace"] = "(no stack trace)",

            // Profiler
            ["profiler.dock"] = "Dock",
            ["profiler.undock"] = "Undock",
            ["profiler.dockTooltip"] = "Show FPS and memory on the game screen while the panel is closed",
            ["profiler.reset"] = "Reset",
            ["profiler.frame"] = "Frame",
            ["profiler.fps"] = "FPS",
            ["profiler.frameTime"] = "Frame Time",
            ["profiler.cpuMain"] = "CPU Main Thread",
            ["profiler.cpuRender"] = "CPU Render Thread",
            ["profiler.gpu"] = "GPU",
            ["profiler.memory"] = "Memory",
            ["profiler.rendering"] = "Rendering",
            ["profiler.custom"] = "Custom",
            ["profiler.unavailable"] = "n/a",
            ["profiler.unavailableNote"] =
                "n/a = not available on this platform / build (e.g. render counters need a development build).",
            ["profiler.graphLegend"] = "Frame time (ms) — target {0}",
            ["profiler.min"] = "Min",
            ["profiler.avg"] = "Avg",
            ["profiler.max"] = "Max",

            // Bug report
            ["bug.title"] = "Bug Report",
            ["bug.description"] = "Description",
            ["bug.descriptionPlaceholder"] = "What happened? What did you expect?",
            ["bug.email"] = "Email (optional)",
            ["bug.includeScreenshot"] = "Screenshot (taken without the debug panel)",
            ["bug.includeLogs"] = "Console log ({0} entries)",
            ["bug.includeSystemInfo"] = "System information",
            ["bug.retake"] = "Retake",
            ["bug.capture"] = "Capture",
            ["bug.noScreenshot"] = "No screenshot yet.",
            ["bug.send"] = "Send",
            ["bug.sending"] = "Sending… {0}%",
            ["bug.sent"] = "Sent. {0}",
            ["bug.failed"] = "Failed: {0}",
            ["bug.emptyDescription"] = "Please enter a description.",
            ["bug.savedTo"] = "Saved to {0}",
            ["bug.discardConfirm"] = "Discard the report? Tap back again",

            // Entry code
            ["entry.title"] = "Enter PIN",
            ["entry.wrong"] = "Wrong PIN",
            ["entry.delete"] = "Del",

            // Docked overlays / misc
            ["dock.undock"] = "Undock",
            ["error.page"] = "Failed to build page '{0}'",
            ["error.option"] = "Failed to draw option '{0}'"
        };

        private static readonly Dictionary<string, string> Japanese = new()
        {
            ["tab.system"] = "システム",
            ["tab.search"] = "検索",
            ["tab.options"] = "オプション",
            ["tab.pinned"] = "ピン",
            ["tab.console"] = "コンソール",
            ["tab.profiler"] = "プロファイラ",
            ["tab.bugReport"] = "バグ報告",
            ["section.logs"] = "ログ",
            ["section.general"] = "全般",
            ["section.empty"] = "まだ何もありません。",
            ["section.overview"] = "概要",

            ["common.copy"] = "コピー",
            ["common.copyAll"] = "すべてコピー",
            ["common.clear"] = "クリア",
            ["common.open"] = "開く",
            ["common.close"] = "閉じる",
            ["common.cancel"] = "キャンセル",
            ["common.back"] = "戻る",

            ["options.search"] = "検索",
            ["options.searchPlaceholder"] = "すべてのオプションを検索（名前・カテゴリ・説明）",
            ["options.searchHit"] = "1 件",
            ["options.searchHits"] = "{0} 件",
            ["options.noMatches"] = "一致するオプションはありません。",
            ["options.emptySection"] = "このセクションにオプションはありません。",
            ["options.pin"] = "ピン留め",
            ["options.pending"] = "未確定",
            ["options.revert"] = "元に戻す",
            ["options.unsupported"] = "未対応の型: {0}",
            ["pinned.title"] = "ピン",
            ["pinned.empty"] = "ピン留めしたオプションはありません。オプションの右の丸いボタンでピン留めできます。",
            ["pinned.unpinAll"] = "すべて解除",
            ["pinned.unpinConfirm"] = "{0} 件を解除？もう一度タップ",

            ["console.filter"] = "フィルタ",
            ["console.regex"] = "正規表現",
            ["console.collapse"] = "まとめる",
            ["console.log"] = "ログ {0}",
            ["console.warn"] = "警告 {0}",
            ["console.error"] = "エラー {0}",
            ["console.dock"] = "常駐",
            ["console.undock"] = "常駐解除",
            ["console.dockTooltip"] = "パネルを閉じている間、最新のログをゲーム画面に表示する",
            ["console.save"] = "保存",
            ["console.saveTooltip"] = "すべてのログをテキストファイルに書き出す",
            ["console.saved"] = "保存しました: {0}",
            ["console.saveFailed"] = "ログを保存できませんでした: {0}",
            ["console.noStackTrace"] = "（スタックトレースなし）",

            ["profiler.dock"] = "常駐",
            ["profiler.undock"] = "常駐解除",
            ["profiler.dockTooltip"] = "パネルを閉じている間、FPS とメモリをゲーム画面に表示する",
            ["profiler.reset"] = "リセット",
            ["profiler.frame"] = "フレーム",
            ["profiler.fps"] = "FPS",
            ["profiler.frameTime"] = "フレーム時間",
            ["profiler.cpuMain"] = "CPU メインスレッド",
            ["profiler.cpuRender"] = "CPU レンダースレッド",
            ["profiler.gpu"] = "GPU",
            ["profiler.memory"] = "メモリ",
            ["profiler.rendering"] = "描画",
            ["profiler.custom"] = "カスタム",
            ["profiler.unavailable"] = "取得不可",
            ["profiler.unavailableNote"] = "取得不可 = このプラットフォーム / ビルドでは取れない値（描画のカウンタは Development Build が必要など）。",
            ["profiler.graphLegend"] = "フレーム時間（ms） — 目標 {0}",
            ["profiler.min"] = "最小",
            ["profiler.avg"] = "平均",
            ["profiler.max"] = "最大",

            ["bug.title"] = "バグ報告",
            ["bug.description"] = "説明",
            ["bug.descriptionPlaceholder"] = "何が起きましたか？本来はどうなるはずでしたか？",
            ["bug.email"] = "メールアドレス（任意）",
            ["bug.includeScreenshot"] = "スクリーンショット（デバッグパネルを除いて撮影）",
            ["bug.includeLogs"] = "コンソールのログ（{0} 件）",
            ["bug.includeSystemInfo"] = "システム情報",
            ["bug.retake"] = "撮り直す",
            ["bug.capture"] = "撮影",
            ["bug.noScreenshot"] = "スクリーンショットはまだありません。",
            ["bug.send"] = "送信",
            ["bug.sending"] = "送信中… {0}%",
            ["bug.sent"] = "送信しました。{0}",
            ["bug.failed"] = "失敗しました: {0}",
            ["bug.emptyDescription"] = "説明を入力してください。",
            ["bug.savedTo"] = "保存先: {0}",
            ["bug.discardConfirm"] = "報告を破棄しますか？もう一度戻るを押してください",

            ["entry.title"] = "PIN を入力",
            ["entry.wrong"] = "PIN が違います",
            ["entry.delete"] = "削除",

            ["dock.undock"] = "常駐解除",
            ["error.page"] = "ページ '{0}' を作れませんでした",
            ["error.option"] = "オプション '{0}' を描画できませんでした"
        };

        /// <summary>The configured language (Auto is resolved by <see cref="EffectiveLanguage" />).</summary>
        public static DebuggerLanguage Language
        {
            get => s_language;
            set
            {
                s_language = value;
                s_active = null;
            }
        }

        /// <summary>English or Japanese.</summary>
        public static DebuggerLanguage EffectiveLanguage
        {
            get
            {
                if (s_language != DebuggerLanguage.Auto)
                {
                    return s_language;
                }

                return Application.systemLanguage == SystemLanguage.Japanese
                    ? DebuggerLanguage.Japanese
                    : DebuggerLanguage.English;
            }
        }

        /// <summary>Every bundled key (for tests and tooling).</summary>
        internal static IEnumerable<string> Keys => English.Keys;

        /// <summary>The string for <paramref name="key" /> in the current language (English, then the key itself, as fallbacks).</summary>
        public static string Get(string key)
        {
            if (key == null)
            {
                return string.Empty;
            }

            var lang = EffectiveLanguage;
            if (Overrides[Index(lang)].TryGetValue(key, out var o))
            {
                return o;
            }

            s_active ??= lang == DebuggerLanguage.Japanese ? Japanese : English;
            if (s_active.TryGetValue(key, out var v))
            {
                return v;
            }

            return English.TryGetValue(key, out v) ? v : key;
        }

        /// <summary><see cref="Get" /> + <see cref="string.Format(string, object[])" /> (invariant culture).</summary>
        public static string Format(string key, params object[] args)
        {
            return string.Format(CultureInfo.InvariantCulture, Get(key), args);
        }

        /// <summary>Replaces a built-in string (or adds one for your own pages) for a language.</summary>
        public static void Override(DebuggerLanguage language, string key, string value)
        {
            if (string.IsNullOrEmpty(key))
            {
                throw new ArgumentException("Key is required.", nameof(key));
            }

            if (language == DebuggerLanguage.Auto)
            {
                throw new ArgumentException("Specify English or Japanese.", nameof(language));
            }

            if (value == null)
            {
                Overrides[Index(language)].Remove(key);
            }
            else
            {
                Overrides[Index(language)][key] = value;
            }
        }

        internal static bool HasJapanese(string key)
        {
            return Japanese.ContainsKey(key);
        }

        private static int Index(DebuggerLanguage language)
        {
            return language == DebuggerLanguage.Japanese ? 1 : 0;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            s_language = DebuggerLanguage.Auto;
            s_active = null;
            foreach (var d in Overrides)
            {
                d.Clear();
            }
        }
    }
}