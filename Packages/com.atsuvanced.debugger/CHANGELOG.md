# Changelog

## [1.0.0] - 2026-11-14

v0.4（Pin・常駐表示・永続化）、v0.5（Profiler）、v0.6（Bug Reporter・エントリーコード）、v1.0（仕上げ）をまとめたリリース。

### Changed — タブバーとヘッダーの順番を入れ替え（破壊的変更を含む）
- パネルの並びを「タブバー → ヘッダー → 内容」にした（以前は「ヘッダー → タブバー → 内容」）。ヘッダーの戻る / タイトル / ツールバーは今のページのものなので、内容の直上に置く
- 閉じる（×）をタブバーの右端に移した。`PageChrome.Header` だけのページ（タブバーを隠す）では、ヘッダーの右端に出す
- 左の一覧を持つタブのヘッダーのタイトルを `"タブ / セクション"` からセクション名だけにした（`SectionedPage.Title`）
- USS: `.atsu-tabbar` は行のコンテナになり、タブの ScrollView は `.atsu-tabbar__tabs` になった。`.atsu-header__close` は `.atsu-panel__close` に改名。`.atsu-header` の背景色をなくした（パネルの背景のまま）

### Performance — 毎フレームの GC Alloc を 0 に
- **非表示時**:
  - `DebugOverlayRegistry.Update` で毎フレーム `ToArray` していた（64 B / フレーム）のをやめた
  - 閉じている間の割り当てが 0 になった
- **表示中**:
  - `DebugPage` のライフサイクル呼び出しで毎回 delegate を作っていた（128 B / ページ / フレーム）のをやめた
- **Docked Console**:
  - ログごとに全行を書式化していた（ログ 1 件あたり約 1.2 KB）のを、変わった行だけ更新するようにした
  - 折り畳み中は行を描画しない
  - 行は「時刻」と「メッセージ」の 2 つの Label に分けた（USS: `.atsu-dock__line` は行のコンテナになり、`.atsu-dock__line-time` / `.atsu-dock__line-text` を追加）
- **Console タブ**:
  - 行の文字列をエントリごとにキャッシュした
  - ラベルの参照を `userData` に持たせた（`Q` をやめた）
  - 件数ボタンの書式化はキャッシュを使う
  - Collapse の辞書を再利用する
  - `ConsoleLogBuffer.CopyTo` が、バッファが満杯になるまでログのたびにリストを確保し直していた（約 72 KB / 件）のを直した
- **オプション**:
  - 値の読み取りを型付きにした（`OptionDefinition.TryGetValue<T>` / `TryGetEnumValue`、`OptionContext.TryGetValue<T>` / `TryGetEnumValue`）
  - 組み込みビューは、表示中の値と比べて変わったときだけ書き換える
  - 非表示の行は、変更通知では更新しない（表示時に更新する）
- **Profiler タブ / Docked Profiler**:
  - 数値の文字列を、表示する精度で丸めた値ごとにキャッシュした
  - フレーム時間の行は、数値ごとの Label に分けた
- **System タブ**:
  - 固定値の行（`InfoEntry.IsConstant`）は 1 回だけ書式化する
  - 組み込みの動的な値（解像度・品質レベルなど）は、変わったときだけ文字列を作る
- **左の一覧**:
  - タブを開くたびに作り直していたのを、構成が変わったときだけにした（再表示ではバッジと選択だけ更新する）
- **その他**:
  - Bug Report のログ件数の表示を、件数が変わったときだけ書式化する
  - カルチャに依存する `StartsWith` を Ordinal にした
- **テスト**: `AllocationTests` を追加（閉じている / Console タブを開いた状態の Tick、型付きの読み取り、各キャッシュの割り当てが 0 であること）

### Removed — 描画のカスタマイズを型ごとに限定（破壊的変更）
- オプションの描画を変える方法を、型ごとの登録（`DebuggerBuilder.AddOptionView<T>` / `IDebugService.OptionViews.Register<T>`）だけにした。見た目を変えたい値は専用の型にして、その型のビューを登録する
- **削除** `[OptionView]` / `[OptionUxml]` / `[OptionElement]`、`OptionDefinition.WithView` / `WithUxml` / `FromElement` / `ViewType` / `UxmlKey` / `ElementFactory`、`OptionKind.Element`
- **削除** グループ単位の描画（FR-GRP-09）: `OptionGroupAttribute.View` / `Uxml`、`OptionGroup.ViewType` / `UxmlKey`、`OptionGroupView` / `OptionGroupContext`、`OptionViewRegistry.CreateGroupView`
- **削除** UXML テンプレートの登録: `DebuggerBuilder.AddOptionUxml`、`OptionViewRegistry.RegisterUxml` / `TryGetUxml`、設定アセットの `UxmlTemplates`（`UxmlTemplateEntry`）
- ページ（`DebugPage.FromElement` / `FromUxml` を含む）と常駐表示（`DebugOverlay`）は変更なし
- サンプル「Options Showcase」を型ごとの描画だけで書き直した（`StageName` / `FrameStats` / `Percent` とそのビュー。`LoadGaugeView` は `PercentView` に置き換え）

