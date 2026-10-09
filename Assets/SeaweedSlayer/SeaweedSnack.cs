using UnityEngine;
namespace SeaweedSlayer
{
    public sealed class SeaweedSnack : MonoBehaviour
    {
        SeaweedArena arena;
        [SerializeField] Transform billboard;
        [SerializeField] float bobAmplitude = .09f, bobSpeed = 3;
        Vector3 origin, visualOrigin;
        bool capturedOffset;
        public void Initialize(SeaweedArena a)
        {
            arena = a; origin = transform.position;
            if (!capturedOffset) { visualOrigin = billboard.localPosition; capturedOffset = true; }
        }
        void Update()
        {
            if (arena == null) return;
            billboard.localPosition = visualOrigin + Vector3.up * (Mathf.Sin(Time.time * bobSpeed + origin.x) * bobAmplitude);
            if (Camera.main != null) billboard.rotation = Camera.main.transform.rotation;
            if (arena.Session.Phase != "Playing") return;
            foreach (var player in arena.Session.Players.Values)
                if (player.gameObject.activeSelf && !player.Stunned && (player.transform.position - origin).sqrMagnitude < .8f)
                { player.Score += 25; gameObject.SetActive(false); break; }
        }
    }
}
