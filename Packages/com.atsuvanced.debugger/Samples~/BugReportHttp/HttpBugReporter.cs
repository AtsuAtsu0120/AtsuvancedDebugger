using System;
using System.Collections;
using Atsuvanced.Debugger;
using UnityEngine;
using UnityEngine.Networking;

namespace Atsuvanced.Debugger.Samples
{
    /// <summary>
    /// Sends bug reports to an HTTP endpoint as multipart/form-data (FR-BUG-03 sample):
    /// <c>report</c> = JSON (<see cref="BugReportBuilder.ToJson"/>), <c>text</c> = readable text, <c>screenshot</c> = PNG.
    /// Adapt the form fields / headers to your server, Slack, Jira, etc.
    /// </summary>
    public sealed class HttpBugReporter : IBugReporter
    {
        private readonly string _url;
        private readonly Action<UnityWebRequest> _configure;
        private readonly MonoBehaviour _host;

        /// <param name="url">Endpoint that accepts a multipart POST.</param>
        /// <param name="host">Any active MonoBehaviour to run the upload coroutine on.</param>
        /// <param name="configure">Optional: add headers (e.g. an auth token) before sending.</param>
        public HttpBugReporter(string url, MonoBehaviour host, Action<UnityWebRequest> configure = null)
        {
            _url = url ?? throw new ArgumentNullException(nameof(url));
            _host = host ? host : throw new ArgumentNullException(nameof(host));
            _configure = configure;
        }

        public void Submit(BugReport report, Action<float> onProgress, Action<bool, string> onComplete)
        {
            _host.StartCoroutine(Send(report, onProgress, onComplete));
        }

        private IEnumerator Send(BugReport report, Action<float> onProgress, Action<bool, string> onComplete)
        {
            var form = new WWWForm();
            form.AddField("report", BugReportBuilder.ToJson(report));
            form.AddField("text", BugReportBuilder.ToText(report));
            if (report.Screenshot != null) form.AddBinaryData("screenshot", report.Screenshot, "screenshot.png", "image/png");

            using var request = UnityWebRequest.Post(_url, form);
            _configure?.Invoke(request);
            var operation = request.SendWebRequest();
            while (!operation.isDone)
            {
                onProgress?.Invoke(request.uploadProgress);
                yield return null;
            }

            if (request.result == UnityWebRequest.Result.Success) onComplete(true, $"HTTP {request.responseCode}");
            else onComplete(false, request.error);
        }
    }

    /// <summary>
    /// Installs <see cref="HttpBugReporter"/> at startup. Put this on a GameObject in your first scene and set the URL.
    /// </summary>
    public sealed class HttpBugReporterInstaller : MonoBehaviour
    {
        [SerializeField] private string _url = "https://example.com/bug-reports";
        [SerializeField] private string _authorizationHeader;

        private void Awake()
        {
            DontDestroyOnLoad(gameObject);
            AtsuDebug.Instance.BugReports.Reporter = new HttpBugReporter(_url, this, request =>
            {
                if (!string.IsNullOrEmpty(_authorizationHeader)) request.SetRequestHeader("Authorization", _authorizationHeader);
            });
        }
    }
}
