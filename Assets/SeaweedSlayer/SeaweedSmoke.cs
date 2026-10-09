using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using ExitGames.Client.Photon;
using Photon.Pun;
using Photon.Realtime;
using UnityEngine;

namespace SeaweedSlayer
{
    // Opt-in integration checks for a built Windows player. Never runs during normal play.
    public sealed class SeaweedSmoke : MonoBehaviour
    {
        readonly List<LoadBalancingClient> clients = new List<LoadBalancingClient>();
        readonly HashSet<LoadBalancingClient> joining = new HashSet<LoadBalancingClient>();
        SeaweedSession session;
        float deadline;
        bool finished;
        Vector3 savedCameraPosition;
        Quaternion savedCameraRotation;
        float savedCameraSize;
        bool savedOrthographic;
        Rect savedCameraRect;
        int authoredWorldCount;
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Boot()
        {
            if (Application.platform == RuntimePlatform.WebGLPlayer) return;
            var args = Environment.GetCommandLineArgs();
            if (args.Contains("-seaweed-controller-preview")) FindAnyObjectByType<SeaweedSession>().editorController = true;
            if (args.Any(a => a == "-seaweed-smoke" || a == "-seaweed-preview" || a == "-seaweed-controller-preview" || a == "-seaweed-lobby-preview"))
            {
                var smoke = new GameObject("Integration smoke").AddComponent<SeaweedSmoke>();
                var camera = Camera.main;
                smoke.savedCameraPosition = camera.transform.position; smoke.savedCameraRotation = camera.transform.rotation;
                smoke.savedCameraSize = camera.orthographicSize; smoke.savedOrthographic = camera.orthographic; smoke.savedCameraRect = camera.rect;
                var arena = FindAnyObjectByType<SeaweedArena>();
                smoke.authoredWorldCount = arena.GetComponentsInChildren<Transform>(true).Length;
            }
        }
        void Update()
        {
            foreach (var client in clients)
            {
                client.Service();
                if (client.State == ClientState.ConnectedToMasterServer && joining.Add(client))
                    client.OpJoinRoom(new EnterRoomParams { RoomName = session.RoomCode });
            }
            if (!finished && deadline > 0 && Time.realtimeSinceStartup > deadline) Fail("Timeout waiting for integration state");
        }
        IEnumerator Start()
        {
            deadline = Time.realtimeSinceStartup + 180;
            session = FindAnyObjectByType<SeaweedSession>();
            bool mobilePreview = Environment.GetCommandLineArgs().Contains("-seaweed-controller-preview");
            bool lobbyPreview = Environment.GetCommandLineArgs().Contains("-seaweed-lobby-preview");
            if (Environment.GetCommandLineArgs().Contains("-seaweed-preview") || mobilePreview || lobbyPreview)
            {
                yield return new WaitUntil(() => session.UI != null);
                if (!mobilePreview && !lobbyPreview)
                {
                    session.PlaySolo(); yield return new WaitForSeconds(4);
                    int previewPlayers = Environment.GetCommandLineArgs().Contains("-seaweed-full-roster") ? 19 : 5;
                    for (int i = 1; i <= previewPlayers; i++) session.Players[100 + i] = session.Arena.CreateAvatar(100 + i, "Player " + i, session.settings.palette[i % session.settings.palette.Length]);
                }
                yield return new WaitForSeconds(1);
                session.UI.Refresh();
                string output = System.IO.Path.Combine(Application.dataPath, mobilePreview ? "../ControllerPreview.png" : lobbyPreview ? "../LobbyPreview.png" : "../Preview.png");
                // Render offscreen so the preview also works when the Windows process is hidden.
                int width = mobilePreview ? 720 : 1280, height = mobilePreview ? 1280 : 720;
                var camera = Camera.main; var target = new RenderTexture(width, height, 24);
                camera.targetTexture = target; camera.rect = new Rect(0, 0, 1, 1);
                var canvas = session.UI.GetComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceCamera; canvas.worldCamera = camera; canvas.planeDistance = 2;
                yield return null; Canvas.ForceUpdateCanvases();
                UnityEngine.Rendering.RenderPipeline.SubmitRenderRequest(camera, new UnityEngine.Rendering.Universal.UniversalRenderPipeline.SingleCameraRequest { destination = target });
                RenderTexture.active = target;
                var image = new Texture2D(width, height, TextureFormat.RGB24, false); image.ReadPixels(new Rect(0, 0, width, height), 0, 0); image.Apply();
                System.IO.File.WriteAllBytes(output, image.EncodeToPNG()); RenderTexture.active = null; camera.targetTexture = null;
                Destroy(image); target.Release(); Destroy(target);
                Debug.Log("SEAWEED_PREVIEW_SUCCESS: " + output); finished = true; Application.Quit(0); yield break;
            }
            yield return new WaitUntil(() => session != null && session.Connected);
            CheckCamera();
            Check(PhotonNetwork.CurrentRoom.MaxPlayers == 21, "20 controllers plus PC host capacity");
            // Two actual network peers exercise the protocol; the room capacity is independently asserted.
            for (int i = 0; i < 2; i++)
            {
                var c = new LoadBalancingClient(ConnectionProtocol.WebSocketSecure);
                c.AuthValues = new AuthenticationValues("smoke-" + Guid.NewGuid().ToString("N"));
                c.NickName = "Smoke " + (i + 1);
                var app = PhotonNetwork.PhotonServerSettings.AppSettings.CopyTo(new AppSettings());
                app.FixedRegion = "asia"; app.Protocol = ConnectionProtocol.WebSocketSecure; app.AppVersion = PhotonNetwork.AppVersion;
                clients.Add(c); Check(c.ConnectUsingSettings(app), "connect peer " + i);
            }
            yield return new WaitUntil(() => clients.All(c => c.InRoom));
            foreach (var c in clients) c.LocalPlayer.SetCustomProperties(new ExitGames.Client.Photon.Hashtable { { "registered", true }, { "color", 1 }, { "ready", false } });
            yield return new WaitUntil(() => session.Players.Count == 2);
            Check(session.Phase == "Lobby", "join alone does not hide QR/start");
            clients[0].LocalPlayer.SetCustomProperties(new ExitGames.Client.Photon.Hashtable { { "ready", true } });
            yield return new WaitForSeconds(1);
            Check(session.Phase == "Lobby", "wait for all controllers Ready");
            clients[1].LocalPlayer.SetCustomProperties(new ExitGames.Client.Photon.Hashtable { { "ready", true } });
            yield return new WaitUntil(() => session.Phase == "Countdown");
            clients[1].LocalPlayer.SetCustomProperties(new ExitGames.Client.Photon.Hashtable { { "ready", false } });
            yield return new WaitUntil(() => session.Phase == "Lobby");
            Check(PhotonNetwork.CurrentRoom.IsOpen, "cancel Ready reopens room");
            clients[1].LocalPlayer.SetCustomProperties(new ExitGames.Client.Photon.Hashtable { { "ready", true } });
            yield return new WaitUntil(() => session.Phase == "Playing");
            Check(!PhotonNetwork.CurrentRoom.IsOpen, "lock joining during round");
            var a = session.Players[clients[0].LocalPlayer.ActorNumber];
            var b = session.Players[clients[1].LocalPlayer.ActorNumber];
            a.ResetAt(new Vector3(0, .05f, -2)); b.ResetAt(new Vector3(0, .05f, 2));
            Check(clients[0].OpRaiseEvent(31, new object[] { 1f, 0f }, new RaiseEventOptions { Receivers = ReceiverGroup.MasterClient }, SendOptions.SendReliable), "send movement event");
            yield return new WaitUntil(() => a.transform.position.x > .1f);
            Check(a.transform.position.x > .1f, "controller input moves PC avatar");
            yield return new WaitForSeconds(.6f);
            var stopped = a.transform.position;
            yield return new WaitForSeconds(.3f);
            Check(Vector3.Distance(stopped, a.transform.position) < .05f, "stale input stops avatar");
            b.ResetAt(new Vector3(0, .05f, 2));
            session.Arena.SpawnCube(a, (b.transform.position - a.transform.position).normalized);
            yield return new WaitUntil(() => b.Stunned);
            var frozen = b.transform.position;
            b.SetInput(Vector2.up);
            yield return new WaitForSeconds(1.8f);
            Check(b.Stunned && Vector3.Distance(frozen, b.transform.position) < .1f, "cube freezes victim for two seconds");
            yield return new WaitForSeconds(.3f);
            Check(!b.Stunned, "stun expires");
            var snack = FindAnyObjectByType<SeaweedSnack>();
            int oldScore = a.Score; a.ResetAt(snack.transform.position);
            yield return new WaitForSeconds(.15f);
            Check(a.Score >= oldScore + 25, "snack scores 25 points");
            session.Arena.ClearEnemies(); yield return null;
            a.ResetAt(new Vector3(0, .05f, -2)); b.ResetAt(new Vector3(8, .05f, 0));
            var enemy = session.Arena.SpawnEnemy(); Check(enemy != null, "enemy.glb spawns");
            enemy.transform.position = new Vector3(0, .05f, 0);
            Physics.SyncTransforms();
            int enemyScore = a.Score;
            session.Arena.SpawnCube(a, Vector3.forward);
            yield return new WaitForSeconds(.4f);
            Check(a.Score >= enemyScore + session.settings.enemyScore && (enemy == null || !enemy.gameObject.activeSelf), "cube defeats enemy and scores once (score " + a.Score + ", expected " + (enemyScore + session.settings.enemyScore) + ")");
            session.Arena.ClearEnemies(); yield return null;
            var closeEnemy = session.Arena.SpawnEnemy(); Check(closeEnemy != null, "spawn close enemy");
            closeEnemy.transform.position = a.transform.position + Vector3.forward * .8f;
            Physics.SyncTransforms(); enemyScore = a.Score; session.Arena.SpawnCube(a, Vector3.forward);
            yield return new WaitForSeconds(.15f);
            Check(a.Score >= enemyScore + session.settings.enemyScore && (closeEnemy == null || !closeEnemy.gameObject.activeSelf), "point blank shot defeats enemy");
            session.Arena.ClearEnemies(); yield return null;
            for (int i = 0; i < 8; i++) session.Arena.SpawnEnemy();
            Check(FindObjectsByType<SeaweedEnemy>(FindObjectsSortMode.None).Length <= session.settings.maximumEnemies, "enemy population stays capped");
            Check(session.Arena.GetComponentsInChildren<Transform>(true).Length == authoredWorldCount, "gameplay reuses authored hierarchy without constructing or destroying elements");
            CheckCamera();
            session.Arena.ClearEnemies();

            var actor = clients[0].LocalPlayer.ActorNumber; clients[0].Disconnect();
            yield return new WaitForSeconds(1);
            Check(session.Players.ContainsKey(actor) && !a.gameObject.activeSelf, "inactive UID retains avatar and score");
            var rejoin = clients[0]; joining.Add(rejoin);
            var reconnectSettings = PhotonNetwork.PhotonServerSettings.AppSettings.CopyTo(new AppSettings());
            reconnectSettings.FixedRegion = "asia"; reconnectSettings.Protocol = ConnectionProtocol.WebSocketSecure; reconnectSettings.AppVersion = PhotonNetwork.AppVersion;
            Check(rejoin.ConnectUsingSettings(reconnectSettings), "reconnect same UID");
            yield return new WaitUntil(() => rejoin.State == ClientState.ConnectedToMasterServer);
            Check(rejoin.OpRejoinRoom(session.RoomCode), "rejoin retained room");
            yield return new WaitUntil(() => rejoin.InRoom && a.gameObject.activeSelf);
            Check(rejoin.LocalPlayer.ActorNumber == actor && a.Score >= 25, "rejoin keeps identity and score");
            typeof(SeaweedSession).GetField("phaseEnds", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).SetValue(session, Time.time + .1f);
            yield return new WaitUntil(() => session.Phase == "Results");
            Check(PhotonNetwork.CurrentRoom.Name == session.RoomCode, "results keep the same room");
            yield return new WaitUntil(() => session.Phase == "Lobby");
            yield return new WaitForSeconds(.5f);
            Check(PhotonNetwork.CurrentRoom.IsOpen && clients.All(c => !(c.LocalPlayer.CustomProperties["ready"] is bool r) || !r), "next round reopens room and resets Ready");
            Debug.Log("SEAWEED_NETWORK_SMOKE_SUCCESS"); finished = true;
            foreach (var c in clients) c.Disconnect(); PhotonNetwork.Disconnect();
            yield return new WaitForSeconds(.5f); Application.Quit(0);
        }
        void Check(bool condition, string message) { if (!condition) Fail(message); else Debug.Log("SEAWEED_CHECK_PASS: " + message); }
        void CheckCamera()
        {
            var camera = Camera.main;
            Check(camera.transform.position == savedCameraPosition && camera.transform.rotation == savedCameraRotation &&
                camera.orthographic == savedOrthographic && Mathf.Approximately(camera.orthographicSize, savedCameraSize) && camera.rect == savedCameraRect,
                "authored Main Camera values stay unchanged at startup and during play");
        }
        void Fail(string reason) { finished = true; Debug.LogError("SEAWEED_SMOKE_FAILED: " + reason); Application.Quit(1); }
    }
}
