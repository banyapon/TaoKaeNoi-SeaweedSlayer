using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using ExitGames.Client.Photon;
using Photon.Pun;
using Photon.Realtime;
using UnityEngine;
using Hashtable = ExitGames.Client.Photon.Hashtable;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.UI;

namespace SeaweedSlayer
{
    // Opt-in 15-player match. Starts only when Captures/playtest-15.running exists.
    public sealed class SeaweedPlaytest15 : MonoBehaviour
    {
        const int Players = 15;
        const byte MoveCode = 31, ThrowCode = 32;
        static readonly string[] Names = {
            "Mali", "Ploy", "Korn", "Mint", "Bank", "Sky", "Beam", "Fern",
            "Ice", "Pim", "Win", "June", "Earth", "Fah", "Nut"
        };
        readonly List<LoadBalancingClient> clients = new List<LoadBalancingClient>();
        readonly HashSet<LoadBalancingClient> joining = new HashSet<LoadBalancingClient>();
        readonly List<Bot> bots = new List<Bot>();
        SeaweedSession session;
        string folder;
        float deadline;
        float nextDrive;
        bool finished;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Boot()
        {
            if (Application.isEditor && File.Exists(RunningPath()))
                new GameObject("Playtest 15").AddComponent<SeaweedPlaytest15>();
        }

        static string ProjectRoot() => Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
        static string RunningPath() => Path.Combine(ProjectRoot(), "Captures", "playtest-15.running");

        void Update()
        {
            foreach (var client in clients)
            {
                client.Service();
                if (session != null && client.State == ClientState.ConnectedToMasterServer && joining.Add(client))
                    client.OpJoinRoom(new EnterRoomParams { RoomName = session.RoomCode });
            }
            if (!finished && session != null && session.Phase == "Playing" && Time.time >= nextDrive)
            {
                nextDrive = Time.time + .08f;
                Drive();
            }
            if (!finished && deadline > 0 && Time.realtimeSinceStartup > deadline)
                Finish("fail: timed out in phase " + (session != null ? session.Phase : "none"));
        }