### Added (v1.0)
- **Project Settings 画面**（`Project/Atsuvanced Debugger`、FR-EDT-01）: 設定アセットの編集、`ATSU_DEBUGGER_DISABLED` の切り替え、バグ報告フォルダを開く、保存した状態の消去。メニュー `Tools > Atsuvanced Debugger > Project Settings`
- **カテゴリの並び順**（FR-CAT-08）: `[OptionCategorySort]`（クラスのカテゴリ / パス指定、複数可）、`OptionRegistry.SetCategorySortPriority` / `GetCategorySortPriority`、`CategoryNode.SortPriority`、`OptionRegistry.CategoriesReordered`。オプションのタブ・セクションの順番にも反映
- **グループ単位のカスタム描画**（FR-GRP-09）: `[OptionGroup(key, View = typeof(MyGroupView))]` / `Uxml = "key"`、`OptionGroupView` / `OptionGroupContext`
- **設定アセットから UXML テンプレートを登録**（FR-VIEW-04）: `DebuggerSettingsAsset.UxmlTemplates`（コードでの登録が優先）
- **UI 文字列のテーブル化**（NFR-11）: 英語 / 日本語を同梱。`General.Language`（Auto / English / Japanese）、`DebuggerStrings.Get` / `Format` / `Override`
- **Light テーマ**（FR-PNL-03）: `General.Theme`（Dark / Light）
- **キーボード / ゲームパッド操作**（FR-PNL-05）: パネルを開くとタブにフォーカス、フォーカス表示、`Keyboard.PreviousTab` / `Keyboard.NextTab`（既定 PageUp / PageDown、ゲームパッドは L / R）でタブ切り替え
- `AtsuDebug.ClearSavedState()`（保存した Pin・タブ・検索語・`[OptionPersist]` の値などを消去）。保存したキーの一覧を `AtsuvancedDebugger/__keys` に記録（このバージョンより前に保存されたキーは対象外）
- SR 互換: `IsProfilerDocked`、`SetEntryCode(EntryCode)` / `DisableEntryCode()`、`SetBugReporterHandler(IBugReporterHandler)`（`BugReport` / `BugReportSubmitResult` / `ConsoleEntry`）、`ShowDebugPanel` の `requireEntryCode`。`PinnedUiCanvasCreated` / `EnableWorldSpaceMode` は `[Obsolete]`（非対応）
- ドキュメント: `Documentation~/API.md`（API リファレンス）、`Documentation~/Migration.md`（SRDebugger からの移行ガイド）

### Added (v0.6)
- **Bug Report タブ**（FR-BUG-01）: 説明・メールアドレス・スクリーンショット（デバッグ UI を除いて撮影、撮り直し可）・コンソールのログ・システム情報（`IsPrivate` は除外）を送信。`BugReportPage`（継承可、`CreateReport` で拡張）
- 既定の送信先 `LocalBugReporter`（`persistentDataPath/AtsuvancedDebugger/BugReports/<日時>/` に report.txt / report.json / screenshot.png）。`BugReportService.Reporter`
- `BugReportBuilder.Create` / `ToText` / `ToJson`、`IDebugPanel.CaptureScreenshot`
- `ShowBugReportSheet` の実装（先にスクリーンショットを撮ってからタブを開く。送らずに閉じたら `onComplete(false)`）
- サンプル「Bug Report (HTTP)」（`HttpBugReporter`、multipart POST）
- **エントリーコード（PIN）**（FR-TRG-05）: 設定 `EntryCode.Required` / `Code` / `EveryTime`、`IDebugPanel.SetEntryCode` / `DisableEntryCode` / `IsEntryCodeEnabled`、`Panel.Show(tabId, requireEntryCode)`。タップ / キー入力できる PIN パッド
- Console の「保存」（FR-CON-06）: 全ログをテキストファイルに書き出し、パスを表示。`ConsoleExport.ToText` / `SaveToFile`

