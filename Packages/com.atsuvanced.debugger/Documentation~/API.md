# Atsuvanced Debugger API リファレンス（v1.0）

名前空間は `Atsuvanced.Debugger`（組み込みページは `Atsuvanced.Debugger.UI`）。詳細な説明は各メンバーの XML ドキュメントコメントを参照してください。

## 入口と組み立て

| API | 説明 |
|-----|------|
| `AtsuDebug.Instance` | `IDebugService`。未初期化なら初期化する |
| `AtsuDebug.Configure(b => b...)` | 組み立ての変更（`DebuggerBuilder` のメソッドチェーン）。初期化前（`AfterAssembliesLoaded` など）に呼ぶ。複数回呼ぶと順に適用 |
| `AtsuDebug.Init(b => b...)` / `Shutdown()` / `IsInitialized` / `IsEnabled` | 初期化（既定で `BeforeSceneLoad` に自動初期化）。`IsEnabled` は `ATSU_DEBUGGER_DISABLED` で false |
| `AtsuDebug.ClearSavedState()` | 保存した状態（Pin・タブ・検索語・`[OptionPersist]` の値など）をすべて消す。次回起動から反映 |

### DebuggerBuilder（Render Pipeline Asset に相当）

`DebuggerBuilder.CreateDefault(asset)` が設定アセットの値とすべての組み込み Feature から始め、`AtsuDebug.Configure` で変更します。

| 区分 | メソッド（すべて `DebuggerBuilder` を返す） |
|------|----------------------------------------|
| 設定 | `ConfigureSettings(s => ...)`, `UseLanguage(lang)`, `UseEntryCode(code, everyTime)` |
| サービス | `UseStorage(IDebuggerStorage)`（既定 PlayerPrefs）, `UseBugReporter(IBugReporter)`（既定 `LocalBugReporter`） |
| Feature | `Add(feature)`（同じ型は同じ位置で置き換え）, `Remove<T>()`, `Configure<T>(f => ...)`（無ければ例外）, `ClearFeatures()`, `Has<T>()`, `Get<T>()` |
| 描画 | `AddOptionView<T>(() => view)`, `UseTheme(tss)`, `AddStyleSheet(uss)`, `UseFont(font)` |

```csharp
[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterAssembliesLoaded)]
static void ConfigureDebugger() => AtsuDebug.Configure(b => b
    .ConfigureSettings(s => s.Trigger.Position = TriggerPosition.BottomRight)
    .UseStorage(new MyCloudStorage())
    .Remove<ProfilerFeature>()
    .Configure<OptionsFeature>(f => f.Layout = OptionsLayout.SingleTab)
    .Add(new NetworkFeature()));
```

### DebuggerFeature（Renderer Feature に相当）

`OnInstall()`（タブ・セクション・オーバーレイ・イベントを登録）/ `OnUninstall()` / `OnUpdate(dt)`（Play mode で毎フレーム）。`Debugger`（`IDebugService`）から公開 API だけを使う。

組み込み（`Atsuvanced.Debugger.UI`、`BuiltInFeatures.Create()` の順）:

| Feature | 内容 | 主なプロパティ |
|---------|------|----------------|
| `SystemInfoFeature` | System タブ（カテゴリごとのセクション）と既定の情報 | `Order`, `IncludeDefaultInfo`, `PageFactory` |
| `OptionsFeature` | オプションのタブ、Search / Pinned タブ | `Layout`（`CategoryTabs` / `SingleTab`）, `Order`, `SectionPageFactory`, `SearchTab`, `PinnedTab`, `SearchPageFactory`, `PinnedPageFactory` |
| `ConsoleFeature` / `ProfilerFeature` / `BugReportFeature` | 各タブ（`SinglePageTabFeature`） | `Order`, `PageFactory` |
| `TriggerFeature` | 画面の隅のトリガー | `IsEnabled` |
| `PinnedOverlayFeature` | Pin したオプションの常駐表示 | |
| `DockedConsoleFeature` | 画面の上下のコンソール（`IDockedConsole`） | `IsVisible`, `IsExpanded`, `Alignment`, `Changed` |
| `DockedProfilerFeature` | FPS / ms / メモリの常駐表示 | `IsDocked`, `Changed` |
| `KeyboardShortcutsFeature` | F1 / Ctrl+F / PageUp・PageDown / ゲームパッド L・R | `FocusSearch()` |
| `PauseTimeFeature` | `General.PauseTimeWhenOpen` のとき開いている間 `Time.timeScale = 0` | |

## IDebugService

