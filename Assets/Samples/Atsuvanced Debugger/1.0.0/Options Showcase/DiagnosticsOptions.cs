using System;
using Atsuvanced.Debugger;
using UnityEngine;
using UnityEngine.UIElements;

namespace AtsuvancedDebuggerSamples.Showcase
{
    /// <summary>
    /// Log generators for the Console tab, a stress test for the virtualized Options tab,
    /// a value type drawn by its own view (Percent → gauge) and an explicit section order (FR-CAT-08).
    /// </summary>
    [OptionCategory("Diagnostics")]
    [OptionCategorySort("Diagnostics/Gauge", -1)] // listed before "Logs" / "Stress"
    public sealed class DiagnosticsOptions
    {
        /// <summary>Simulated load (0-100). Also shown in the Profiler tab as a custom counter.</summary>
        [OptionCategory("Gauge"), OptionName("Load")]
        [OptionGroup("load", Label = "負荷シミュレーション（Percent 型のビュー）", Commit = OptionGroupCommit.Immediate)]
        public Percent Load { get; set; } = new Percent(30);

        [OptionCategory("Gauge"), OptionName("Reset"), OptionGroup("load"), OptionSort(1)]
        public void ResetLoad() => Load = new Percent(0);

        private DynamicOptionContainer _stress;

        [OptionCategory("Logs"), OptionName("Log")]
        public void LogInfo() => Debug.Log($"[Showcase] Info at {DateTime.Now:HH:mm:ss}");

        [OptionCategory("Logs"), OptionName("Warning")]
        public void LogWarning() => Debug.LogWarning("[Showcase] Something looks odd");

        [OptionCategory("Logs"), OptionName("Error")]
        public void LogError() => Debug.LogError("[Showcase] Something failed\nwith a second line of detail");

        [OptionCategory("Logs"), OptionName("Exception")]
        public void LogException() => Debug.LogException(new InvalidOperationException("[Showcase] Sample exception"));

        [OptionCategory("Logs"), OptionName("同じログ ×50（Collapse 確認用）")]
        public void SpamSame()
        {
            for (var i = 0; i < 50; i++) Debug.Log("[Showcase] Repeated message");
        }

        [OptionCategory("Logs"), OptionName("ログ 1000 件")]
        public void Spam1000()
        {
            for (var i = 0; i < 1000; i++) Debug.Log($"[Showcase] Bulk log #{i}  hp={UnityEngine.Random.Range(0, 100)}");
        }

        [OptionCategory("Stress"), OptionName("オプションを 1000 件追加"), OptionSort(0)]
        [OptionDescription("Adds 1000 options under Stress Test/Block N to try the virtualized list and search")]
        public void AddStressOptions()
        {
            if (_stress == null)
            {
                _stress = new DynamicOptionContainer();
                AtsuDebug.Instance.AddOptionContainer(_stress, "Stress Test");
            }

            var start = _stress.Options.Count;
            for (var i = start; i < start + 1000; i++)
            {
                var n = i;
                var value = n;
                _stress.AddOption(OptionDefinition.Create($"Value {n}", () => value, v => value = v, $"Block {n / 100}"));
            }
        }

        [OptionCategory("Stress"), OptionName("追加したオプションを削除"), OptionSort(1)]
        public void RemoveStressOptions()
        {
            if (_stress == null) return;
            AtsuDebug.Instance.RemoveOptionContainer(_stress);
            _stress = null;
        }
    }
}
