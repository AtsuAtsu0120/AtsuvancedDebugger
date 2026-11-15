using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UIElements;

namespace Atsuvanced.Debugger.Tests
{
    public class PageNavigatorTests
    {
        private bool _closed;
        private List<string> _log;
        private PageNavigator _nav;

        private DebugTabRegistry _tabs;

        [SetUp]
        public void SetUp()
        {
            _log = new List<string>();
            _tabs = new DebugTabRegistry();
            _tabs.Add(new DebugTabDefinition("a", "A", () => new RecordingPage("A", _log), 0));
            _tabs.Add(new DebugTabDefinition("b", "B", () => new RecordingPage("B", _log), 1));
            _closed = false;
            _nav = new PageNavigator(_tabs, null, () => _closed = true);
        }

        [Test]
        public void PagesAreShownOnlyWhileActive()
        {
            _nav.SelectTab("a");
            Assert.IsEmpty(_log, "Root page is created lazily, only when the panel is visible.");

            _nav.SetActive(true);
            CollectionAssert.AreEqual(new[] { "A:create", "A:show" }, _log);

            _nav.SetActive(false);
            Assert.AreEqual("A:hide", _log[_log.Count - 1]);
        }

        [Test]
        public void PushPop_RunsLifecycleAndKeepsStackPerTab()
        {
            _nav.SetActive(true);
            _nav.SelectTab("a");
            var detail = new RecordingPage("Detail", _log);
            _nav.Push(detail);

            Assert.AreEqual(2, _nav.Depth);
            Assert.IsTrue(_nav.CanGoBack);
            Assert.AreSame(detail, _nav.CurrentPage);
            Assert.AreSame(_nav, detail.Navigator);

            _nav.SelectTab("b");
            Assert.AreEqual(1, _nav.Depth);
            _nav.SelectTab("a");
            Assert.AreSame(detail, _nav.CurrentPage, "Each tab keeps its own stack.");

            _log.Clear();
            Assert.IsTrue(_nav.Pop());
            CollectionAssert.AreEqual(new[] { "Detail:hide", "Detail:dispose", "A:show" }, _log);
            Assert.IsFalse(_nav.Pop(), "Root page cannot be popped.");
        }

        [Test]
        public void OnBackRequested_CanCancelPop()
        {
            _nav.SetActive(true);
            _nav.SelectTab("a");
            var page = new RecordingPage("Form", _log) { AllowBack = false };
            _nav.Push(page);

            Assert.IsFalse(_nav.Pop());
            Assert.AreSame(page, _nav.CurrentPage);

            page.AllowBack = true;
            Assert.IsTrue(_nav.Pop());
        }

        [Test]
        public void SamePageCannotBePushedTwice()
        {
            _nav.SelectTab("a");
            var page = new RecordingPage("P", _log);
            _nav.Push(page);
            Assert.Throws<InvalidOperationException>(() => _nav.Push(page));
        }

        [Test]
        public void ContentException_ShowsErrorInsteadOfThrowing()
        {
            _nav.SetActive(true);
            _nav.SelectTab("a");
            LogAssert.Expect(LogType.Exception, new Regex("boom"));

            var page = new ThrowingPage();
            _nav.Push(page);

            Assert.NotNull(page.Content);
            Assert.IsTrue(page.Content.ClassListContains("atsu-error-box"));
        }

        [Test]
        public void RemovingCurrentTab_DisposesItsPagesAndSelectsAnother()
        {
            _nav.SetActive(true);
            _nav.SelectTab("b");
            _nav.Push(new RecordingPage("Bd", _log));

            _tabs.Remove("b");

            Assert.Contains("Bd:dispose", _log);
            Assert.Contains("B:dispose", _log);
            Assert.AreEqual("a", _nav.CurrentTabId);
        }

        [Test]
        public void Close_InvokesCallback()
        {
            _nav.Close();
            Assert.IsTrue(_closed);
        }

        [Test]
        public void Reset_DisposesEverything()
        {
            _nav.SetActive(true);
            _nav.SelectTab("a");
            _nav.Push(new RecordingPage("P", _log));
            _nav.Reset();

            Assert.Contains("P:dispose", _log);
            Assert.Contains("A:dispose", _log);
            Assert.IsNull(_nav.CurrentPage);
        }

        private sealed class RecordingPage : DebugPage
        {
            public readonly List<string> Log;
            public bool AllowBack = true;

            public RecordingPage(string title, List<string> log) : base(title)
            {
                Log = log;
            }

            protected override VisualElement CreateContent()
            {
                Log.Add(Title + ":create");
                return new VisualElement();
            }

            protected override void OnShow()
            {
                Log.Add(Title + ":show");
            }

            protected override void OnHide()
            {
                Log.Add(Title + ":hide");
            }

            protected override void OnDispose()
            {
                Log.Add(Title + ":dispose");
            }

            protected override bool OnBackRequested()
            {
                return AllowBack;
            }
        }

        private sealed class ThrowingPage : DebugPage
        {
            protected override VisualElement CreateContent()
            {
                throw new InvalidOperationException("boom");
            }
        }
    }
}