| 区分 | メンバー |
|------|----------|
| データ | `Settings`（構築時のコピー）, `State`（`DebuggerState`: `Get` / `Set` / `Save` / `Clear` / `Keys`。キーは組み込みと共有なので接頭辞を付ける）, `Options`（`OptionRegistry`）, `OptionViews`（`OptionViewRegistry`）, `Console`（`ConsoleLogBuffer`）, `SystemInfo`（`SystemInfoRegistry`）, `Profiler`（`DebugProfiler`） |
| パネル | `Panel`（`IDebugPanel`）, `Tabs`（`DebugTabRegistry`）, `Pages`（`IPageNavigator`）, `Overlays`（`DebugOverlayRegistry`） |
| バグ報告 | `BugReports`（`BugReportService`） |
| Feature | `Features`, `GetFeature<T>()`（無ければ null） |

拡張メソッド（`DebugServiceExtensions`）: `ShowDebugPanel(tabId, requireEntryCode)`, `HideDebugPanel()`, `DestroyDebugPanel()`, `IsDebugPanelVisible()`, `PushPage(page)`, `AddOptionContainer(container, prefix)`, `RemoveOptionContainer`, `AddOption`, `RemoveOption`, `RemoveOptionCategory`, `PinOption`, `UnpinOption`, `PinAllOptions`, `UnpinAllOptions`, `ClearPinnedOptions`, `AddSystemInfo(entry, category)`, `ShowBugReportSheet(onComplete, takeScreenshot, description)`。

### IDebugPanel

`IsVisible`, `VisibilityChanged`, `Show(tabId = null, requireEntryCode = true)`, `Hide()`, `Destroy()`, `IsScreenPointOver(Vector2)`, `CaptureScreenshot(Action<byte[]>)`、PIN: `IsEntryCodeEnabled`, `IsEntryCodeShown`, `SetEntryCode(string 4 桁)`, `DisableEntryCode()`。

### BugReportService

`Reporter`（null を入れると既定に戻る）, `MaxLogEntries`, `Pending`（`BugReportRequest`: `Description` / `Screenshot`）, `ShowSheet(onComplete, takeScreenshot, description)`, `Complete(bool sent)`。

### オーバーレイ（`DebugOverlay`）

ゲーム画面上の常駐表示。トリガー・Pin・Docked Console / Profiler もすべてオーバーレイ（id は `DebugOverlayIds`）。

- `DebugOverlay(id, anchor, order)`: `CreateContent()` / `OnShow` / `OnUpdate(dt)` / `OnHide` / `OnDispose`、`IsActive`（表示したいか。毎フレーム評価）、`Visibility`（`WhilePanelClosed` / `Always`）、`Anchor` / `Order`（実行中に変えると移動）
- `OverlayAnchor`: 四隅と上下中央（同じ場所は `Order` の小さい順に隅から積む）、`TopEdge` / `BottomEdge`（全幅。隅の表示を内側に押す）
- `Debugger.Overlays.Add(overlay)` / `Remove(id)` / `Find(id)` / `Find<T>()`。PIN パッドの表示中はすべて隠れる

## オプション

### 属性

| 属性 | 対象 | 説明 |
|------|------|------|
| `[OptionCategory("A/B")]` | クラス / メンバー | カテゴリ（`/` で階層）。クラスに付けるとメンバーの親になる |
| `[OptionCategorySort(int)]` / `[OptionCategorySort("A/B", int)]` | クラス（複数可） | カテゴリの並び順（小さいほど前、既定 0）。タブ・セクションの順にもなる |
| `[OptionName]` / `[OptionDescription]` / `[OptionSort]` | メンバー | 表示名・説明・カテゴリ内の順番 |
| `[NumberRange(min, max)]` / `[Increment(step)]` | プロパティ | 数値の範囲と増減幅 |
| `[HiddenOption]` / `[Browsable(false)]` | メンバー | 表示しない |
| `[OptionGroup(key, Label, Layout, Commit)]` | メンバー | 複数のメンバーを 1 つの UI 単位に |
| `[OptionPersist(Key)]` | プロパティ | 値を保存して次回の起動で戻す |

`System.ComponentModel` の `Category` / `DisplayName` / `Description` / `Browsable` も解釈します。

### OptionRegistry（`IDebugService.Options`）

- ツリー: `Root`（`CategoryNode`: `Children` / `Entries` / `Options` / `SortPriority` / `TotalOptionCount`）, `FindCategory(path)`, `GetCategory(option)`, `GetGroup(option)`
- 並び順: `SetCategorySortPriority(path, priority)`, `GetCategorySortPriority(path)`
- 検索: `Search(query, results)`（空白区切り AND、かな / カナ・全角 / 半角を区別しない）
- Pin: `Pin` / `Unpin` / `IsPinned` / `PinCategory` / `UnpinCategory` / `ClearPinned` / `PinnedOptions`
- イベント: `OptionAdded`, `OptionRemoved`, `CategoryAdded`, `CategoryRemoved`, `CategoriesReordered`, `GroupAdded`, `GroupRemoved`, `PinStateChanged`, `PinsCleared`

