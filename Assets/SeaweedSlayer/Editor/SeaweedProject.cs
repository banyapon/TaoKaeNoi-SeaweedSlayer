using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace SeaweedSlayer.Editor
{
    public static class SeaweedProject
    {
        const string ScenePath = "Assets/Scenes/Game.unity";
        const string Generated = "Assets/SeaweedSlayer/Generated";
        const string SettingsPath = "Assets/SeaweedSlayer/SeaweedSettings.asset";
        static readonly Dictionary<Material, Material> converted = new Dictionary<Material, Material>();
        [MenuItem("Seaweed Slayer/Prepare Game Scene")]
        public static void Prepare()
        {
            if (EditorApplication.isPlaying) throw new InvalidOperationException("Stop Play Mode before preparing the scene.");
            Directory.CreateDirectory(Generated); AssetDatabase.Refresh();
            var s = AssetDatabase.LoadAssetAtPath<SeaweedSettings>(SettingsPath);
            if (s == null) { s = ScriptableObject.CreateInstance<SeaweedSettings>(); AssetDatabase.CreateAsset(s, SettingsPath); }
            s.titleFont = AssetDatabase.LoadAssetAtPath<Font>("Assets/Resources/fonts/upheavtt.ttf");
            s.uiFont = AssetDatabase.LoadAssetAtPath<Font>("Assets/Resources/fonts/KANIT-SEMIBOLD.TTF");
            s.playerOutline = ImportSprite("Assets/Joystick Pack/Sprites/Handles/Handle_Outline.png");
            s.snack = ImportSprite("Assets/Resources/sprites/item.png");
            ConfigureContent(s);
            s.joystickPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Joystick Pack/Prefabs/Fixed Joystick.prefab");
            s.greenA = Material("Grass A", new Color32(101, 151, 57, 255));
            s.greenB = Material("Grass B", new Color32(120, 173, 68, 255));
            s.wood = Material("Fence", new Color32(146, 101, 57, 255));
            s.water = Material("Pond", new Color32(50, 174, 209, 255));
            s.player = Material("Player", new Color32(235, 244, 219, 255));
            s.projectile = Material("Grey cube", new Color32(151, 156, 160, 255));
            string forest = "Assets/Resources/level/AurynSky/Forest Pack/Prefabs/";
            s.forestTrees = new[] { "ForestTreePineShort", "ForestTreeAppleShort", "ForestTreeDShort" }.Select(n => BakePrefab(forest + n + ".prefab", n)).ToArray();
            s.waterPrefab = BakePrefab(forest + "WaterQuadSolo.prefab", "Forest Water");
            s.groundPrefab = BakePrefab(forest + "ForestGround01.prefab", "Forest Ground");
            s.grassPrefab = BakePrefab(forest + "ForestGrass02.prefab", "ForestGrass02");
            s.playerModel = BakePrefab("Assets/Resources/characters/player.glb", "Player Model");
            var endpoint = Environment.GetEnvironmentVariable("SEAWEED_CONTROLLER_URL");
            if (!string.IsNullOrEmpty(endpoint)) s.controllerUrl = endpoint;
            EditorUtility.SetDirty(s); AssetDatabase.SaveAssets();
            var scene = SceneManager.GetSceneByPath(ScenePath);
            if (!scene.IsValid() || !scene.isLoaded) scene = EditorSceneManager.OpenScene(ScenePath);
            var sessions = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<SeaweedSession>(true)).ToArray();
            var session = sessions.FirstOrDefault();
            if (session == null) { var root = new GameObject("Seaweed Slayer / PC + mobile controller"); SceneManager.MoveGameObjectToScene(root, scene); session = root.AddComponent<SeaweedSession>(); }
            session.settings = s; EditorUtility.SetDirty(session);
            EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene);
            SeaweedSceneAuthoring.Materialize();
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
            PlayerSettings.productName = "TaoKaeNoi Seaweed Slayer"; PlayerSettings.runInBackground = true;
            Validate(); Debug.Log("SEAWEED_PREPARE_SUCCESS");
        }
        public static void ConfigureUpdates()
        {
            Directory.CreateDirectory(Generated); AssetDatabase.Refresh();
            var s = AssetDatabase.LoadAssetAtPath<SeaweedSettings>(SettingsPath);
            ConfigureContent(s); EditorUtility.SetDirty(s); AssetDatabase.SaveAssets(); Validate();
        }
        static void ConfigureContent(SeaweedSettings s)
        {
            s.controllerUrl = "https://taokaenoiarena.vercel.app/Controller";
            s.logo = ImportSprite("Assets/Resources/sprites/logo.png");
            s.enemyModel = BakePrefab("Assets/Resources/characters/enemy.glb", "Enemy Model");
            s.maximumEnemies = 3; s.enemyScore = 50;
        }
        static Sprite ImportSprite(string path)
        {
            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer != null && (importer.textureType != TextureImporterType.Sprite || importer.spriteImportMode != SpriteImportMode.Single))
            { importer.textureType = TextureImporterType.Sprite; importer.spriteImportMode = SpriteImportMode.Single; importer.SaveAndReimport(); }
            return AssetDatabase.LoadAssetAtPath<Sprite>(path);
        }
        static Material Material(string name, Color color)
        {
            string path = Generated + "/" + name + ".mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null) { material = new Material(Shader.Find("Universal Render Pipeline/Lit")); AssetDatabase.CreateAsset(material, path); }
            material.color = color; EditorUtility.SetDirty(material); return material;
        }
        public static GameObject BakePrefab(string source, string name)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(source);
            if (prefab == null) throw new Exception("Missing model/prefab: " + source);
            var go = UnityEngine.Object.Instantiate(prefab); go.name = name;
            try
            {
                foreach (var r in go.GetComponentsInChildren<Renderer>(true))
                {
                    var mats = r.sharedMaterials;
                    for (int i = 0; i < mats.Length; i++)
                    {
                        var original = mats[i]; if (original == null || source.EndsWith(".glb", StringComparison.OrdinalIgnoreCase) || original.shader.name.StartsWith("Universal Render Pipeline/")) continue;
                        if (!converted.TryGetValue(original, out var replacement))
                        {
                            AssetDatabase.TryGetGUIDAndLocalFileIdentifier(original, out string guid, out long localId);
                            string id = guid + "_" + localId;
                            string path = Generated + "/Forest Material " + id + ".mat";
                            replacement = AssetDatabase.LoadAssetAtPath<Material>(path);
                            if (replacement == null) { replacement = new Material(Shader.Find("Universal Render Pipeline/Lit")); AssetDatabase.CreateAsset(replacement, path); }
                            if (original.HasProperty("_MainTex")) replacement.SetTexture("_BaseMap", original.GetTexture("_MainTex"));
                            if (original.HasProperty("_Color")) replacement.SetColor("_BaseColor", original.GetColor("_Color"));
                            replacement.SetFloat("_Smoothness", .1f); EditorUtility.SetDirty(replacement); converted[original] = replacement;
                        }
                        mats[i] = replacement;
                    }
                    r.sharedMaterials = mats;
                }
                if (source.EndsWith(".glb", StringComparison.OrdinalIgnoreCase)) ApplyPlayerAnimations(go, source);
                return PrefabUtility.SaveAsPrefabAsset(go, Generated + "/" + name + ".prefab");
            }
            finally { UnityEngine.Object.DestroyImmediate(go); }
        }
        [InitializeOnLoadMethod]
        static void ScheduleAnimationUpgrade()
        {
            if (Application.isBatchMode) return;
            EditorApplication.delayCall += UpgradePlayerAnimations;
            EditorApplication.playModeStateChanged += state => {
                if (state == PlayModeStateChange.EnteredEditMode) EditorApplication.delayCall += UpgradePlayerAnimations;
            };
        }
        [MenuItem("Seaweed Slayer/Upgrade Player Animations")]
        public static void UpgradePlayerAnimations()
        {
            if (EditorApplication.isPlaying || EditorApplication.isCompiling) return;
            string path = Generated + "/Player Model.prefab";
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (prefab == null || prefab.GetComponent<Animation>() != null) return;
            var animator = prefab.GetComponent<Animator>();
            if (animator != null && animator.runtimeAnimatorController != null) return;
            var go = PrefabUtility.LoadPrefabContents(path);
            try { ApplyPlayerAnimations(go, "Assets/Resources/characters/player.glb"); PrefabUtility.SaveAsPrefabAsset(go, path); }
            finally { PrefabUtility.UnloadPrefabContents(go); }
            AssetDatabase.SaveAssets(); Debug.Log("SEAWEED_ANIMATIONS_SUCCESS");
        }
        static void ApplyPlayerAnimations(GameObject go, string source)
        {
            var originals = AssetDatabase.LoadAllAssetsAtPath(source).OfType<AnimationClip>().ToArray();
            if (originals.Length == 0) return;
            foreach (var animator in go.GetComponentsInChildren<Animator>()) UnityEngine.Object.DestroyImmediate(animator);
            var animation = go.GetComponent<Animation>();
            if (animation == null) animation = go.AddComponent<Animation>();
            foreach (var original in originals)
            {
                string safeName = new string(original.name.Select(c => Path.GetInvalidFileNameChars().Contains(c) ? '_' : c).ToArray());
                string path = Generated + (source.Contains("enemy.glb") ? "/Enemy " : "/Player ") + safeName + ".anim";
                var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
                if (clip == null) { clip = UnityEngine.Object.Instantiate(original); AssetDatabase.CreateAsset(clip, path); }
                else EditorUtility.CopySerialized(original, clip);
                clip.name = original.name; clip.legacy = true;
                clip.wrapMode = original.name.StartsWith("Throw") ? WrapMode.Once : WrapMode.Loop;
                EditorUtility.SetDirty(clip); animation.AddClip(clip, original.name);
                if (animation.clip == null || original.name == "Idle_Cute") animation.clip = clip;
            }
            animation.playAutomatically = true;
        }
        [MenuItem("Seaweed Slayer/Validate")]
        public static void Validate()
        {
            var s = AssetDatabase.LoadAssetAtPath<SeaweedSettings>(SettingsPath);
            if (s == null || s.titleFont == null || s.uiFont == null || s.playerOutline == null || s.snack == null || s.logo == null || s.enemyModel == null || s.joystickPrefab == null || s.playerModel == null || s.forestTrees.Any(p => p == null)) throw new Exception("Missing required game assets. Run Prepare Game Scene.");
            if (s.maximumPlayers < 1 || s.maximumPlayers > 20) throw new Exception("Player capacity must be 1..20.");
            var clips = AnimationUtility.GetAnimationClips(s.playerModel);
            if (s.playerModel.GetComponent<Animation>() == null || new[] { "Idle_Cute", "Run_Cute", "Throw_Left" }.Any(name => !clips.Any(c => c.name == name && c.legacy)))
                throw new Exception("Missing player animations. Run Upgrade Player Animations.");
            Debug.Log("SEAWEED_VALIDATE_SUCCESS");
            SeaweedSceneAuthoring.Validate();
        }
        [MenuItem("Seaweed Slayer/Build Windows PC")]
        public static void BuildWindows()
        {
            Validate();
            Build(BuildTarget.StandaloneWindows64, "Builds/Windows/SeaweedSlayer.exe");
        }
        [MenuItem("Seaweed Slayer/Build Mobile WebGL Controller")]
        public static void BuildController()
        {
            Validate(); PlayerSettings.WebGL.template = "PROJECT:SeaweedController";
            PlayerSettings.WebGL.compressionFormat = WebGLCompressionFormat.Gzip;
            PlayerSettings.WebGL.decompressionFallback = true;
            PlayerSettings.WebGL.nameFilesAsHashes = true;
            PlayerSettings.WebGL.initialMemorySize = 64;
            PlayerSettings.defaultWebScreenWidth = 1280; PlayerSettings.defaultWebScreenHeight = 720;
            Build(BuildTarget.WebGL, "Builds/WebGL/Controller", new[] { "SEAWEED_CONTROLLER" });
        }
        [MenuItem("Seaweed Slayer/Build WebGL Arena + Controller + Windows")]
        public static void BuildUpdatedRelease()
        {
            ConfigureUpdates(); BuildArenaWebGL(); BuildController(); PackageWebRelease(); BuildWindows();
        }
        [MenuItem("Seaweed Slayer/Build WebGL Site (Arena + Controller)")]
        public static void BuildWebRelease()
        {
            BuildArenaWebGL(); BuildController(); PackageWebRelease();
        }
        [MenuItem("Seaweed Slayer/Package WebGL Site")]
        public static void PackageWebRelease()
        {
            if (!File.Exists("Builds/WebGL/index.html") || !File.Exists("Builds/WebGL/Controller/index.html"))
                throw new Exception("Build WebGL Arena and Controller first.");
            File.WriteAllText("Builds/WebGL/vercel.json", "{\"trailingSlash\": true}");
            Debug.Log("SEAWEED_WEB_PACKAGE_SUCCESS");
        }
        [MenuItem("Seaweed Slayer/Build WebGL Arena")]
        public static void BuildArenaWebGL()
        {
            Validate(); PlayerSettings.WebGL.template = "PROJECT:SeaweedArena";
            PlayerSettings.WebGL.compressionFormat = WebGLCompressionFormat.Gzip;
            PlayerSettings.WebGL.decompressionFallback = true; PlayerSettings.WebGL.initialMemorySize = 128;
            PlayerSettings.WebGL.nameFilesAsHashes = true;
            PlayerSettings.defaultWebScreenWidth = 1920; PlayerSettings.defaultWebScreenHeight = 1080;
            Build(BuildTarget.WebGL, "Builds/WebGL");
        }
        static void Build(BuildTarget target, string output, string[] defines = null)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(output));
            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions { scenes = new[] { ScenePath }, target = target, locationPathName = output, options = BuildOptions.None, extraScriptingDefines = defines ?? Array.Empty<string>() });
            if (report.summary.result != BuildResult.Succeeded) throw new Exception("Build failed: " + report.summary.result);
            Debug.Log("SEAWEED_BUILD_SUCCESS: " + Path.GetFullPath(output));
        }
        public static void PrepareAndBuildWindows() { Prepare(); BuildWindows(); }
        [MenuItem("Seaweed Slayer/Build Controller and Windows PC")]
        public static void BuildBoth() { BuildController(); BuildWindows(); }
        public static void UpgradeAndBuildBoth() { UpgradePlayerAnimations(); BuildBoth(); }
    }
}