        IEnumerator Start()
        {
            folder = Path.Combine(ProjectRoot(), "Captures", "playtest-15");
            Directory.CreateDirectory(folder);
            foreach (var old in Directory.GetFiles(folder, "*.png")) File.Delete(old);
            deadline = Time.realtimeSinceStartup + 220f;
            session = FindAnyObjectByType<SeaweedSession>();
            Log("waiting for the PC room");
            float connectedBy = Time.realtimeSinceStartup + 40f;
            while (session == null || !session.Connected || string.IsNullOrEmpty(session.RoomCode))
            {
                if (Time.realtimeSinceStartup > connectedBy) { Finish("fail: PC room did not open"); yield break; }
                yield return null;
            }
            yield return Capture("01-lobby.png");
            var app = PhotonNetwork.PhotonServerSettings.AppSettings.CopyTo(new AppSettings());
            app.FixedRegion = "asia";
            app.Protocol = ConnectionProtocol.WebSocketSecure;
            app.AppVersion = PhotonNetwork.AppVersion;
            for (int i = 0; i < Players; i++)
            {
                var client = new LoadBalancingClient(ConnectionProtocol.WebSocketSecure);
                client.AuthValues = new AuthenticationValues("playtest15-" + Guid.NewGuid().ToString("N"));
                client.NickName = Names[i];
                clients.Add(client);
                bots.Add(new Bot(client));
                if (!client.ConnectUsingSettings(app)) { Finish("fail: connect " + Names[i]); yield break; }
                yield return new WaitForSeconds(.12f);
            }
            float joinedBy = Time.realtimeSinceStartup + 50f;
            while (clients.Any(c => !c.InRoom))
            {
                if (Time.realtimeSinceStartup > joinedBy)
                {
                    Finish("fail: joined " + clients.Count(c => c.InRoom) + "/" + Players);
                    yield break;
                }
                yield return null;
            }
            int round = PhotonNetwork.CurrentRoom.CustomProperties["round"] is int value ? value : 0;
            for (int i = 0; i < clients.Count; i++)
                clients[i].LocalPlayer.SetCustomProperties(new Hashtable {
                    { "registered", true }, { "color", i % session.settings.palette.Length }, { "ready", false }
                });
            float rosterBy = Time.realtimeSinceStartup + 20f;
            while (session.Players.Count < Players)
            {
                if (Time.realtimeSinceStartup > rosterBy) { Finish("fail: roster " + session.Players.Count + "/" + Players); yield break; }
                yield return null;
            }
            yield return new WaitForSeconds(.4f);
            yield return Capture("02-fifteen-joined.png");
            for (int i = 0; i < clients.Count; i++)
                clients[i].LocalPlayer.SetCustomProperties(new Hashtable { { "ready", true }, { "readyRound", round } });
            float countdownBy = Time.realtimeSinceStartup + 15f;
            while (session.Phase != "Countdown")
            {
                if (Time.realtimeSinceStartup > countdownBy) { Finish("fail: countdown did not start"); yield break; }
                yield return null;
            }
            yield return Capture("03-countdown.png");
            while (session.Phase == "Countdown" && session.Remaining > 3.2f) yield return null;
            if (session.Phase == "Countdown") yield return Capture("04-countdown-3.png");
            float playingBy = Time.realtimeSinceStartup + 20f;
            while (session.Phase != "Playing")
            {
                if (Time.realtimeSinceStartup > playingBy) { Finish("fail: round did not start"); yield break; }
                yield return null;
            }
            yield return new WaitForSeconds(.35f);
            yield return Capture("05-start.png");
            float[] marks = { 75f, 60f, 45f, 30f, 15f, 5f };
            string[] files = { "06-play-75s.png", "07-play-60s.png", "08-play-45s.png", "09-play-30s.png", "10-play-15s.png", "11-play-5s.png" };
            for (int i = 0; i < marks.Length; i++)
            {
                while (session.Phase == "Playing" && session.Remaining > marks[i]) yield return null;
                if (session.Phase != "Playing") break;
                yield return Capture(files[i]);
            }
            float resultsBy = Time.realtimeSinceStartup + 30f;
            while (session.Phase != "Results")
            {
                if (Time.realtimeSinceStartup > resultsBy) { Finish("fail: results did not open"); yield break; }
                yield return null;
            }
            yield return new WaitForSeconds(.6f);
            yield return Capture("12-results.png");
            var scores = session.Players.Values.OrderByDescending(p => p.Score).ThenBy(p => p.DisplayName);
            Log("results " + string.Join(", ", scores.Select(p => p.DisplayName + "=" + p.Score)));
            Finish("ok");
        }

        void Drive()
        {
            var snacks = FindObjectsByType<SeaweedSnack>(FindObjectsInactive.Exclude);
            var enemies = FindObjectsByType<SeaweedEnemy>(FindObjectsInactive.Exclude);
            foreach (var bot in bots)
            {
                if (bot.Client.LocalPlayer == null || !session.Players.TryGetValue(bot.Client.LocalPlayer.ActorNumber, out var avatar) || !avatar.gameObject.activeSelf)
                    continue;
                Vector3 origin = avatar.transform.position;
                Vector3 goal = bot.Goal;
                float bestSnack = 16f * 16f;
                foreach (var snack in snacks)
                {
                    if (!snack.gameObject.activeInHierarchy) continue;
                    float distance = (snack.transform.position - origin).sqrMagnitude;
                    if (distance < bestSnack) { bestSnack = distance; goal = snack.transform.position; }
                }
                if (bestSnack == 16f * 16f && (Time.time >= bot.NextGoal || (goal - origin).sqrMagnitude < 2f))
                {
                    bot.Goal = new Vector3(UnityEngine.Random.Range(-14f, 14f), 0f, UnityEngine.Random.Range(-8f, 8f));
                    bot.NextGoal = Time.time + UnityEngine.Random.Range(1.5f, 3f);
                    goal = bot.Goal;
                }
                Vector3 delta = goal - origin;
                delta.y = 0f;
                foreach (var other in session.Players.Values)
                {
                    if (other == avatar || !other.gameObject.activeSelf) continue;
                    Vector3 away = origin - other.transform.position;
                    away.y = 0f;
                    if (away.sqrMagnitude > .01f && away.sqrMagnitude < 3f) delta += away.normalized * (1.8f - away.magnitude);
                }
                Vector2 move = delta.sqrMagnitude < .25f ? Vector2.zero : Vector2.ClampMagnitude(new Vector2(delta.x, delta.z), 1f);
                var options = new RaiseEventOptions { Receivers = ReceiverGroup.MasterClient };
                bot.Client.OpRaiseEvent(MoveCode, new object[] { move.x, move.y }, options, SendOptions.SendUnreliable);
                if (Time.time < bot.NextThrow) continue;
                Vector3 aim = Vector3.zero;
                float best = 11f * 11f;
                foreach (var enemy in enemies)
                {
                    if (!enemy.gameObject.activeInHierarchy) continue;
                    float distance = (enemy.transform.position - origin).sqrMagnitude;
                    if (distance < best && distance > .3f) { best = distance; aim = enemy.transform.position - origin; }
                }
                if (aim == Vector3.zero)
                {
                    best = 8f * 8f;
                    foreach (var other in session.Players.Values)
                    {
                        if (other == avatar || !other.gameObject.activeSelf) continue;
                        float distance = (other.transform.position - origin).sqrMagnitude;
                        if (distance < best && distance > 1.4f) { best = distance; aim = other.transform.position - origin; }
                    }
                }
                aim.y = 0f;
                if (aim.sqrMagnitude < .01f) continue;
                aim.Normalize();
                bot.Client.OpRaiseEvent(ThrowCode, new object[] { aim.x, aim.z }, options, SendOptions.SendReliable);
                bot.NextThrow = Time.time + UnityEngine.Random.Range(.5f, 1.1f);
            }
        }