### OptionDefinition

`Create<T>(name, getter, setter, category, sort)`, `FromMethod`, `FromPage(name, () => page, category)`、`WithPersistence(key)`。

値の読み取り: `GetValue()`（object）、`TryGetValue<T>(out T)`、`TryGetEnumValue(out long)`。
- `TryGetValue<T>` は、getter が `T` 型ならボックス化せずに読む。対象は次のとおり。
  - `Create<T>` で作ったオプション
  - スキャンしたプロパティのうち、bool / int / long / float / double / string / Vector2 / 3 / 4 / Color のもの
  - それ以外の型は `GetValue()` に戻る。`T` が違えば false を返す
- `TryGetEnumValue` は、int を基にした enum を割り当てなしで整数値として読む。

### 描画のカスタマイズ

オプションの描画を変える方法は **型ごとの登録だけ** です。オプションごと・グループごと・任意の要素を差し込む方法はありません。見た目を変えたい値は専用の型（例: `Percent`、`StageName`）にして、その型のビューを登録します。

| 段階 | API |
|------|-----|
| テーマ | 設定アセットの Style Sheets（`--atsu-color-*`）または `AddStyleSheet`、`General.Theme`（Dark / Light） |
| 型ごと | `DebuggerBuilder.AddOptionView<T>(() => new MyView())`、または実行中に `Debugger.OptionViews.Register<T>` / `Unregister(type)`（デバッガごと）。登録した型のプロパティはオプションになる（読み取り専用も可）。組み込みのビューを包むときは `OptionViewRegistry.CreateDefault(definition)`。解決結果は `OptionViews.Create(definition)` |
| ページ / タブ / セクション | `DebugPage`, `DebugTabDefinition`, `DebugSection` |

`OptionView`: `CreateElement(OptionContext)`（行ごとに 1 回）, `Refresh(OptionContext)`（変更通知・表示中のポーリング・保留値の変化で呼ばれる）, `Dispose()`, `ShowLabel`。
解決の順番: メソッド → ボタン、型の登録 → そのビュー、それ以外 → 組み込み。

`OptionContext`: `Definition`, `Group`, `Container`, `Placement`（`Panel` / `Pinned`）, `GetValue()`, `GetValue<T>()`, `TryGetValue<T>(out T)`, `TryGetEnumValue(out long)`, `SetValue(object)`, `Invoke()`, `IsPending`。
- `Refresh` は表示中に 0.3 秒ごとに呼ばれる。毎回割り当てないようにするには、次の 2 点を守る。
  - 値は `TryGetValue<T>` で読む。`GetValue()` は値型をボックス化し、`GetValue<T>()` はさらに型変換もする
  - コントロールが今表示している値と比べ、変わったときだけ書き換える（組み込みのビューはこの形）
- `TryGetValue<T>` / `TryGetEnumValue` はグループの保留値も返す。

## タブ・ページ

- `DebugTabDefinition(id, title, order)` + `Sections.Add(new DebugSection(id, title, () => page, order, badge))`、または `DebugTabDefinition(id, title, () => page, order)`
- 組み込みタブの id は `DebugTabIds`（`system` / `search` / `pinned` / `console` / `profiler` / `bug-reporter`、オプションのタブは `option:<名前>`、`SingleTab` のときは `options`）。Order: System 0、Search 50、Pinned 60、オプション 100、Console 200、Profiler 300、Bug Report 400、ユーザー既定 1000（各 Feature の `Order` で変更可）
- `DebugTabRegistry` / `DebugSectionCollection`: `Add`（同じ id は置き換え）, `Remove`, `Find`, `Reorder(ids)`
- `IPageNavigator`: `CurrentTabId`, `CurrentPage`, `ActivePage`（タブのルートなら選択中のセクションのページ）, `Tabs`, `SelectTab`, `SelectAdjacentTab(±1)`, `Push`, `Pop`, `PopToRoot`, `Close`, `Changed`
- `DebugPage`: `CreateContent` / `CreateToolbar` / `OnShow` / `OnUpdate`（`UpdateInterval`）/ `OnHide` / `OnDispose` / `OnBackRequested` / `Chrome`。`DebugPage.FromElement` / `FromUxml`
- 組み込みページ（継承可）: `SystemInfoPage`, `ConsolePage`, `OptionSectionPage`, `OptionSearchPage`, `PinnedOptionsPage`, `ProfilerPage`, `BugReportPage`。いずれも公開 API だけで作られている（`PublicApiParityTests` で検査）
- `OptionListView`（`Atsuvanced.Debugger.UI`）: 仮想化したオプション一覧。`new OptionListView(Debugger, categoryPath, directOptionsOnly, emptyText)`、`Attach` / `Detach` / `Tick(dt)` / `SetFilter(filter)` / `Dispose`、`OpenCategoryPath` / `OpenCategoryChanged`
- `DebugPage.CreateErrorElement(message, exception)`: 失敗した部分の代わりに出すエラー表示。`UssClasses.Hidden`: 非表示のクラス（`atsu-hidden`）