### Added (v0.5)
- **Profiler タブ**（FR-PRF-01〜03）: フレーム時間のグラフ、FPS、CPU メイン / レンダースレッド・GPU 時間（`FrameTimingManager`）、メモリ（Total Used / Reserved、GC Used / Reserved、GC Alloc / Frame、System Used）、描画（Draw Calls / Batches / SetPass / Triangles / Vertices）。取れない値は「取得不可」
- 任意のカウンタ（FR-PRF-04）: `DebugProfiler.AddCounter(name, Func<double>)` / `AddCounter(name, ProfilerCategory, statName)`、`ProfilerUnit`、`ProfilerPage`（継承可）、`FrameGraph`
- **Docked Profiler**（FR-PRF-05）: `DockedProfilerFeature.IsDocked`（永続化）、設定 `Profiler.DockedAtStart` / `DockedCorner` / `DockedOpacity` / `HistoryFrames`。タップで Profiler タブ
- 記録は Profiler タブか常駐表示が出ている間だけ（非表示時の負荷なし）

### Changed — Feature による構成（設計レビュー 課題 2・3・5〜8、破壊的変更を含む）
URP が SRP の上に作られているように、組み込みのタブ・常駐表示・ショートカットをすべて公開 API の上の **Feature** として作り直した。
- **追加** `DebuggerFeature`（`OnInstall` / `OnUninstall` / `OnUpdate`）と組み込み Feature: `SystemInfoFeature`, `OptionsFeature`（`Layout = CategoryTabs / SingleTab`、`SectionPageFactory`、Search / Pinned タブ）, `ConsoleFeature` / `ProfilerFeature` / `BugReportFeature`（`Order`, `PageFactory`）, `TriggerFeature`, `PinnedOverlayFeature`, `DockedConsoleFeature`, `DockedProfilerFeature`, `KeyboardShortcutsFeature`, `PauseTimeFeature`
- **追加** 組み立ての起点 `DebuggerBuilder`（メソッドチェーン: `ConfigureSettings` / `UseStorage` / `UseBugReporter` / `UseLanguage` / `UseEntryCode` / `Add` / `Remove<T>` / `Configure<T>` / `ClearFeatures` / `AddOptionView<T>` / `AddOptionUxml` / `UseTheme` / `AddStyleSheet` / `UseFont`）と `AtsuDebug.Configure(b => ...)` / `AtsuDebug.Init(b => ...)`。**削除** `AtsuDebug.Storage`、`AtsuDebug.Init(DebuggerSettings)`
- **追加** オーバーレイ `DebugOverlay`（`OverlayAnchor`: 四隅・上下中央・上下の辺、`Order`、`OverlayVisibility`、`IsActive`）と `IDebugService.Overlays`。トリガー・Pin・Docked Console / Profiler もオーバーレイになり、同じ隅では積み重なる（重なり回避の個別処理を廃止）
- **変更** `PanelView` を Host（`PanelView`）/ 外枠（`PanelShell`）/ オーバーレイの配置（`OverlayLayer`）に分割。ショートカットは `KeyboardShortcutsFeature` へ
- **変更** `IDebugService` をサブシステムのアクセサだけにした: `Settings`, `State`, `Options`, `OptionViews`, `Console`, `SystemInfo`, `Profiler`, `Panel`（`IDebugPanel`: 表示 / PIN / スクリーンショット）, `Tabs`, `Pages`, `Overlays`, `BugReports`（`BugReportService`）, `Features`, `GetFeature<T>()`。`ShowDebugPanel` / `AddOptionContainer` / `PinOption` などは拡張メソッド（`DebugServiceExtensions`）。**削除** `IsTriggerEnabled` / `DockedConsole` / `IsProfilerDocked`（各 Feature へ）、`SetBugReporter` / `BugReporter` / `PendingBugReport` / `CompleteBugReport` / `CaptureScreenshot` / `SetEntryCode` 系（`BugReports` / `Panel` へ）
- **変更** `DebuggerSettings` を Feature ごとのグループにした（`General` / `Trigger` / `EntryCode` / `Console` / `DockedConsole` / `Pinned` / `Profiler` / `BugReport` / `Keyboard`）。`Clone()` は深いコピー。`DefaultTab` はタブ id（文字列）。**削除** `DebugTab` enum（SR 互換の `DefaultTabs` の対応は互換アセンブリへ）
- **変更** 型ごとのビューの登録をデバッガごとにした（`OptionViewRegistry`、`IDebugService.OptionViews`）。**削除** static の `OptionViews`。スキャン対象の型は `OptionRegistry.AddValueType` / `IsValueTypeSupported`
- **追加** `IPageNavigator.ActivePage` / `Tabs` / `SelectAdjacentTab`、`DebugTabRegistry.Reorder` / `DebugSectionCollection.Reorder` の公開、`DefaultSystemInfo` の公開
- **テスト** `PublicApiParityTests` が組み込み Feature とそこから使われる UI 層の型も検査する

