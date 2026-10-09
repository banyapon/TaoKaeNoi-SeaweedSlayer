using System.Linq;
using UnityEngine;

namespace SeaweedSlayer
{
    public sealed class SeaweedEnemy : MonoBehaviour
    {
        SeaweedArena arena;
        [SerializeField] CharacterController body;
        [SerializeField] Transform visual;
        [SerializeField] Animation animationPlayer;
        [SerializeField] SpriteRenderer outline;
        [SerializeField] Renderer[] renderers;
        string runClip;
        float nextContact, nextWander;
        Vector3 wander;
        bool defeated;
        public void Initialize(SeaweedArena a, Color color)
        {
            arena = a;
            defeated = false; nextContact = nextWander = 0;
            outline.color = color;
            foreach (var renderer in renderers)
            {
                var tint = new MaterialPropertyBlock(); renderer.GetPropertyBlock(tint);
                tint.SetColor("_BaseColor", color); tint.SetColor("_Color", color);
                tint.SetColor("baseColorFactor", color); renderer.SetPropertyBlock(tint);
            }
            if (animationPlayer != null)
            {
                foreach (AnimationState clip in animationPlayer)
                    if (clip.name.ToLowerInvariant().Contains("run") || clip.name.ToLowerInvariant().Contains("walk")) { runClip = clip.name; break; }
                if (runClip != null) animationPlayer.Play(runClip);
            }
        }
        public void Hit(int shooter)
        {
            if (arena == null || defeated || arena.Session.Phase != "Playing" || !arena.Session.Players.TryGetValue(shooter, out var player)) return;
            defeated = true; player.Score += arena.Session.settings.enemyScore;
            arena.Session.UI.Refresh(); gameObject.SetActive(false);
        }
        void Update()
        {
            if (arena == null || defeated || arena.Session.Phase != "Playing") return;
            var target = arena.Session.Players.Values.Where(p => p.gameObject.activeSelf)
                .OrderBy(p => (p.transform.position - transform.position).sqrMagnitude).FirstOrDefault();
            if (target == null) return;
            if (Time.time > nextWander) { nextWander = Time.time + 2; wander = arena.FreePosition(); }
            Vector3 destination = (target.transform.position - transform.position).sqrMagnitude < 100 ? target.transform.position : wander;
            Vector3 direction = destination - transform.position; direction.y = 0;
            if (direction.sqrMagnitude > .1f) visual.rotation = Quaternion.Slerp(visual.rotation, Quaternion.LookRotation(direction), Time.deltaTime * 8);
            body.Move((direction.normalized * 2.2f + Vector3.down * 2) * Time.deltaTime);
            var pos = transform.position; pos.x = Mathf.Clamp(pos.x, -16.3f, 16.3f); pos.z = Mathf.Clamp(pos.z, -10.3f, 10.3f); transform.position = pos;
            if (Time.time >= nextContact && (target.transform.position - pos).sqrMagnitude < 1.25f)
            { target.Hit(); nextContact = Time.time + 3; wander = arena.FreePosition(); }
        }
    }
}
