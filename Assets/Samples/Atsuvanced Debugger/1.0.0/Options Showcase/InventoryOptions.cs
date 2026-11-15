using System.Collections.Generic;
using System.Linq;
using Atsuvanced.Debugger;
using UnityEngine;
using UnityEngine.UIElements;

namespace AtsuvancedDebuggerSamples.Showcase
{
    /// <summary>A game-specific type. Drawn everywhere by ItemIdView (registered per type).</summary>
    public struct ItemId
    {
        public int Value;

        public ItemId(int value) => Value = value;

        public static readonly string[] Names = { "ポーション", "ハイポーション", "エリクサー", "鉄の剣", "ミスリルの剣", "竜の鱗", "転移の巻物" };

        public override string ToString() => Value >= 0 && Value < Names.Length ? Names[Value] : $"#{Value}";
    }

    /// <summary>Type view: one view for every option of type ItemId.</summary>
    public sealed class ItemIdView : OptionView
    {
        private DropdownField _dropdown;

        public override VisualElement CreateElement(OptionContext context)
        {
            _dropdown = new DropdownField(ItemId.Names.ToList(), 0);
            _dropdown.RegisterValueChangedCallback(e => context.SetValue(new ItemId(_dropdown.index)));
            return _dropdown;
        }

        public override void Refresh(OptionContext context) => _dropdown.SetValueWithoutNotify(context.GetValue<ItemId>().ToString());
    }

    /// <summary>
    /// "Method with arguments" expressed as a group: arguments are properties, the button runs it.
    /// </summary>
    [OptionCategory("Inventory")]
    public sealed class InventoryOptions
    {
        public readonly Dictionary<ItemId, int> Items = new Dictionary<ItemId, int>();

        [OptionGroup("give", Label = "アイテム付与"), OptionName("アイテム")]
        public ItemId GiveItem { get; set; }

        [OptionGroup("give"), OptionName("個数"), NumberRange(1, 99), OptionSort(1)]
        public int GiveCount { get; set; } = 1;

        [OptionGroup("give"), OptionName("付与"), OptionSort(2)]
        public void Give()
        {
            Items.TryGetValue(GiveItem, out var n);
            Items[GiveItem] = n + GiveCount;
            Debug.Log($"[Showcase] Gave {GiveItem} x{GiveCount}");
        }

        [OptionName("お気に入り装備")]
        public ItemId Favorite { get; set; } = new ItemId(4);

        [OptionName("全アイテム 99 個")]
        public void GiveAll()
        {
            for (var i = 0; i < ItemId.Names.Length; i++) Items[new ItemId(i)] = 99;
        }

        [OptionName("所持品を空にする")]
        public void ClearItems() => Items.Clear();
    }

    /// <summary>A page opened from an option button (OptionDefinition.FromPage).</summary>
    public sealed class InventoryPage : DebugPage
    {
        private readonly InventoryOptions _inventory;
        private readonly List<KeyValuePair<ItemId, int>> _rows = new List<KeyValuePair<ItemId, int>>();
        private ListView _list;
        private Label _empty;

        public InventoryPage(InventoryOptions inventory) : base("所持品一覧") => _inventory = inventory;

        protected override float UpdateInterval => 0.5f;

        protected override VisualElement CreateContent()
        {
            var root = new VisualElement();
            _empty = new Label("所持品はありません。「アイテム付与」で追加してください。");
            _empty.style.marginLeft = 16;
            _empty.style.marginTop = 12;
            root.Add(_empty);

            _list = new ListView
            {
                itemsSource = _rows,
                fixedItemHeight = 36,
                makeItem = () =>
                {
                    var row = new VisualElement();
                    row.style.flexDirection = FlexDirection.Row;
                    row.style.paddingLeft = 16;
                    row.style.paddingRight = 16;
                    row.style.alignItems = Align.Center;
                    var name = new Label { name = "name" };
                    name.style.flexGrow = 1;
                    row.Add(name);
                    row.Add(new Label { name = "count" });
                    return row;
                },
                bindItem = (e, i) =>
                {
                    e.Q<Label>("name").text = _rows[i].Key.ToString();
                    e.Q<Label>("count").text = "×" + _rows[i].Value;
                },
            };
            _list.style.flexGrow = 1;
            root.Add(_list);
            return root;
        }

        protected override VisualElement CreateToolbar()
        {
            var clear = new Button(() =>
            {
                _inventory.ClearItems();
                OnUpdate();
            }) { text = "空にする" };
            clear.AddToClassList("atsu-button");
            return clear;
        }

        protected override void OnShow() => OnUpdate();

        protected override void OnUpdate()
        {
            _rows.Clear();
            _rows.AddRange(_inventory.Items.OrderBy(p => p.Key.Value));
            _list.RefreshItems();
            _empty.style.display = _rows.Count == 0 ? DisplayStyle.Flex : DisplayStyle.None;
        }
    }
}
