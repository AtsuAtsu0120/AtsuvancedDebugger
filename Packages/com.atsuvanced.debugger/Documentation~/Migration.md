# SRDebugger からの移行ガイド

Atsuvanced Debugger は SRDebugger の主要 API（`SRDebug` / `SROptions` / `SRDebugger.*`）を互換アセンブリで提供します。
移行元のバージョンは厳密には合わせていません。「属性を付けたメンバーがそのままデバッグ UI になる」という思想と、よく使われる API の互換を優先しています。

## 手順

1. SRDebugger（`Assets/StompyRobot` など）を削除する
2. 本パッケージを追加する
3. Package Manager > Atsuvanced Debugger > Samples から **SROptions Shim** を Import する
   - `SROptions` は `partial class` なので、ユーザーと同じアセンブリ（通常 `Assembly-CSharp`）に置く必要があります。Shim は起動時に `SROptions.Current` を自動で登録します
4. Play して、既存の `SRDebug.Instance.*` 呼び出しと `partial class SROptions` が動くことを確認する
5. 設定を移す（下の「設定の対応」）。設定は `Edit > Project Settings > Atsuvanced Debugger`

SRDebugger を残したまま試す場合は、Scripting Define Symbols に `ATSU_DEBUGGER_DISABLE_SR_COMPAT` を追加して互換アセンブリを無効化してください（型の衝突を避けるため）。

## API の対応

| SRDebugger | Atsuvanced Debugger | 備考 |
|------------|---------------------|------|
| `SRDebug.Init()` / `SRDebug.Instance` / `SRDebug.IsInitialized` | `AtsuDebug.*` | 自動初期化も同じく既定で有効 |
| `ShowDebugPanel(bool requireEntryCode)` / `ShowDebugPanel(DefaultTabs, bool)` | 同名 | `DefaultTabs.Options` は Options タブ（`OptionsLayout.SingleTab`）か、最初のオプションカテゴリのタブを開く |
| `HideDebugPanel()` / `DestroyDebugPanel()` | 同名 | |
| `IsDebugPanelVisible` / `IsTriggerEnabled` / `PanelVisibilityChanged` | 同名 | `IsTriggerEnabled` は `TriggerFeature` を外すと常に false |
| `AddOptionContainer` / `RemoveOptionContainer` / `AddOption` / `RemoveOption` | 同名 | `DynamicOptionContainer` / `IOptionContainer` / `OptionDefinition.Create` / `FromMethod` も対応 |
| `PinOption` / `UnpinOption` / `PinAllOptions` / `UnpinAllOptions` / `ClearPinnedOptions` | 同名 | 名前指定は同名のすべてに当たる。`"Category/Name"` の Id 指定を推奨 |
| `AddSystemInfo(InfoEntry, category)` / `InfoEntry.Create` | 同名 | `isPrivate` の項目はバグ報告から除外 |
| `DockConsole`（`IsVisible` / `IsExpanded` / `Alignment`） | 同名 | 本体側は `DockedConsoleFeature` |
| `IsProfilerDocked` | 同名 | FPS / フレーム時間 / メモリを画面の隅に表示。本体側は `DockedProfilerFeature.IsDocked` |
| `SetEntryCode(EntryCode)` / `DisableEntryCode()` | 同名 | `new EntryCode(1, 2, 3, 4)` |
| `ShowBugReportSheet(onComplete, takeScreenshot, description)` | 同名 | 閉じて送らなかった場合は `onComplete(false)` |
| `SetBugReporterHandler(IBugReporterHandler)` | 同名 | `BugReport` / `BugReportSubmitResult` / `ConsoleEntry` も互換型あり。null で既定（ローカル保存）に戻る |
| `PinnedUiCanvasCreated` | `[Obsolete]` | UI Toolkit のため uGUI の Canvas はない。発火しない |
| `EnableWorldSpaceMode()` | `[Obsolete]` | 非対応。警告を出して null を返す |
| `SRServiceManager`, `SRF.*`, uGUI prefab | 非対応 | |

### SROptions の属性

Shim は SRDebugger と同じ書き方を受け付けます: `[Category]`, `[DisplayName]`, `[Sort]`, `[NumberRange]`, `[Increment]`, `[Browsable(false)]`。
加えて `[SROptions.Group("key")]` でオプショングループ、本パッケージの属性（`[OptionCategory("A/B/C")]` など）もそのまま使えます。

## 設定の対応

| SRDebugger の設定 | Atsuvanced Debugger |
|-------------------|---------------------|
| Auto Load | 既定で有効（`ATSU_DEBUGGER_DISABLE_AUTO_INIT` で無効化） |
| Trigger Enabled / Position / Behaviour | `Trigger.Enabled` / `Trigger.Position` / `Trigger.Behaviour` |
| Error Notification | `Trigger.ErrorNotification` |
| Keyboard Shortcuts | `Keyboard.Toggle`（F1）ほか。`Keyboard.Enabled` |
| Default Tab | `General.DefaultTab`（タブ id。`system` / `console` など） |
| Require Entry Code / Entry Code / Require Every Time | `EntryCode.Required` / `EntryCode.Code` / `EntryCode.EveryTime` |
| Maximum Console Entries | `Console.MaximumEntries` |
| UI Scale | `General.UiScale`（画面密度に応じたスケールに掛かる倍率） |
| Layer / Sorting Order / Use Debug Camera | `General.SortingOrder` のみ（UI Toolkit の PanelSettings を使うため） |
| Enable Bug Reporter / API Key | `DebuggerBuilder.UseBugReporter(IBugReporter)`（実行中は `BugReports.Reporter`）。既定はローカル保存。HTTP 送信はサンプル「Bug Report (HTTP)」 |
| （無効にするタブ・機能） | `AtsuDebug.Configure(b => b.Remove<ProfilerFeature>())` など。Feature 単位で外せる |

設定値を自動で移すツールは v1.1 で予定しています（FR-EDT-03）。

## 動作の違い

- **UI**: UI Toolkit で描画し、独自の UIDocument / PanelSettings を持ちます。EventSystem は作りません
- **タブ構成**: オプションの 1 階層目のカテゴリがパネルのタブになり、2 階層目が左の一覧（セクション）、3 階層目以降は折り畳み。横断検索は Search タブ。SRDebugger のように 1 つの Options タブにまとめるには `AtsuDebug.Configure(b => b.Configure<OptionsFeature>(f => f.Layout = OptionsLayout.SingleTab))`
- **状態の保存**: Pin、タブ、開いたカテゴリ、検索語、Docked Console / Profiler の表示状態を保存します（`PersistState` でオフ）
- **リリースビルド**: `ATSU_DEBUGGER_DISABLED` で全 API が no-op になり、設定アセットとテーマはビルドに含まれません。Resources フォルダは使いません
- **言語**: 組み込み UI は英語 / 日本語（設定の `Language`）
