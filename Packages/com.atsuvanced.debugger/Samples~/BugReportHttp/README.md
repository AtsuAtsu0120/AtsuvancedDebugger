# Bug Report (HTTP) sample

`HttpBugReporter` sends bug reports to your server as `multipart/form-data`:

| field | content |
|-------|---------|
| `report` | JSON (`BugReportBuilder.ToJson`): description, email, system information (private entries excluded), console log |
| `text` | the same report as readable text (`BugReportBuilder.ToText`) |
| `screenshot` | PNG taken without the debug panel (only when attached) |

Usage: add `HttpBugReporterInstaller` to a GameObject in your first scene and set the URL (and an `Authorization` header if needed),
or set `AtsuDebug.Instance.BugReports.Reporter = new HttpBugReporter(url, someMonoBehaviour)` yourself.

The default reporter (`LocalBugReporter`) saves reports under `Application.persistentDataPath/AtsuvancedDebugger/BugReports`.

Requires the built-in **Unity Web Request** module (enabled by default in new projects).
