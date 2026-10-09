using UnityEngine;
using UnityEngine.InputSystem;

namespace SeaweedSlayer
{
    [DisallowMultipleComponent]
    public sealed class QuitOnEscape : MonoBehaviour
    {
        void Update()
        {
            if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
                Quit();
        }

        public void Quit()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }
    }
}
