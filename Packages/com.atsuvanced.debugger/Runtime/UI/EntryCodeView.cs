using System;
using System.Text;
using UnityEngine;
using UnityEngine.UIElements;

namespace Atsuvanced.Debugger.UI
{
    /// <summary>
    ///     PIN pad shown before the panel opens when an entry code is set (FR-TRG-05).
    ///     Digits can be tapped or typed (0-9, Backspace, Enter, Escape).
    ///     <code>
    /// .atsu-entry-code (full screen dim)
    ///   .atsu-entry-code__box   title | dots | message | keypad (1-9, Del, 0, ×)
    /// </code>
    /// </summary>
    internal sealed class EntryCodeView : VisualElement
    {
        private readonly VisualElement _box;
        private readonly Action _cancel;
        private readonly VisualElement[] _dots = new VisualElement[EntryCodeGate.Length];
        private readonly StringBuilder _input = new();
        private readonly Label _message;
        private readonly Func<string, bool> _submit;

        public EntryCodeView(Func<string, bool> submit, Action cancel)
        {
            _submit = submit;
            _cancel = cancel;
            name = "atsu-entry-code";
            AddToClassList("atsu-entry-code");
            focusable = true;

            _box = new VisualElement();
            _box.AddToClassList("atsu-entry-code__box");
            Add(_box);

            var title = new Label(DebuggerStrings.Get("entry.title"));
            title.AddToClassList("atsu-entry-code__title");
            _box.Add(title);

            var dots = new VisualElement();
            dots.AddToClassList("atsu-entry-code__dots");
            for (var i = 0; i < _dots.Length; i++)
            {
                _dots[i] = new VisualElement();
                _dots[i].AddToClassList("atsu-entry-code__dot");
                dots.Add(_dots[i]);
            }

            _box.Add(dots);

            _message = new Label();
            _message.AddToClassList("atsu-entry-code__message");
            _box.Add(_message);

            var pad = new VisualElement();
            pad.AddToClassList("atsu-entry-code__pad");
            VisualElement row = null;
            for (var d = 1; d <= 9; d++)
            {
                if ((d - 1) % 3 == 0)
                {
                    pad.Add(row = Row());
                }

                row.Add(Key(d.ToString(), AppendDigit(d)));
            }

            pad.Add(row = Row());
            row.Add(Key(DebuggerStrings.Get("entry.delete"), Backspace, "atsu-entry-code__key--sub"));
            row.Add(Key("0", AppendDigit(0)));
            row.Add(Key("×", () => _cancel?.Invoke(), "atsu-entry-code__key--sub"));
            _box.Add(pad);

            RegisterCallback<KeyDownEvent>(OnKeyDown, TrickleDown.TrickleDown);
            RegisterCallback<NavigationCancelEvent>(e =>
            {
                _cancel?.Invoke();
                e.StopPropagation();
            });
        }

        /// <summary>The digits typed so far (for tests / tooling).</summary>
        internal string Input => _input.ToString();

        public void Reset()
        {
            _input.Clear();
            _message.text = string.Empty;
            _box.RemoveFromClassList("atsu-entry-code__box--wrong");
            UpdateDots();
        }

        /// <summary>Types one character ('0'-'9'); submits automatically after the last digit.</summary>
        internal void Type(char c)
        {
            if (c < '0' || c > '9' || _input.Length >= EntryCodeGate.Length)
            {
                return;
            }

            _input.Append(c);
            _message.text = string.Empty;
            _box.RemoveFromClassList("atsu-entry-code__box--wrong");
            UpdateDots();
            if (_input.Length < EntryCodeGate.Length)
            {
                return;
            }

            var code = _input.ToString();
            _input.Clear();
            if (_submit(code))
            {
                return;
            }

            _message.text = DebuggerStrings.Get("entry.wrong");
            _box.AddToClassList("atsu-entry-code__box--wrong");
            UpdateDots();
        }

        private Action AppendDigit(int d)
        {
            return () => Type((char)('0' + d));
        }

        private void Backspace()
        {
            if (_input.Length == 0)
            {
                return;
            }

            _input.Length--;
            UpdateDots();
        }

        private void UpdateDots()
        {
            for (var i = 0; i < _dots.Length; i++)
            {
                _dots[i].EnableInClassList("atsu-entry-code__dot--filled", i < _input.Length);
            }
        }

        private void OnKeyDown(KeyDownEvent e)
        {
            var c = e.character;
            if (c >= '0' && c <= '9')
            {
                Type(c);
            }
            else if (e.keyCode == KeyCode.Backspace || e.keyCode == KeyCode.Delete)
            {
                Backspace();
            }
            else if (e.keyCode == KeyCode.Escape)
            {
                _cancel?.Invoke();
            }
            else
            {
                return;
            }

            e.StopPropagation();
        }

        private static VisualElement Row()
        {
            var row = new VisualElement();
            row.AddToClassList("atsu-entry-code__row");
            return row;
        }

        private static Button Key(string text, Action clicked, string extraClass = null)
        {
            var b = new Button(clicked) { text = text };
            b.AddToClassList("atsu-button");
            b.AddToClassList("atsu-entry-code__key");
            if (extraClass != null)
            {
                b.AddToClassList(extraClass);
            }

            return b;
        }
    }
}