### Changed — 組み込みとユーザー拡張のパリティ（設計レビュー 課題 1・4、破壊的変更を含む）
組み込みページが内部 API を使わず、ユーザーと同じ公開 API だけで組まれるようにした。テスト `PublicApiParityTests` が、組み込みページから内部のコア API を参照していないことを IL から検査する。
- **追加** `IDebugService.State`（`DebuggerState`、public に変更）: 自作ページも状態を永続化できる。キーは組み込みと同じ名前空間なので接頭辞を付ける
- **追加** `BugReportRequest`（`Description` / `Screenshot`）と、それを受け取って完了させる API（後に `BugReportService.Pending` / `Complete(bool sent)` へ移動）: 自作のバグ報告ページでも `ShowBugReportSheet` に応えられる
- **追加** `DebugProfiler.BeginRecording()`（`IDisposable` を返す。参照カウント）: 自作の Profiler ページでも記録を開始できる
- **公開** `OptionListView`（仮想化したオプション一覧。`CategoryPath` / `DirectOptionsOnly` / `Filter` / `OpenCategoryPath` / `OpenCategoryChanged` / `Attach` / `Detach` / `Tick` / `SetFilter` / `Dispose`）: 自作ページにオプション一覧を埋め込める。組み込みのオプションページの `View` も public
- **公開** ビューの解決（`Create` / `CreateDefault`（組み込みのビューを包んで拡張する用）/ `CreateGroupView`、現在は `OptionViewRegistry`）、`DebugPage.CreateErrorElement`
- **追加** `UssClasses.Hidden`（`atsu-hidden`）。**削除** `PanelView.HiddenClass`（internal）
- **変更** フレームの駆動を逆転: ランナー（`[AtsuvancedDebugger]`）をサービスが持ち、パネル（子の `Panel`）を毎フレーム更新する。パネルを一度も開いていなくても、Profiler の記録と破棄済みコンテナの掃除（FR-OPT-14）が動く
- **変更** `OptionScanner` が UI のビュー登録を参照しなくなった。型の絞り込みはスキャン時に行い、ビューの登録 / 解除後にキャッシュの破棄が不要になった（解除するとその型は再びスキャン対象外になる）
- **変更** オプションのタブの同期（`OptionTabsSync`）は組み込みタブと一緒に `UI/BuiltIn` で登録する（コアから UI のページへの依存をなくした）

### Fixed (v1.0)
- `DestroyDebugPanel` の後にパネルを開き直すと、ページが表示されずタブも切り替わらなかった（イベントの購読を解除したままだった。同じフレームで開き直すと古いホストの破棄が新しいパネルの参照を消す問題も修正）

### Changed (v1.0)
- 組み込みタブ・セクション・ボタンなどの文言は `DebuggerStrings` から取得（既定の `Language = Auto` では日本語環境で日本語表示）
- Profiler / Bug Report タブはプレースホルダから実装に置き換え

### Performance (NFR-01〜03、macOS Editor / Apple M4 Max での計測)
- パネル非表示時（Pin 最小化・Docked Console 表示を含む）: GC Alloc 0 B / フレーム、約 0.005 ms / フレーム（Docked Profiler 表示中は 0.25 秒ごとの文字列更新で平均約 6 B / フレーム）
- 初回表示（ホスト生成 + System タブ）約 3 ms、タブの初回切り替え 0.2〜28 ms（最大は Console の初回生成）、2 回目以降 0.3 ms 以下
- Console 1 万件: ログ追加後の再フィルタ + 再描画 約 0.9 ms、表示中の平均フレーム時間 0.9 ms
- モバイル実機での計測は未実施

### Added (v0.4)
- **Pinned Options**: Pin したオプションを、パネルを閉じている間ゲーム画面の隅に常駐表示（グループはひとまとまり）
  - 設定: `Pinned.Corner` / `Width` / `MaxHeight` / `Opacity`
  - ビューには `OptionPlacement.Pinned` が渡る（Pin 用の描画に切り替え可能、FR-VIEW-07）