## Console

- `ConsoleLogBuffer`: `Count`, `Capacity`, `Version`, `GetCount(severity)`, `CopyTo(list)`, `CopyLast(n, list)`, `Clear()`, `Resize(n)`
- `ConsoleExport.ToText(entries)` / `SaveToFile(buffer, directory)`（Console タブの「保存」と同じ）

## Profiler（`IDebugService.Profiler`）

- `FrameTimes`（`FrameHistory`: 1 フレームごとの ms）, `Fps`, `CpuMainThreadMs`, `CpuRenderThreadMs`, `GpuMs`（取れないときは -1）, `IsRecording`, `ResetHistory()`
- `BeginRecording()`: 記録を開始し、返り値（`IDisposable`）を `Dispose` すると終了。参照カウントなので、最後のハンドルが破棄されるまで記録が続く
- `AddCounter(name, Func<double>, unit, group)`, `AddCounter(name, ProfilerCategory, statName, unit, group)`, `RemoveCounter`, `Find(name)`, `Counters`, `Changed`
- `ProfilerCounter`: `Name`, `Group`, `Unit`（`Count` / `Bytes` / `Milliseconds` / `Nanoseconds` / `Percent` / `Raw`）, `Value`, `IsAvailable`, `FormatValue()`

## Bug Report

- `BugReport`: `Description`, `UserEmail`, `Screenshot`（PNG）, `ConsoleLog`, `SystemInformation`, `CreatedAt`
- `IBugReporter.Submit(report, onProgress, onComplete(success, message))`
- `LocalBugReporter(directory)`: 既定の送信先（`report.txt` / `report.json` / `screenshot.png` を保存）。`LastReportPath`
- `BugReportBuilder.Create(service, description, email, screenshot, maxLogEntries, includeSystemInfo)` / `ToText` / `ToJson`

## 文字列・言語

- `DebuggerStrings.Language`（`Auto` / `English` / `Japanese`）, `Get(key)`, `Format(key, args)`, `Override(language, key, value)`
- 初期化時に `General.Language`（`DebuggerBuilder.UseLanguage`）が適用されます。UI は 1 つなので文字列表はプロセス全体で共有です。タブ・セクションの名前は初期化時に決まるため、`Override` は初期化前に呼んでください

## 設定（`DebuggerSettings`）

使う Feature ごとにグループ分けしています。値は初期化時（Feature のインストール時）に読むため、実行中に変えても反映されません。実行中に変える状態は各 API で変更します（`TriggerFeature.IsEnabled`、`DockedConsoleFeature.IsVisible`、`Panel.SetEntryCode` など）。

| グループ | 項目 |
|------|------|
| `General` | `DefaultTab`（タブ id）, `SortingOrder`, `PauseTimeWhenOpen`, `Language`, `Theme`, `UiScale`, `PersistState` |
| `Trigger` | `Enabled`, `Position`, `Behaviour`, `Size`, `ErrorNotification` |
| `EntryCode` | `Required`, `Code`, `EveryTime` |
| `Console` | `MaximumEntries` |
| `DockedConsole` | `VisibleAtStart`, `Alignment`, `Expanded`, `Lines`, `Opacity` |
| `Pinned` | `Corner`, `Width`, `MaxHeight`, `Opacity` |
| `Profiler` | `HistoryFrames`, `DockedAtStart`, `DockedCorner`, `DockedOpacity` |
| `BugReport` | `MaxLogEntries`, `Folder` |
| `Keyboard` | `Enabled`, `Toggle`（F1）, `Search`（Ctrl+F）, `PreviousTab`（PageUp）, `NextTab`（PageDown） |

設定アセット（`DebuggerSettingsAsset`）: 上記 + `Theme`（TSS）, `StyleSheets`, `Font`。

## Scripting Define Symbols

| Symbol | 効果 |
|--------|------|
| `ATSU_DEBUGGER_DISABLED` | 全 API を no-op 化。設定アセットとテーマをビルドから外す |
| `ATSU_DEBUGGER_DISABLE_AUTO_INIT` | 起動時の自動初期化を無効化 |
