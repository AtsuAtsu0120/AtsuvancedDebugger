# SROptions Shim

SRDebugger の `SROptions` を置き換えるファイル群です。Import すると `Assets/Samples/...` 配下に配置されます。

- 既存の `partial class SROptions { ... }` はそのまま動作します。
- `SROptions` を参照している asmdef がある場合は、このフォルダをその asmdef と同じアセンブリに移動してください。
- 起動時に `SRDebug.Instance.AddOptionContainer(SROptions.Current)` が自動実行されます。
