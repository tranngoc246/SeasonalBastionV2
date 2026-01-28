using UnityEngine;
using UnityEngine.InputSystem;

namespace SeasonalBastion.DebugTools
{
    public sealed class DebugInputRouter : MonoBehaviour
    {
        [SerializeField] private DebugHUDHub _hub;

        private void Awake()
        {
            if (_hub == null) _hub = GetComponentInChildren<DebugHUDHub>(true);
            if (_hub == null) _hub = FindObjectOfType<DebugHUDHub>();
        }

        private void Update()
        {
            var kb = Keyboard.current;
            if (kb == null || _hub == null) return;

            _hub.HandleHotkeys(kb);
        }
    }
}
