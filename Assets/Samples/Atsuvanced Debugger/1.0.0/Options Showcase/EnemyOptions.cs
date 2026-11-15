using Atsuvanced.Debugger;
using UnityEngine;

namespace AtsuvancedDebuggerSamples.Showcase
{
    /// <summary>
    /// Dynamic categories: every spawned enemy is registered under its own category
    /// ("Enemies/Goblin#3") and removed again when it dies.
    /// </summary>
    public sealed class Enemy
    {
        private readonly string _category;

        public Enemy(string category) => _category = category;

        [OptionName("HP"), NumberRange(0, 500)]
        public int Hp { get; set; } = 120;

        [OptionName("攻撃的")]
        public bool Aggressive { get; set; } = true;

        [OptionCategory("AI"), OptionName("索敵範囲"), NumberRange(1, 30)]
        public float SightRange { get; set; } = 8f;

        [OptionCategory("AI"), OptionName("状態")]
        public string State => Aggressive ? "Chase" : "Idle";

        [OptionName("倒す")]
        public void Kill()
        {
            AtsuDebug.Instance.RemoveOptionContainer(this);
            Debug.Log($"[Showcase] {_category} defeated");
        }
    }

    [OptionCategory("Enemies")]
    public sealed class EnemySpawner
    {
        private int _next = 1;

        [OptionName("ゴブリンを出す"), OptionSort(0)]
        public void SpawnGoblin() => Spawn("Goblin");

        [OptionName("スライムを出す"), OptionSort(1)]
        public void SpawnSlime() => Spawn("Slime");

        [OptionName("10 体まとめて出す"), OptionSort(2)]
        public void SpawnTen()
        {
            for (var i = 0; i < 10; i++) Spawn(i % 2 == 0 ? "Goblin" : "Slime");
        }

        [OptionName("全滅させる"), OptionSort(3)]
        public void KillAll()
        {
            var removed = AtsuDebug.Instance.RemoveOptionCategory("Enemies/Spawned");
            Debug.Log($"[Showcase] Removed {removed} enemy options");
        }

        private void Spawn(string kind)
        {
            var category = $"Enemies/Spawned/{kind}#{_next++}";
            AtsuDebug.Instance.AddOptionContainer(new Enemy(category), category);
        }
    }
}
