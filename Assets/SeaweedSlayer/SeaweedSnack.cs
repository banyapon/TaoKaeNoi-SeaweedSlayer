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
        Sprite[] items;
        static readonly int[] ItemPoints = { 5, 10, 10, 20 };
        public int Points { get; private set; }
        public void Initialize(SeaweedArena a)
        {
            arena = a; origin = transform.position;
            if (!capturedOffset) { visualOrigin = billboard.localPosition; capturedOffset = true; }
            if (items == null) items = new[] {
                Resources.Load<Sprite>("sprites/itemA"), Resources.Load<Sprite>("sprites/itemB"),
                Resources.Load<Sprite>("sprites/itemC"), Resources.Load<Sprite>("sprites/itemD")
            };
            int kind = Random.Range(0, items.Length);
            Points = ItemPoints[kind];
            var renderer = billboard.GetComponent<SpriteRenderer>();
            renderer.sprite = items[kind];
            if (renderer.sprite != null)
            {
                var size = renderer.sprite.bounds.size;
                billboard.localScale = Vector3.one * (a.Session.settings.itemSize / Mathf.Max(size.x, size.y));
            }
        }
        void Update()
        {
            if (arena == null) return;
            billboard.localPosition = visualOrigin + Vector3.up * (Mathf.Sin(Time.time * bobSpeed + origin.x) * bobAmplitude);
            if (Camera.main != null) billboard.rotation = Camera.main.transform.rotation;
            if (arena.Session.Phase != "Playing") return;
            foreach (var player in arena.Session.Players.Values)
                if (player.gameObject.activeSelf && !player.Stunned && (player.transform.position - origin).sqrMagnitude < .8f)
                {
                    player.Score += Points;
                    arena.Session.PlayItemSound();
                    SeaweedFeedback.ShowScore(player.transform.position + Vector3.up * 1.8f, Points, arena.Session.settings.uiFont, arena.transform);
                    arena.Session.UI.Refresh();
                    gameObject.SetActive(false); break;
                }
        }
    }
}
