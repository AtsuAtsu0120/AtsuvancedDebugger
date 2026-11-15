using UnityEngine;

namespace AtsuvancedDebuggerSamples.Showcase
{
    /// <summary>Simulates the game changing values on its own (Gold +1 per second).</summary>
    public sealed class ShowcaseTicker : MonoBehaviour
    {
        public PlayerOptions Player;
        private float _timer;

        private void Update()
        {
            _timer += Time.unscaledDeltaTime;
            if (_timer < 1f) return;
            _timer = 0f;
            if (Player != null) Player.Gold += 1;
        }
    }
}
