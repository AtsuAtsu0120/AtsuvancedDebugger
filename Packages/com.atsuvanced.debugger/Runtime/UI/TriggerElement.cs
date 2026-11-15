using System;
using UnityEngine;
using UnityEngine.UIElements;

namespace Atsuvanced.Debugger.UI
{
    /// <summary>Invisible-ish corner hot spot that opens the panel (tap / double / triple tap / long press).</summary>
    internal sealed class TriggerElement : VisualElement
    {
        private const float MultiTapWindow = 0.45f;
        private const long LongPressMs = 600;

        private readonly Action _activated;
        private TriggerBehaviour _behaviour;
        private float _lastTapTime;
        private IVisualElementScheduledItem _longPressItem;
        private bool _pressed;
        private int _tapCount;

        public TriggerElement(Action activated)
        {
            _activated = activated;
            AddToClassList("atsu-trigger");
            RegisterCallback<PointerDownEvent>(OnPointerDown);
            RegisterCallback<PointerUpEvent>(OnPointerUp);
            RegisterCallback<PointerCancelEvent>(_ => Release());
        }

        public void Configure(TriggerBehaviour behaviour, float size)
        {
            _behaviour = behaviour;
            style.width = size;
            style.height = size;
        }

        public void SetErrorState(bool hasError)
        {
            EnableInClassList("atsu-trigger--error", hasError);
        }

        private void OnPointerDown(PointerDownEvent evt)
        {
            _pressed = true;
            this.CapturePointer(evt.pointerId);
            AddToClassList("atsu-trigger--pressed");

            if (_behaviour == TriggerBehaviour.LongPress)
            {
                _longPressItem?.Pause();
                _longPressItem = schedule.Execute(() =>
                {
                    if (!_pressed)
                    {
                        return;
                    }

                    _activated();
                }).StartingIn(LongPressMs);
            }

            evt.StopPropagation();
        }

        private void OnPointerUp(PointerUpEvent evt)
        {
            if (this.HasPointerCapture(evt.pointerId))
            {
                this.ReleasePointer(evt.pointerId);
            }

            var wasPressed = _pressed;
            Release();
            evt.StopPropagation();
            if (!wasPressed || _behaviour == TriggerBehaviour.LongPress)
            {
                return;
            }

            var required = _behaviour == TriggerBehaviour.TripleTap ? 3 :
                _behaviour == TriggerBehaviour.DoubleTap ? 2 : 1;
            var now = Time.unscaledTime;
            if (now - _lastTapTime > MultiTapWindow)
            {
                _tapCount = 0;
            }

            _lastTapTime = now;
            if (++_tapCount < required)
            {
                return;
            }

            _tapCount = 0;
            _activated();
        }

        private void Release()
        {
            _pressed = false;
            _longPressItem?.Pause();
            RemoveFromClassList("atsu-trigger--pressed");
        }
    }
}