- **状態の永続化**: Pin（Id で記録し、後から登録されたオプションにも適用）、選択中のタブ / セクション、開いているカテゴリ、Search の直近の検索語
  - `IDebuggerStorage`（既定 `PlayerPrefsDebuggerStorage`）、`DebuggerBuilder.UseStorage` で差し替え、`General.PersistState` で無効化
- `Keyboard.Search`（既定 Ctrl+F）: Search タブを開いて検索欄へフォーカス
- `UnityEngine.Object` のコンテナが破棄されたら自動で登録解除（`OptionRegistry.RemoveDestroyedContainers`）
- `OptionRegistry.Contains` / `PinsCleared`
- Pinned Options の最小化（見出しをタップで最小化 / 復元、状態は永続化）
- **Pinned タブ**（Search の次、Order 60）: Pin 中のオプションをツリーの形で一覧し、個別に外せる。「Unpin All」は 2 回押しで全解除（`PinnedOptionsPage`、`DebugTabIds.Pinned`）
- `OptionTreeFilter.CreatePinned` / `ForOptions` / `Recreate` / `HighlightHits`
- Pinned タブで解除した行は、パネルを閉じるまで一覧に残る（押し直せば再 Pin）
- **Docked Console**（FR-CON-08）: パネルを閉じている間、画面の上下に最新のログを常駐表示
  - `DockedConsoleFeature`（`IDockedConsole`: `IsVisible` / `IsExpanded` / `Alignment`、状態は永続化）
  - 設定: `DockedConsole.VisibleAtStart` / `Alignment` / `Expanded` / `Lines` / `Opacity`
  - Console タブに「Dock」ボタン。同じ辺の Pin 表示とトリガーは重ならないよう退避
  - SR 互換: `SRDebug.Instance.DockConsole`（`IDockConsoleService`、`SRDebugger.ConsoleAlignment`）
- `ConsoleLogBuffer.CopyLast`
- **オプション値の保存**（Issues B-06）: `[OptionPersist]` / `OptionDefinition.WithPersistence()`。変更のたびに保存し、登録時に書き戻す（bool / 数値 / string / enum / Vector / Color、InvariantCulture）
- 組み込みページ `ConsolePage` / `SystemInfoPage` / `OptionSectionPage` / `OptionSearchPage` を `public`・継承可能に
  - `ConsolePage.Filter` / `InvalidateFilter()`、`SystemInfoPage.Category`、`OptionSectionPage.CategoryPath` / `IsGeneral`、`OptionSearchPage.Query` / `FocusSearch()`

### Fixed
- Console の選択行の色: リニアカラースペースで半透明の塗りが濃く見えていたため、不透明のテーマ色（`--atsu-color-selection`）に
- 縦スクロールバーのつまみが既定テーマの幅のまま右にはみ出していた
- オプション一覧の行にマウスを乗せると、既定テーマの明るい灰色が背景に出ていた（行を透明にするルールが古いクラス名のままだった）

### Changed
- **タブをセクション構成に統一**: すべてのタブ（組み込み / ユーザー）は `DebugSection` の並び。2 つ以上で左に一覧を表示
- **オプションの 1 階層目のカテゴリをパネルのタブに**（System と Console の間に並ぶ）。「Options」タブは廃止（`DefaultTabs.Options` は最初のオプションタブを開く）
- 2 階層目 = セクション（直下のオプションは「General」）、3 階層目以降 = 折り畳み（同時に開けるのは 1 本の枝のみ）
- System タブはシステム情報のカテゴリごとのセクションに、Console タブは「Logs」セクションに
- 全オプション横断の検索を **Search タブ**に（System の次）。オプションタブの「Search」ボタンは Search タブへ切り替え

### Added
- `DebugSection` / `DebugSectionCollection` / `DebugTabDefinition.Sections`、セクションを後から足せるタブのコンストラクタ
- `OptionListModel`（セクション内リストのモデル、UI 非依存でテスト可能）

## [0.3.0] - 2026-11-14