        IEnumerator Capture(string fileName)
        {
            var camera = Camera.main;
            var canvas = session.UI.GetComponent<Canvas>();
            var target = new RenderTexture(1920, 1080, 24);
            var previousTarget = camera.targetTexture;
            var previousRect = camera.rect;
            var previousMode = canvas.renderMode;
            var previousCamera = canvas.worldCamera;
            var previousPlane = canvas.planeDistance;
            camera.targetTexture = target;
            camera.rect = new Rect(0f, 0f, 1f, 1f);
            canvas.renderMode = RenderMode.ScreenSpaceCamera;
            canvas.worldCamera = camera;
            canvas.planeDistance = 2f;
            session.UI.Refresh();
            yield return null;
            Canvas.ForceUpdateCanvases();
            RenderPipeline.SubmitRenderRequest(camera, new UniversalRenderPipeline.SingleCameraRequest { destination = target });
            RenderTexture.active = target;
            var image = new Texture2D(1920, 1080, TextureFormat.RGB24, false);
            image.ReadPixels(new Rect(0f, 0f, 1920f, 1080f), 0, 0);
            image.Apply();
            File.WriteAllBytes(Path.Combine(folder, fileName), image.EncodeToPNG());
            RenderTexture.active = null;
            camera.targetTexture = previousTarget;
            camera.rect = previousRect;
            canvas.renderMode = previousMode;
            canvas.worldCamera = previousCamera;
            canvas.planeDistance = previousPlane;
            Destroy(image);
            target.Release();
            Destroy(target);
            Log("shot " + fileName + " phase=" + session.Phase + " players=" + session.Players.Count + " remaining=" + session.Remaining.ToString("0.0"));
        }

        void Finish(string status)
        {
            if (finished) return;
            finished = true;
            foreach (var client in clients) client.Disconnect();
            try { if (File.Exists(RunningPath())) File.Delete(RunningPath()); } catch (Exception ex) { Debug.LogException(ex); }
            File.WriteAllText(Path.Combine(ProjectRoot(), "Captures", "playtest-15.done"), status);
            Log(status);
        }

        void Log(string message)
        {
            string line = DateTime.Now.ToString("HH:mm:ss") + " " + message;
            Debug.Log("PLAYTEST15 " + message);
            File.AppendAllText(Path.Combine(folder, "log.txt"), line + Environment.NewLine);
        }

        sealed class Bot
        {
            public LoadBalancingClient Client;
            public Vector3 Goal;
            public float NextGoal;
            public float NextThrow;
            public Bot(LoadBalancingClient client) { Client = client; }
        }
    }
}
