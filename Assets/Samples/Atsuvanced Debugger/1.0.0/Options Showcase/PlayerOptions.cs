using System.ComponentModel;
using Atsuvanced.Debugger;
using UnityEngine;

namespace AtsuvancedDebuggerSamples.Showcase
{
    public enum Difficulty
    {
        Easy,
        Normal,
        Hard,
        Nightmare,
    }

    [System.Flags]
    public enum Buffs
    {
        None = 0,
        Haste = 1,
        Shield = 2,
        Regen = 4,
        Stealth = 8,
    }

    /// <summary>
    /// Basic types, 3-level categories, descriptions, read-only values and live updates.
    /// The class-level category becomes the parent of every member category: Player / Status / Buffs.
    /// Implements INotifyPropertyChanged so values changed by the game refresh the panel immediately.
    /// </summary>
    [OptionCategory("Player")]
    public sealed class PlayerOptions : INotifyPropertyChanged
    {
        private int _hp = 80;
        private int _gold = 1000;

        public event PropertyChangedEventHandler PropertyChanged;

        // ---- Player / Status ----------------------------------------------------------

        [OptionCategory("Status"), OptionName("HP"), NumberRange(0, 100), OptionSort(0)]
        [OptionDescription("Range-limited int → slider + input field")]
        public int Hp
        {
            get => _hp;
            set
            {
                _hp = value;
                Notify(nameof(Hp));
            }
        }

        [OptionCategory("Status"), OptionName("移動速度"), Increment(0.25), OptionSort(1)]
        [OptionDescription("float with Increment → − / + buttons")]
        public float MoveSpeed { get; set; } = 1.5f;

        [OptionCategory("Status"), OptionName("無敵"), OptionSort(2)]
        public bool Invincible { get; set; }

        [OptionCategory("Status"), OptionName("名前"), OptionSort(3)]
        public string PlayerName { get; set; } = "あつや";

        [OptionCategory("Status"), OptionName("難易度"), OptionSort(4)]
        public Difficulty Difficulty { get; set; } = Difficulty.Normal;

        [OptionCategory("Status"), OptionName("座標"), OptionSort(5)]
        public Vector3 Position { get; set; } = new Vector3(12.5f, 0f, -3f);

        [OptionCategory("Status"), OptionName("キャラ色"), OptionSort(6)]
        [OptionDescription("Color → hex input with swatch")]
        public Color Tint { get; set; } = new Color(0.3f, 0.8f, 1f);

        [OptionCategory("Status"), OptionName("ステータス"), OptionSort(7)]
        [OptionDescription("Getter only → read-only value")]
        public string Summary => $"Lv.{Level} / HP {Hp} / {Difficulty}";

        [OptionCategory("Status"), OptionName("レベル"), NumberRange(1, 99), OptionSort(8)]
        public int Level { get; set; } = 12;

        // ---- Player / Status / Buffs (3rd level) --------------------------------------------

        [OptionCategory("Status", "Buffs"), OptionName("有効なバフ")]
        [OptionDescription("[Flags] enum → one toggle per flag")]
        public Buffs ActiveBuffs { get; set; } = Buffs.Haste | Buffs.Regen;

        [OptionCategory("Status", "Buffs"), OptionName("バフ残り時間"), NumberRange(0, 60)]
        public float BuffSeconds { get; set; } = 30f;

        [OptionCategory("Status", "Buffs"), OptionName("全バフ解除")]
        public void ClearBuffs() => ActiveBuffs = Buffs.None;

        // ---- Player / Wallet (updated by the game every second) -------------------------------

        [OptionCategory("Wallet"), OptionName("所持金")]
        [OptionDescription("Increases every second (ShowcaseTicker) → live update via INotifyPropertyChanged")]
        public int Gold
        {
            get => _gold;
            set
            {
                _gold = value;
                Notify(nameof(Gold));
            }
        }

        [OptionCategory("Wallet"), OptionName("プレイ時間")]
        public string PlayTime => $"{Time.realtimeSinceStartup:F0} s";

        [OptionCategory("Wallet"), OptionName("+10000 G")]
        public void AddGold() => Gold += 10000;

        // ---- Player (methods directly under the class category) -------------------------------

        [OptionName("全回復"), OptionSort(-10)]
        public void FullHeal()
        {
            Hp = 100;
            Debug.Log("[Showcase] Full heal");
        }

        [OptionName("ダメージ 10"), OptionSort(-9)]
        public void Damage() => Hp = Mathf.Max(0, Hp - 10);

        // Hidden from the panel.
        [HiddenOption]
        public int InternalCounter { get; set; }

        private void Notify(string name) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}
