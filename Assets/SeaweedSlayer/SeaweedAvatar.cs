using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;

namespace SeaweedSlayer
{
    public sealed class SeaweedAvatar : MonoBehaviour
    {
        public int Actor { get; private set; }
        public string DisplayName { get; private set; }
        public Color Color { get; private set; }
        public int Score;
        public bool Stunned => Time.time < stunEnds;
        SeaweedArena arena;
        [SerializeField] CharacterController body;
        [SerializeField] Transform visual, nameplate;
        [SerializeField] Renderer[] renderers;
        [SerializeField] Text label;
        [SerializeField] SpriteRenderer outline;
        Vector2 input;
        Vector3 facing = Vector3.forward;
        float inputAt, stunEnds, nextThrow, animationLocked;
        [SerializeField] Animation animationPlayer;
        public void Initialize(SeaweedArena a, int actor, string name, Color color)
        {
            arena = a; Actor = actor;
            Score = 0; facing = Vector3.forward; animationLocked = 0;
            ResetAt(transform.position);
            SetProfile(name, color);
        }
        public void SetProfile(string name, Color color) { DisplayName = name; Color = color; label.text = name; label.color = color; outline.color = color; }
        public void SetInput(Vector2 direction) { input = direction; inputAt = Time.time; }
        public void ResetAt(Vector3 p)
        { body.enabled = false; transform.position = p; body.enabled = true; stunEnds = nextThrow = 0; input = Vector2.zero; foreach (var r in renderers) r.enabled = true; }
        public void Hit() { if (Stunned) return; stunEnds = Time.time + 2; input = Vector2.zero; }
        public void Throw()
        {
            if (arena.Session.Phase != "Playing" || Stunned || Time.time < nextThrow) return;
            nextThrow = Time.time + .65f; arena.SpawnCube(this, facing);
            if (animationPlayer != null && animationPlayer["Throw_Left"] != null) { animationPlayer.CrossFade("Throw_Left", .08f); animationLocked = Time.time + .4f; }
        }
        void Update()
        {
            if (arena == null) return;
            bool playing = arena.Session.Phase == "Playing";
            if (Actor == 0 && Keyboard.current != null && playing)
            {
                var k = Keyboard.current;
                SetInput(new Vector2((k.dKey.isPressed || k.rightArrowKey.isPressed ? 1 : 0) - (k.aKey.isPressed || k.leftArrowKey.isPressed ? 1 : 0),
                    (k.wKey.isPressed || k.upArrowKey.isPressed ? 1 : 0) - (k.sKey.isPressed || k.downArrowKey.isPressed ? 1 : 0)));
                if (k.spaceKey.wasPressedThisFrame) Throw();
            }
            if (Time.time - inputAt > .35f) input = Vector2.zero;
            var move = playing && !Stunned ? new Vector3(input.x, 0, input.y) : Vector3.zero;
            if (move.sqrMagnitude > .02f) { facing = move.normalized; visual.rotation = Quaternion.Slerp(visual.rotation, Quaternion.LookRotation(facing), Time.deltaTime * 14); }
            body.Move((Vector3.ClampMagnitude(move, 1) * 5 + Vector3.down * 2) * Time.deltaTime);
            var pos = transform.position; pos.x = Mathf.Clamp(pos.x, -16.4f, 16.4f); pos.z = Mathf.Clamp(pos.z, -10.4f, 10.4f); transform.position = pos;
            bool visible = !Stunned || Mathf.FloorToInt(Time.time * 12) % 2 == 0;
            foreach (var r in renderers) r.enabled = visible;
            if (animationPlayer != null && Time.time >= animationLocked)
            {
                string clip = move.sqrMagnitude > .02f ? "Run_Cute" : "Idle_Cute";
                if (animationPlayer[clip] != null && !animationPlayer.IsPlaying(clip)) animationPlayer.CrossFade(clip, .12f);
            }
        }
        void LateUpdate() { if (Camera.main != null) { nameplate.rotation = Camera.main.transform.rotation; } }
    }
}
