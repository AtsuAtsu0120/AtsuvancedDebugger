using System.Collections.Generic;
using Atsuvanced.Debugger;
using UnityEngine;
using UnityEngine.UIElements;

namespace AtsuvancedDebuggerSamples.Showcase
{
    /// <summary>
    /// Option groups: several members shown and operated as one UI unit.
    /// </summary>
    [OptionCategory("World")]
    public sealed class WorldOptions
    {
        // ---- Input + button: the input is only committed when the button is pressed ------------
        // (a group with a writable property and a method defaults to Commit = OnAction)

        [OptionCategory("Map"), OptionGroup("warp", Label = "ワープ"), OptionName("行き先")]
        public StageName WarpTarget { get; set; } = new StageName("Town");

        [OptionCategory("Map"), OptionGroup("warp"), OptionName("ワープ実行"), OptionSort(1)]
        public void Warp() => Debug.Log($"[Showcase] Warp to {WarpTarget}");

        [OptionCategory("Map"), OptionGroup("teleport", Label = "座標テレポート", Layout = OptionGroupLayout.Vertical), OptionName("座標")]
        public Vector2 TeleportTo { get; set; }

        [OptionCategory("Map"), OptionGroup("teleport"), OptionName("テレポート"), OptionSort(1)]
        public void Teleport() => Debug.Log($"[Showcase] Teleport to {TeleportTo}");

        // ---- Immediate group: value + preset buttons, applied right away ----------------------

        [OptionCategory("Time"), OptionGroup("timescale", Label = "タイムスケール", Commit = OptionGroupCommit.Immediate)]
        [NumberRange(0, 4), OptionName("倍率")]
        public float TimeScale
        {
            get => Time.timeScale;
            set => Time.timeScale = value;
        }

        [OptionCategory("Time"), OptionGroup("timescale"), OptionName("停止"), OptionSort(1)]
        public void Pause() => Time.timeScale = 0f;

        [OptionCategory("Time"), OptionGroup("timescale"), OptionName("×1"), OptionSort(2)]
        public void Normal() => Time.timeScale = 1f;

        [OptionCategory("Time"), OptionGroup("timescale"), OptionName("×2"), OptionSort(3)]
        public void Fast() => Time.timeScale = 2f;

        [OptionCategory("Time"), OptionName("目標 FPS")]
        public int TargetFrameRate
        {
            get => Application.targetFrameRate;
            set => Application.targetFrameRate = value;
        }

        // ---- Vertical group bound to the main camera ---------------------------------------

        [OptionCategory("Camera"), OptionGroup("camera", Label = "メインカメラ", Layout = OptionGroupLayout.Vertical,
             Commit = OptionGroupCommit.Immediate)]
        [OptionName("Orthographic Size"), NumberRange(1, 20)]
        public float OrthoSize
        {
            get => Camera.main ? Camera.main.orthographicSize : 0f;
            set
            {
                if (Camera.main) Camera.main.orthographicSize = value;
            }
        }

        [OptionCategory("Camera"), OptionGroup("camera"), OptionName("背景色")]
        public Color Background
        {
            get => Camera.main ? Camera.main.backgroundColor : Color.black;
            set
            {
                if (Camera.main) Camera.main.backgroundColor = value;
            }
        }

        [OptionCategory("Camera"), OptionGroup("camera"), OptionName("リセット"), OptionSort(10)]
        public void ResetCamera()
        {
            OrthoSize = 5f;
            Background = new Color(0.19f, 0.30f, 0.47f);
        }

        // ---- A read-only property of a type with its own view ----------------------------------

        [OptionCategory("Time"), OptionName("フレームモニタ")]
        public FrameStats FrameMonitor => FrameStats.Current;
    }

    /// <summary>A stage name. Drawn by <see cref="StageNameView" /> (registered per type in OptionsShowcase).</summary>
    public readonly struct StageName
    {
        public readonly string Value;

        public StageName(string value) => Value = value;

        public override string ToString() => Value ?? "";
    }

    /// <summary>Type view: every StageName option becomes a dropdown of the stages decided at runtime.</summary>
    public sealed class StageNameView : OptionView
    {
        public static readonly List<string> Stages = new List<string> { "Town", "Forest", "Dungeon B1", "Dungeon B2", "Castle", "ラスボス部屋" };

        private DropdownField _dropdown;

        public override VisualElement CreateElement(OptionContext context)
        {
            _dropdown = new DropdownField(Stages, 0);
            _dropdown.SetEnabled(context.CanWrite);
            _dropdown.RegisterValueChangedCallback(e => context.SetValue(new StageName(e.newValue)));
            return _dropdown;
        }

        public override void Refresh(OptionContext context) => _dropdown.SetValueWithoutNotify(context.GetValue<StageName>().Value);
    }

    /// <summary>A snapshot of the frame time.</summary>
    public readonly struct FrameStats
    {
        public readonly float Milliseconds;

        public FrameStats(float milliseconds) => Milliseconds = milliseconds;

        public static FrameStats Current => new FrameStats(Time.unscaledDeltaTime * 1000f);

        public float Fps => 1000f / Mathf.Max(Milliseconds, 0.001f);

        public override string ToString() => $"{Fps:F0} FPS ({Milliseconds:F1} ms)";
    }

    /// <summary>Type view: FPS / frame time with a colored bar. Refresh is called while the row is visible.</summary>
    public sealed class FrameStatsView : OptionView
    {
        private Label _label;
        private VisualElement _bar;

        public override VisualElement CreateElement(OptionContext context)
        {
            var root = new VisualElement();
            _label = new Label();
            root.Add(_label);
            var track = new VisualElement();
            track.style.height = 6;
            track.style.marginTop = 4;
            track.style.backgroundColor = new Color(1, 1, 1, 0.08f);
            track.style.borderTopLeftRadius = track.style.borderTopRightRadius =
                track.style.borderBottomLeftRadius = track.style.borderBottomRightRadius = 3;
            _bar = new VisualElement();
            _bar.style.height = Length.Percent(100);
            _bar.style.borderTopLeftRadius = _bar.style.borderTopRightRadius =
                _bar.style.borderBottomLeftRadius = _bar.style.borderBottomRightRadius = 3;
            track.Add(_bar);
            root.Add(track);
            return root;
        }

        public override void Refresh(OptionContext context)
        {
            var stats = context.GetValue<FrameStats>();
            var ms = stats.Milliseconds;
            _label.text = stats.ToString();
            _bar.style.width = Length.Percent(Mathf.Clamp01(ms / 33.3f) * 100f);
            _bar.style.backgroundColor = ms < 17f ? new Color(0.3f, 0.8f, 0.4f) : ms < 34f ? new Color(0.95f, 0.75f, 0.3f) : new Color(0.93f, 0.36f, 0.36f);
        }
    }
}
