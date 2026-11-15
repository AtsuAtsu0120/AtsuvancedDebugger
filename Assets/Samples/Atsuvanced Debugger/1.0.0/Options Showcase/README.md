# Options Showcase

Atsuvanced Debugger の Options / Console の機能を一通り試せるサンプルです。カスタム描画はすべて「型ごとの描画」（`DebuggerBuilder.AddOptionView<T>`）で行っています。Import して Play するだけで登録されます（F1 またはトリプルタップで開く）。

| カテゴリ | 内容 |
|---|---|
| Player / Status / Buffs | 全組み込み型（int スライダー、増減ボタン、bool、string、enum、Flags、Vector3、Color、読み取り専用）、3 階層、説明文 |
| Player / Wallet | ゲーム側が値を毎秒変更 → `INotifyPropertyChanged` で即時反映 |
| World / Map | グループ: 入力して「実行」ボタンで確定（Commit = OnAction）。ワープ先は独自型 `StageName` の型ビュー（ドロップダウン） |
| World / Time | 即時反映グループ（倍率 + プリセットボタン）、読み取り専用の独自型 `FrameStats` の型ビュー（フレームモニタ） |
| World / Camera | 縦レイアウトのグループ（メインカメラ） |
| Inventory | 独自型 `ItemId` を型ビューで表示、「引数付きメソッド」をグループで表現、ページを開くボタン |
| Enemies | 動的カテゴリ: スポーンごとに `Enemies/Spawned/Goblin#3` を追加、倒すと削除 |
| Diagnostics | 独自型 `Percent` の型ビュー（ゲージ + 増減ボタン）と Reset ボタンのグループ、セクションの並び順指定（`[OptionCategorySort]`）。Console 確認用のログ生成、1000 件のオプション追加（仮想化 / 検索の確認） |
| Profiler タブ | 「Showcase」グループにカスタムカウンタ（Simulated Load）を追加 |
