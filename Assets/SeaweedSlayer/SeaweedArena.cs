
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
namespace SeaweedSlayer
{
    public sealed class SeaweedArena : MonoBehaviour
    {
        public const float HalfWidth = 17, HalfDepth = 11;
        public SeaweedSession Session { get; private set; }
        [SerializeField] SeaweedAvatar[] players;
        [SerializeField] SeaweedSnack[] snacks;
        [SerializeField] SeaweedCube[] shots;
        [SerializeField] SeaweedEnemy[] enemies;
        [SerializeField] Transform[] trees;
        [SerializeField] Transform[] grass;
        [SerializeField] Transform[] fixedObstacles;
        [SerializeField] bool randomizeTrees = true;
        [SerializeField] bool randomizeGrass = true;
        readonly HashSet<SeaweedAvatar> assigned = new HashSet<SeaweedAvatar>();
        readonly List<Vector3> blocked = new List<Vector3>();
        float nextSnack, nextEnemy;
        public void Initialize(SeaweedSession session)
        {
            Session = session;
            foreach (var player in players) player.gameObject.SetActive(false);
            ClearSnacks(); ClearProjectiles(); ClearEnemies(); RefreshObstacles();
        }
        void RefreshObstacles()
        {
            blocked.Clear();
            foreach (var obstacle in fixedObstacles) if (obstacle != null && obstacle.gameObject.activeSelf) blocked.Add(obstacle.position);
            foreach (var tree in trees) if (tree != null && tree.gameObject.activeSelf) blocked.Add(tree.position);
        }
        public void NewRound()
        {
            ClearSnacks(); ClearProjectiles(); ClearEnemies();
            if (randomizeTrees)
            {
                blocked.Clear();
                foreach (var obstacle in fixedObstacles) if (obstacle != null && obstacle.gameObject.activeSelf) blocked.Add(obstacle.position);
                foreach (var tree in trees)
                {
                    if (tree == null || !tree.gameObject.activeSelf) continue;
                    for (int attempt = 0; attempt < 200; attempt++)
                    {
                        var position = FreePosition();
                        if (Mathf.Abs(position.x) < 3 || Mathf.Abs(position.z) < 2.5f) continue;
                        tree.position = position; tree.rotation = Quaternion.Euler(0, Random.Range(0, 360), 0); break;
                    }
                    blocked.Add(tree.position);
                }
            }
            else RefreshObstacles();
            if (randomizeGrass) ScatterGrass();
            for (int i = 0; i < 28; i++) SpawnSnack();
            nextSnack = Time.time + 1; nextEnemy = Time.time + 3;
        }
        void ScatterGrass()
        {
            if (grass == null) return;
            foreach (var patch in grass)
            {
                if (patch == null || !patch.gameObject.activeSelf) continue;
                for (int attempt = 0; attempt < 200; attempt++)
                {
                    var p = new Vector3(Random.Range(-14f, 14f), patch.position.y, Random.Range(-8f, 8f));
                    bool free = true;
                    foreach (var b in blocked) if ((b - p).sqrMagnitude < 8) { free = false; break; }
                    foreach (var other in grass)
                        if (other != null && other != patch && other.gameObject.activeSelf && (other.position - p).sqrMagnitude < 25) { free = false; break; }
                    if (!free) continue;
                    patch.SetPositionAndRotation(p, Quaternion.Euler(0, Random.Range(0, 360f), 0));
                    break;
                }
            }
        }
        public Vector3 FreePosition()
        {
            for (int i = 0; i < 200; i++)
            {
                var p = new Vector3(Random.Range(-15f, 15f), .05f, Random.Range(-9f, 9f)); bool free = true;
                foreach (var b in blocked) if ((b - p).sqrMagnitude < 12) { free = false; break; }
                if (Session != null) foreach (var a in Session.Players.Values)
                    if (a != null && a.gameObject.activeSelf && (a.transform.position - p).sqrMagnitude < 2) { free = false; break; }
                if (free) return p;
            }
            return new Vector3(0, .05f, 0);
        }
        public SeaweedAvatar CreateAvatar(int actor, string name, Color color)
        {
            var player = players.FirstOrDefault(p => !assigned.Contains(p));
            if (player == null) { Debug.LogWarning("All authored player slots are assigned."); return null; }
            assigned.Add(player); player.transform.position = FreePosition();
            player.Initialize(this, actor, name, color); player.gameObject.SetActive(true); return player;
        }
        public void ReleaseAvatar(SeaweedAvatar player)
        { if (player == null) return; player.SetInput(Vector2.zero); player.gameObject.SetActive(false); assigned.Remove(player); }
        public void SpawnCube(SeaweedAvatar owner, Vector3 direction)
        {
            var cube = shots.FirstOrDefault(p => !p.gameObject.activeSelf); if (cube == null) return;
            cube.transform.SetPositionAndRotation(owner.transform.position + Vector3.up * .85f + direction * .8f, Quaternion.identity);
            cube.Initialize(this, owner.Actor, direction); cube.gameObject.SetActive(true);
        }
        void SpawnSnack()
        {
            var snack = snacks.FirstOrDefault(p => !p.gameObject.activeSelf); if (snack == null) return;
            snack.transform.position = FreePosition(); snack.Initialize(this); snack.gameObject.SetActive(true);
        }
        public SeaweedEnemy SpawnEnemy()
        {
            if (enemies.Count(p => p.gameObject.activeSelf) >= Session.settings.maximumEnemies) return null;
            var enemy = enemies.FirstOrDefault(p => !p.gameObject.activeSelf); if (enemy == null) return null;
            var body = enemy.GetComponent<CharacterController>(); body.enabled = false;
            enemy.transform.position = FreePosition(); body.enabled = true;
            enemy.Initialize(this, Session.settings.palette[Random.Range(0, Session.settings.palette.Length)]);
            enemy.gameObject.SetActive(true); return enemy;
        }
        void Update()
        {
            if (Session == null || Session.Phase != "Playing") return;
            if (Time.time > nextSnack) { nextSnack = Time.time + .6f; SpawnSnack(); }
            if (Time.time >= nextEnemy) { nextEnemy = Time.time + Random.Range(7f, 11f); SpawnEnemy(); }
        }
        public void ClearEnemies() { foreach (var enemy in enemies) enemy.gameObject.SetActive(false); }
        public void ClearSnacks() { foreach (var snack in snacks) snack.gameObject.SetActive(false); }
        public void ClearProjectiles() { foreach (var shot in shots) shot.gameObject.SetActive(false); }
        public static void FitModel(GameObject go, float height)
        {
            var rs = go.GetComponentsInChildren<Renderer>(); if (rs.Length == 0) return;
            Bounds bounds = rs[0].bounds; foreach (var r in rs) bounds.Encapsulate(r.bounds);
            if (bounds.size.y < .001f) return;
            go.transform.localScale *= height / bounds.size.y;
            bounds = rs[0].bounds; foreach (var r in rs) bounds.Encapsulate(r.bounds);
            go.transform.position += Vector3.up * (go.transform.position.y - bounds.min.y);
        }
    }
}
