using UnityEngine;
namespace SeaweedSlayer
{
    public sealed class SeaweedCube : MonoBehaviour
    {
        SeaweedArena arena; int owner; Vector3 direction; float expires;
        public void Initialize(SeaweedArena a, int actor, Vector3 aim) { arena = a; owner = actor; direction = aim; expires = Time.time + 1.4f; }
        void Update()
        {
            if (arena == null) return;
            if (arena.Session.Phase != "Playing" || Time.time > expires) { gameObject.SetActive(false); return; }
            Vector3 start = transform.position; Vector3 step = direction * (13 * Time.deltaTime);
            // SphereCast ignores targets already overlapping the starting sphere, such as a close enemy.
            foreach (var collider in Physics.OverlapSphere(start, .16f, ~0, QueryTriggerInteraction.Ignore))
                if (Impact(collider)) return;
            // Swept query prevents small fast cubes passing through characters between frames.
            var hits = Physics.SphereCastAll(start, .16f, direction, step.magnitude, ~0, QueryTriggerInteraction.Ignore);
            System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));
            foreach (var hit in hits)
            {
                if (Impact(hit.collider)) return;
            }
            transform.position += step; transform.Rotate(new Vector3(150, 220, 90) * Time.deltaTime);
        }
        bool Impact(Collider collider)
        {
            if (collider.transform.IsChildOf(transform)) return false;
            var avatar = collider.GetComponentInParent<SeaweedAvatar>();
            if (avatar != null && avatar.Actor == owner) return false;
            var enemy = collider.GetComponentInParent<SeaweedEnemy>();
            if (enemy != null) enemy.Hit(owner);
            else if (avatar != null) avatar.Hit();
            gameObject.SetActive(false); return true;
        }
    }
}