### Added
- サンプル「Options Showcase」（全組み込み型、3 階層、グループ、カスタム描画 Lv2/3/5、動的カテゴリ、ページ、ログ生成、1000 件の負荷テスト）
- Options タブ
  - **仮想化**（ツリーを表示中の行に平坦化し、可変高さ ListView で描画。行ビューは遅延生成してキャッシュ）
  - 任意の深さのカテゴリツリー、検索（ツリーを保ちヒットを含む枝だけ展開・ハイライト）
  - グループ表示（横 / 縦レイアウト、Pending 表示と Revert）、Pin ボタン
  - 組み込みコントロール: bool / 整数 / 浮動小数（範囲指定ならスライダー、増減ボタン）/ string / enum / Flags / Vector2-4 / Color / 読み取り専用 / メソッド
  - 動的な追加・削除・カテゴリ増減に追従。値は画面に出ている行だけをポーリング
- カスタム描画: `OptionView` / `OptionContext` / `OptionViews`
  - Lv2 型ごと `DebuggerBuilder.AddOptionView<T>` / `OptionViews.Register<T>`（デバッガごと）、Lv3 `[OptionView(typeof(T))]` / `WithView`、Lv4 `[OptionUxml(key)]` / `RegisterUxml`、Lv5 `[OptionElement]` / `OptionDefinition.FromElement`
  - カスタム描画が例外を出してもその行だけエラー表示
- Console タブ: 仮想化リスト、Log/Warn/Error トグル（件数）、テキスト / 正規表現フィルタ、Collapse、詳細とスタックトレース、コピー、末尾追従
- `ConsoleFilter`、`OptionValueConverter`

### Fixed
- 縦レイアウトのグループで各メンバーの高さが 0 に潰れ、内容がはみ出していた


## [0.2.0] - 2026-11-14

### Added
- UI Toolkit パネル基盤（独自の UIDocument / PanelSettings、EventSystem 不要）
  - ヘッダー（戻る / タイトル / ツールバー / 閉じる）、タブバー、SafeArea 追従、画面密度に応じたスケール
  - トリガー（タップ / ダブル / トリプル / 長押し）、エラー発生時のトリガー強調
  - キーボードショートカット（Input System / 旧 Input Manager の両対応、既定 F1）、Escape / ゲームパッドで戻る
  - ダークテーマ（`AtsuDebuggerTheme.tss`）。標準コントロールもダーク化。追加の USS で上書き可能
- ページ: `DebugPage`、`DebugPage.FromElement` / `FromUxml`、タブごとのページスタック（`IPageNavigator`）
  - `PageChrome` によるヘッダー / タブバーの非表示、ヘッダーツールバー、`OnBackRequested`
- カスタムタブ: `DebugTabDefinition` / `IDebugService.Tabs`
- `OptionDefinition.FromPage`、`IDebugService.PushPage` / `ShowDebugPanel(string tabId)` / `IsScreenPointOverPanel`
- System Info タブ（1秒ごとに更新、クリップボードへコピー）
- 設定アセット `DebuggerSettingsAsset`（自動作成し Preloaded Assets に登録。無効化ビルドでは除外）
- サンプル「Custom Page」

## [0.1.x] - 2026-11-14

### Added
- 任意の深さのカテゴリ階層（`CategoryPath`, `CategoryNode`, `OptionRegistry.Root` / `FindCategory`）
- クラスに付ける `[OptionCategory]`、`[OptionCategory("A", "B", "C")]`、`[OptionDescription]`
- カテゴリ接頭辞付きの登録 `AddOptionContainer(container, "Enemies/Goblin#3")`、`RemoveOptionCategory`
- カテゴリの増減イベント（`CategoryAdded` / `CategoryRemoved`）、空カテゴリの自動削除
- オプション検索 `OptionRegistry.Search`（AND 検索、スコア順、全角半角・カナかなを区別しない）
- 検索結果をツリー上で絞り込む `OptionTreeFilter`（ヒットを含む枝だけを表示・展開）
- オプショングループ `[OptionGroup]` / `OptionGroup` / `CategoryNode.Entries`（入力欄とボタンを1つの UI として扱い、ボタンで確定）
- SROptions Shim に `SROptions.GroupAttribute` を追加

### Changed
- `PinAllOptions` / `UnpinAllOptions` が配下のカテゴリも対象にするようになった
- グループに属するオプションの Pin / Unpin はグループ全体に適用されるようになった

## [0.1.0] - 2026-11-14

### Added
- パッケージ骨格（Runtime / Editor / Compat / Tests / Samples~）
- コア API: `AtsuDebug`, `IDebugService`, Options（走査・登録・Pin）, Console ログバッファ, System Info
- SRDebugger 互換 API（`SRDebug`, `SRDebugger.*`）と SROptions Shim サンプル
