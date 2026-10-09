using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
namespace SeaweedSlayer.Editor
{
    public sealed class SeaweedSceneAuthoring
    {
        SeaweedSettings settings;
        Transform staticRoot;
        const string Prefabs = "Assets/SeaweedSlayer/Generated/Authoring";
        static void Set(Object target, string name, object value)
        {
            target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(target, value);
            EditorUtility.SetDirty(target);
        }
        static Transform Group(string name, Transform parent)
        { var go = new GameObject(name); go.transform.SetParent(parent, false); return go.transform; }
        [MenuItem("Seaweed Slayer/Materialize Editable Hierarchy")]
        public static void Materialize()
        {
            if (EditorApplication.isPlaying) throw new System.InvalidOperationException("Stop Play Mode first.");
            var scene = SceneManager.GetSceneByPath("Assets/Scenes/Game.unity");
            if (!scene.IsValid() || !scene.isLoaded) scene = EditorSceneManager.OpenScene("Assets/Scenes/Game.unity");
            var session = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<SeaweedSession>(true)).Single();
            if (scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<SeaweedArena>(true)).Any())
            { ScatterArenaGrass(); Validate(); Debug.Log("SEAWEED_AUTHORING_ALREADY_COMPLETE"); return; }
            new SeaweedSceneAuthoring { settings = session.settings }.Bake(session);
            EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene); AssetDatabase.SaveAssets();
            Validate(); Debug.Log("SEAWEED_AUTHORING_SUCCESS");
        }
        public static void Validate()
        {
            var scene = SceneManager.GetSceneByPath("Assets/Scenes/Game.unity");
            if (!scene.IsValid() || !scene.isLoaded) scene = EditorSceneManager.OpenScene("Assets/Scenes/Game.unity");
            var session = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<SeaweedSession>(true)).Single();
            var serialized = new SerializedObject(session);
            foreach (var name in new[] { "arena", "pcUI", "controllerUI" })
                if (serialized.FindProperty(name).objectReferenceValue == null) throw new System.Exception("Missing authored " + name + ". Run Materialize Editable Hierarchy.");
            var arena = (SeaweedArena)serialized.FindProperty("arena").objectReferenceValue;
            serialized = new SerializedObject(arena);
            foreach (var name in new[] { "players", "snacks", "shots", "enemies", "trees", "grass", "fixedObstacles" })
            {
                var array = serialized.FindProperty(name);
                if (array.arraySize == 0) throw new System.Exception("Empty authored pool: " + name);
                for (int i = 0; i < array.arraySize; i++) if (array.GetArrayElementAtIndex(i).objectReferenceValue == null) throw new System.Exception("Missing authored reference: " + name);
            }
            Debug.Log("SEAWEED_AUTHORING_VALIDATE_SUCCESS");
        }
        void Bake(SeaweedSession session)
        {
            System.IO.Directory.CreateDirectory(Prefabs); AssetDatabase.Refresh();
            var world = Group("Arena", null); var arena = world.gameObject.AddComponent<SeaweedArena>();
            staticRoot = Group("Environment", world); var border = Group("Forest border", staticRoot);
            if (settings.groundPrefab != null)
            {
                for (int i = -16; i <= 16; i += 4)
                {
                    var patch = Object.Instantiate(settings.groundPrefab, new Vector3(i, -.22f, 14), Quaternion.identity);
                    patch.transform.SetParent(border); patch.name = "Forest Pack border ground";
                    var rs = patch.GetComponentsInChildren<Renderer>();
                    if (rs.Length > 0) { var b = rs[0].bounds; foreach (var r in rs) b.Encapsulate(r.bounds); patch.transform.localScale *= 4 / Mathf.Max(b.size.x, b.size.z); }
                }
            }
            for (int x = -17; x < 17; x += 2)
                for (int z = -11; z < 11; z += 2)
                    Block("Green checker tile", new Vector3(x + 1, -.15f, z + 1), new Vector3(2, .3f, 2), ((x + z) / 2 % 2 == 0) ? settings.greenA : settings.greenB);
            for (int x = -17; x <= 17; x += 2) { Fence(new Vector3(x, 0, -11.5f), false); Fence(new Vector3(x, 0, 11.5f), false); }
            for (int z = -9; z <= 9; z += 2) { Fence(new Vector3(-17.5f, 0, z), true); Fence(new Vector3(17.5f, 0, z), true); }

            var forest = Group("Trees - randomize each round", world);
            var trees = new Transform[9];
            for (int i = 0; i < trees.Length; i++)
            {
                var go = Object.Instantiate(settings.forestTrees[i % settings.forestTrees.Length], forest);
                go.name = "Tree " + (i + 1); go.transform.position = new Vector3(-13 + (i % 3) * 13, 0, -7 + (i / 3) * 7);
                SeaweedArena.FitModel(go, 2.7f);
                foreach (var collider in go.GetComponentsInChildren<Collider>()) Object.DestroyImmediate(collider);
                var trunk = go.AddComponent<CapsuleCollider>(); trunk.height = 2 / go.transform.localScale.y;
                trunk.radius = .7f / go.transform.localScale.x; trunk.center = Vector3.up / go.transform.localScale.y;
                trees[i] = go.transform;
            }
            var grass = PlaceGrass(staticRoot);
            var ponds = Group("Water ponds", staticRoot); var obstacles = new Transform[2];
            for (int i = 0; i < 2; i++)
            {
                var pos = new Vector3(i == 0 ? -11 : 11, 0, i == 0 ? 6 : -6);
                var pool = Block("Water pond " + (i + 1), pos + Vector3.up * .02f, new Vector3(2.6f, .08f, 2.6f), settings.water, ponds);
                pool.GetComponent<BoxCollider>().size = new Vector3(1, 25, 1); obstacles[i] = pool.transform;
                if (settings.waterPrefab != null) { var water = Object.Instantiate(settings.waterPrefab, pool.transform); water.name = "Forest water"; water.transform.position = pos + Vector3.up * .1f; water.transform.localScale = Vector3.one; }
            }
            var playerPrefab = BakePlayer(); var snackPrefab = BakeSnack(); var enemyPrefab = BakeEnemy(); var cubePrefab = BakeCube();
            Set(arena, "players", Pool<SeaweedAvatar>(playerPrefab, 20, "Players (20 slots)", world));
            Set(arena, "snacks", Pool<SeaweedSnack>(snackPrefab, 35, "Snacks (35 slots)", world));
            Set(arena, "enemies", Pool<SeaweedEnemy>(enemyPrefab, 3, "Enemies (3 slots)", world));
            Set(arena, "shots", Pool<SeaweedCube>(cubePrefab, 64, "Cubes (64 slots)", world));
            Set(arena, "trees", trees); Set(arena, "grass", grass); Set(arena, "fixedObstacles", obstacles); Set(session, "arena", arena);
            var ui = new SeaweedUIAuthoring(); Set(session, "pcUI", ui.Bake(session, false));
            var mobile = new SeaweedUIAuthoring().Bake(session, true); mobile.gameObject.SetActive(false); Set(session, "controllerUI", mobile);
            if (Object.FindFirstObjectByType<EventSystem>() == null) new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
            // Bake the previous play view once only when the original camera still has Unity's defaults.
            var camera = Camera.main;
            if (camera != null && camera.transform.position == new Vector3(0, 1, -10) && camera.transform.rotation == Quaternion.identity && !camera.orthographic)
            {
                camera.transform.SetPositionAndRotation(new Vector3(0, 29, -19), Quaternion.Euler(57, 0, 0));
                camera.orthographic = true; camera.orthographicSize = 17.5f;
                camera.backgroundColor = new Color32(20, 42, 39, 255); camera.clearFlags = CameraClearFlags.SolidColor;
                camera.nearClipPlane = .1f; camera.farClipPlane = 150; EditorUtility.SetDirty(camera);
            }
        }
        static T[] Pool<T>(GameObject prefab, int count, string name, Transform parent) where T : Component
        {
            var root = Group(name, parent); var result = new T[count];
            for (int i = 0; i < count; i++)
            {
                var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent.gameObject.scene);
                instance.transform.SetParent(root, false); instance.name = prefab.name + " " + (i + 1).ToString("00");
                instance.SetActive(false); result[i] = instance.GetComponent<T>();
            }
            return result;
        }
        GameObject Save(GameObject go, string name)
        {
            try { go.name = name; return PrefabUtility.SaveAsPrefabAsset(go, Prefabs + "/" + name + ".prefab"); }
            finally { Object.DestroyImmediate(go); }
        }
        GameObject BakePlayer()
        {
            var gameObject = new GameObject("Player"); var transform = gameObject.transform;
            var component = gameObject.AddComponent<SeaweedAvatar>();
            var body = gameObject.AddComponent<CharacterController>(); body.height = 1.65f; body.radius = .36f;
            body.center = Vector3.up * .85f; body.stepOffset = .2f;
            GameObject model;
            if (settings.playerModel != null) { model = Object.Instantiate(settings.playerModel, transform); SeaweedArena.FitModel(model, 1.8f); }
            else { model = GameObject.CreatePrimitive(PrimitiveType.Capsule); model.transform.SetParent(transform); model.transform.localPosition = Vector3.up * .9f; model.transform.localScale = new Vector3(.6f, .9f, .6f); Object.DestroyImmediate(model.GetComponent<Collider>()); model.GetComponent<Renderer>().sharedMaterial = settings.player; }
            var visual = model.transform; var renderers = model.GetComponentsInChildren<Renderer>();
            var animationPlayer = model.GetComponentInChildren<Animation>();
            if (animationPlayer != null) foreach (AnimationState clip in animationPlayer) clip.wrapMode = clip.name.StartsWith("Throw") ? WrapMode.Once : WrapMode.Loop;
            var ring = new GameObject("Selected color outline"); ring.transform.SetParent(transform, false);
            ring.transform.localPosition = Vector3.up * .07f; ring.transform.localRotation = Quaternion.Euler(90, 0, 0);
            var outline = ring.AddComponent<SpriteRenderer>(); outline.sprite = settings.playerOutline;
            if (outline.sprite != null) ring.transform.localScale = Vector3.one * (1.5f / outline.sprite.bounds.size.x);
            var canvas = new GameObject("Player name", typeof(RectTransform), typeof(Canvas));
            canvas.transform.SetParent(transform, false); var nameplate = canvas.transform;
            canvas.GetComponent<Canvas>().renderMode = RenderMode.WorldSpace;
            var rect = canvas.GetComponent<RectTransform>(); rect.sizeDelta = new Vector2(240, 48); rect.localScale = Vector3.one * .023f;
            var label = SeaweedUIAuthoring.MakeText(canvas.transform, "Name", settings.uiFont, 28);
            label.resizeTextForBestFit = true; label.resizeTextMinSize = 12; label.resizeTextMaxSize = 28;
            label.gameObject.AddComponent<Outline>().effectColor = new Color(0, 0, 0, .8f);

            nameplate.localPosition = Vector3.up * 2.3f; label.text = "Player";
            Set(component, "body", body); Set(component, "visual", visual); Set(component, "renderers", renderers);
            Set(component, "nameplate", nameplate); Set(component, "label", label); Set(component, "outline", outline); Set(component, "animationPlayer", animationPlayer);
            return Save(gameObject, "Arena Player");
        }
        GameObject BakeEnemy()
        {
            var gameObject = new GameObject("Enemy"); var transform = gameObject.transform;
            var component = gameObject.AddComponent<SeaweedEnemy>();
            var body = gameObject.AddComponent<CharacterController>();
            body.height = 1.5f; body.radius = .38f; body.center = Vector3.up * .78f; body.stepOffset = .2f;
            var model = Object.Instantiate(settings.enemyModel, transform);
            SeaweedArena.FitModel(model, 1.65f); var visual = model.transform;
            foreach (var collider in model.GetComponentsInChildren<Collider>()) Object.DestroyImmediate(collider);
            foreach (var renderer in model.GetComponentsInChildren<Renderer>())
            {
                var tint = new MaterialPropertyBlock(); renderer.GetPropertyBlock(tint);
                tint.SetColor("_BaseColor", Color.white); tint.SetColor("_Color", Color.white);
                tint.SetColor("baseColorFactor", Color.white); renderer.SetPropertyBlock(tint);
            }
            var animationPlayer = model.GetComponentInChildren<Animation>();
            string runClip = null;
            if (animationPlayer != null)
            {
                foreach (AnimationState clip in animationPlayer)
                    if (clip.name.ToLowerInvariant().Contains("run") || clip.name.ToLowerInvariant().Contains("walk")) { runClip = clip.name; break; }
                if (runClip != null) animationPlayer.Play(runClip);
            }
            var ring = new GameObject("Enemy color ring"); ring.transform.SetParent(transform, false);
            ring.transform.localPosition = Vector3.up * .08f; ring.transform.localRotation = Quaternion.Euler(90, 0, 0);
            var sprite = ring.AddComponent<SpriteRenderer>(); sprite.sprite = settings.playerOutline; sprite.color = Color.white;
            if (sprite.sprite != null) ring.transform.localScale = Vector3.one * (1.4f / sprite.sprite.bounds.size.x);

            Set(component, "body", body); Set(component, "visual", visual); Set(component, "animationPlayer", animationPlayer);
            Set(component, "outline", sprite); Set(component, "renderers", model.GetComponentsInChildren<Renderer>());
            return Save(gameObject, "Arena Enemy");
        }
        GameObject BakeSnack()
        {
            var go = new GameObject("Snack"); var component = go.AddComponent<SeaweedSnack>();
            var billboard = Group("item.png billboard", go.transform); billboard.localPosition = Vector3.up * .65f;
            var sprite = billboard.gameObject.AddComponent<SpriteRenderer>(); sprite.sprite = settings.snack;
            billboard.localScale = Vector3.one * (1.15f / sprite.sprite.bounds.size.y);
            Set(component, "billboard", billboard); return Save(go, "Arena Snack");
        }
        GameObject BakeCube()
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube); go.transform.localScale = Vector3.one * .24f;
            go.GetComponent<Renderer>().sharedMaterial = settings.projectile;
            Object.DestroyImmediate(go.GetComponent<Collider>()); go.AddComponent<SeaweedCube>(); return Save(go, "Arena Cube");
        }
        GameObject Block(string name, Vector3 pos, Vector3 size, Material material, Transform parent = null)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube); go.name = name;
            go.transform.SetParent(parent != null ? parent : staticRoot, false); go.transform.position = pos; go.transform.localScale = size;
            go.GetComponent<Renderer>().sharedMaterial = material; return go;
        }
        void Fence(Vector3 position, bool side)
        {
            var s = settings;
            Block("Fence post", position + Vector3.up * .65f, new Vector3(.22f, 1.3f, .22f), s.wood);
            Block("Arena boundary", position + Vector3.up * .5f, side ? new Vector3(.25f, 1, 2) : new Vector3(2, 1, .25f), s.wood);
            Block("Fence top rail", position + Vector3.up * 1.25f, side ? new Vector3(.3f, .16f, 2) : new Vector3(2, .16f, .3f), s.wood);
        }
        [MenuItem("Seaweed Slayer/Scatter Arena Grass")]
        public static void ScatterArenaGrass()
        {
            if (EditorApplication.isPlaying) throw new System.InvalidOperationException("Stop Play Mode first.");
            var scene = SceneManager.GetSceneByPath("Assets/Scenes/Game.unity");
            if (!scene.IsValid() || !scene.isLoaded) scene = EditorSceneManager.OpenScene("Assets/Scenes/Game.unity");
            var session = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<SeaweedSession>(true)).Single();
            var arena = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<SeaweedArena>(true)).Single();
            var environment = arena.transform.Find("Environment");
            if (environment == null) throw new System.Exception("Missing Arena / Environment.");
            var existing = environment.Find("Grass");
            if (existing != null && existing.childCount > 0)
            {
                var patches = new Transform[existing.childCount];
                for (int i = 0; i < existing.childCount; i++) patches[i] = existing.GetChild(i);
                var authored = new SerializedObject(arena).FindProperty("grass");
                if (authored.arraySize == 0) { Set(arena, "grass", patches); EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene); }
                Debug.Log("SEAWEED_GRASS_ALREADY_COMPLETE"); return;
            }
            if (existing != null) Object.DestroyImmediate(existing.gameObject);
            var authoring = new SeaweedSceneAuthoring { settings = session.settings };
            authoring.EnsureGrassPrefab();
            Set(arena, "grass", authoring.PlaceGrass(environment));
            EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene); AssetDatabase.SaveAssets();
            Debug.Log("SEAWEED_GRASS_SUCCESS");
        }
        void EnsureGrassPrefab()
        {
            if (settings.grassPrefab != null) return;
            settings.grassPrefab = SeaweedProject.BakePrefab("Assets/Resources/level/AurynSky/Forest Pack/Prefabs/ForestGrass02.prefab", "ForestGrass02");
            EditorUtility.SetDirty(settings);
        }
        Transform[] PlaceGrass(Transform parent)
        {
            EnsureGrassPrefab();
            if (settings.grassPrefab == null) throw new System.Exception("Missing ForestGrass02 prefab.");
            var root = Group("Grass", parent);
            var rng = new System.Random(7);
            var used = new System.Collections.Generic.List<Vector3> { new Vector3(-11f, 0, 6f), new Vector3(11f, 0, -6f) };
            var patches = new Transform[12];
            for (int i = 0; i < patches.Length; i++)
            {
                var pos = Vector3.zero;
                for (int attempt = 0; attempt < 200; attempt++)
                {
                    pos = new Vector3((float)(rng.NextDouble() * 28 - 14), 0, (float)(rng.NextDouble() * 16 - 8));
                    bool free = true;
                    foreach (var other in used) if ((other - pos).sqrMagnitude < 30.25f) { free = false; break; }
                    if (free) break;
                }
                used.Add(pos);
                var go = Object.Instantiate(settings.grassPrefab, root);
                go.name = "ForestGrass02 " + (i + 1).ToString("00");
                foreach (var collider in go.GetComponentsInChildren<Collider>()) Object.DestroyImmediate(collider);
                go.transform.SetPositionAndRotation(pos, Quaternion.Euler(0, (float)rng.NextDouble() * 360f, 0));
                SeaweedArena.FitModel(go, .45f + (float)rng.NextDouble() * .25f);
                foreach (var renderer in go.GetComponentsInChildren<Renderer>()) renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                patches[i] = go.transform;
            }
            return patches;
        }
    }
}
