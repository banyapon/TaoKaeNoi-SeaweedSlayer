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
            if (args.Any(a => a == "-seaweed-smoke" || a == "-seaweed-preview" || a == "-seaweed-results-preview" || a == "-seaweed-controller-preview" || a == "-seaweed-lobby-preview"))
            {
                var smoke = new GameObject("Integration smoke").AddComponent<SeaweedSmoke>();
                var camera = Camera.main;
                smoke.savedCameraPosition = camera.transform.position; smoke.savedCameraRotation = camera.transform.rotation;
                smoke.savedCameraSize = camera.orthographicSize; smoke.savedOrthographic = camera.orthographic; smoke.savedCameraRect = camera.rect;
                var arena = FindAnyObjectByType<SeaweedArena>();
                smoke.authoredWorldCount = CountAuthoredWorld(arena);
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
            deadline = Time.realtimeSinceStartup + 300;
            session = FindAnyObjectByType<SeaweedSession>();
            bool resultsPreview = Environment.GetCommandLineArgs().Contains("-seaweed-results-preview");
            bool mobilePreview = Environment.GetCommandLineArgs().Contains("-seaweed-controller-preview");
            bool lobbyPreview = Environment.GetCommandLineArgs().Contains("-seaweed-lobby-preview");
            if (Environment.GetCommandLineArgs().Contains("-seaweed-preview") || resultsPreview || mobilePreview || lobbyPreview)
            {
                yield return new WaitUntil(() => session.UI != null);
                if (!mobilePreview && !lobbyPreview)
                {
                    session.PlaySolo(); yield return new WaitUntil(() => session.Phase == "Playing");
                    int previewPlayers = Environment.GetCommandLineArgs().Contains("-seaweed-full-roster") ? 19 : 5;
                    for (int i = 1; i <= previewPlayers; i++) session.Players[100 + i] = session.Arena.CreateAvatar(100 + i, "Player " + i, session.settings.palette[i % session.settings.palette.Length]);
                }
                if (resultsPreview)
                {
                    foreach (var avatar in session.Players.Values) avatar.Score = (avatar.Actor + 1) * 25;
                    typeof(SeaweedSession).GetField("phaseEnds", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).SetValue(session, Time.time);
                    yield return new WaitUntil(() => session.Phase == "Results");
                }
                yield return new WaitForSeconds(1);
                session.UI.Refresh();
                string output = System.IO.Path.Combine(Application.dataPath, resultsPreview ? "../ResultsPreview.png" : mobilePreview ? "../ControllerPreview.png" : lobbyPreview ? "../LobbyPreview.png" : "../Preview.png");
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
            CheckMobilePhases();
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
            clients[0].LocalPlayer.SetCustomProperties(new ExitGames.Client.Photon.Hashtable { { "registered", true }, { "color", 1 }, { "ready", false } });
            yield return new WaitUntil(() => session.Players.Count == 1);
            Check(session.Phase == "Lobby", "join alone does not hide QR/start");
            clients[0].LocalPlayer.SetCustomProperties(new ExitGames.Client.Photon.Hashtable { { "ready", true }, { "readyRound", session.ReadyRound } });
            yield return new WaitUntil(() => session.Phase == "Waiting");
            Check(session.Remaining == 0, "Ready opens an untimed waiting room");
            CheckUIPhase("Waiting");
            yield return new WaitForSeconds(11);
            Check(session.Phase == "Waiting" && session.Remaining == 0, "waiting does not auto-start after the old timeout");
            Confirm(clients[0], true);
            yield return new WaitUntil(() => session.Phase == "Countdown");
            Check(session.Remaining > 59 && session.Remaining <= 60, "Confirm starts sixty-second countdown");
            Check(PhotonNetwork.CurrentRoom.IsOpen, "room stays open during countdown");
            float originalStartTime = Time.time + session.Remaining;
            yield return new WaitForSeconds(1);
            clients[1].LocalPlayer.SetCustomProperties(new ExitGames.Client.Photon.Hashtable { { "registered", true }, { "color", 2 }, { "ready", true }, { "readyRound", session.ReadyRound } });
            yield return new WaitUntil(() => session.Players.Count == 2);
            Check(session.Phase == "Countdown" && Mathf.Abs(Time.time + session.Remaining - originalStartTime) < .1f, "late registration joins without resetting countdown");
            clients[1].LocalPlayer.SetCustomProperties(new ExitGames.Client.Photon.Hashtable { { "ready", false } });
            yield return new WaitForSeconds(.5f);
            Check(session.Phase == "Countdown", "one unready peer does not cancel another ready peer");
            Confirm(clients[0], false);
            yield return new WaitUntil(() => session.Phase == "Waiting");
            Check(session.Remaining == 0, "cancel Confirm clears countdown and retains Ready");
            Confirm(clients[0], true);
            yield return new WaitUntil(() => session.Phase == "Countdown");
            Check(session.Remaining > 59, "reconfirm starts a fresh sixty seconds");
            clients[0].LocalPlayer.SetCustomProperties(new ExitGames.Client.Photon.Hashtable { { "ready", false } });
            yield return new WaitUntil(() => session.Phase == "Lobby");
            Check(PhotonNetwork.CurrentRoom.IsOpen, "all peers cancel Ready returns to open lobby");
            clients[0].LocalPlayer.SetCustomProperties(new ExitGames.Client.Photon.Hashtable { { "ready", true }, { "readyRound", session.ReadyRound } });
            clients[1].LocalPlayer.SetCustomProperties(new ExitGames.Client.Photon.Hashtable { { "ready", true }, { "readyRound", session.ReadyRound } });
            yield return new WaitUntil(() => session.Phase == "Waiting");
            Confirm(clients[1], true);
            yield return new WaitUntil(() => session.Phase == "Playing");
            Check(!PhotonNetwork.CurrentRoom.IsOpen, "lock joining during round");
            var a = session.Players[clients[0].LocalPlayer.ActorNumber];
            var b = session.Players[clients[1].LocalPlayer.ActorNumber];
            a.ResetAt(new Vector3(0, .05f, -2)); b.ResetAt(new Vector3(0, .05f, 2));
            Check(clients[0].OpRaiseEvent(31, new object[] { 1f, 0f }, new RaiseEventOptions { Receivers = ReceiverGroup.MasterClient }, SendOptions.SendReliable), "send movement event");
            float movementDeadline = Time.realtimeSinceStartup + 5;
            while (a.transform.position.x <= .1f && Time.realtimeSinceStartup < movementDeadline)
            {
                // Real controllers continuously publish while the stick is held.
                clients[0].OpRaiseEvent(31, new object[] { 1f, 0f }, new RaiseEventOptions { Receivers = ReceiverGroup.MasterClient }, SendOptions.SendReliable);
                yield return new WaitForSeconds(.05f);
            }
            if (a.transform.position.x <= .1f)
            {
                var flags = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
                Debug.Log("SEAWEED_MOVEMENT_DIAGNOSTIC: position=" + a.transform.position + " input=" + typeof(SeaweedAvatar).GetField("input", flags).GetValue(a) + " active=" + a.gameObject.activeInHierarchy + " body=" + a.GetComponent<CharacterController>().enabled + " phase=" + session.Phase + " stunned=" + a.Stunned);
                foreach (var collider in Physics.OverlapSphere(a.transform.position + Vector3.up * .8f, 1)) Debug.Log("SEAWEED_NEARBY: " + collider.name + " bounds=" + collider.bounds);
            }
            Check(a.transform.position.x > .1f, "controller input moves PC avatar");
            yield return new WaitForSeconds(.6f);
            var stopped = a.transform.position;
            yield return new WaitForSeconds(.3f);
            Check(Vector3.Distance(stopped, a.transform.position) < .05f, "stale input stops avatar");
            var aimPosition = a.transform.position;
            a.SetInput(new Vector2(0, .08f)); yield return null;
            Check(Vector3.Distance(aimPosition, a.transform.position) < .05f, "small joystick nudge aims without walking");
            Check((Vector3)typeof(SeaweedAvatar).GetField("facing", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).GetValue(a) == Vector3.forward, "small joystick nudge turns immediately");
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
            int oldScore = a.Score, itemPoints = snack.Points; a.ResetAt(snack.transform.position);
            yield return new WaitForSeconds(.15f);
            Check(a.Score >= oldScore + itemPoints && !snack.gameObject.activeSelf, "snack awards its item points once");
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
            Check(CountAuthoredWorld(session.Arena) == authoredWorldCount, "gameplay reuses authored hierarchy apart from aim arrows and temporary score/particle feedback");
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
            CheckUIPhase("Results");
            yield return new WaitForSeconds(11);
            Check(session.Phase == "Results", "results remain visible after the old ten-second timeout");
            clients[0].OpRaiseEvent(33, session.ReadyRound, new RaiseEventOptions { Receivers = ReceiverGroup.MasterClient }, SendOptions.SendReliable);
            yield return new WaitForSeconds(.5f);
            Check(session.Phase == "Results" && session.ResultsWaiting == 1 && session.ResultsReturning && session.Remaining > 29 && session.Remaining <= 30,
                "first exit starts thirty-second title countdown");
            float resultsEnd = Time.time + session.Remaining;
            clients[0].OpRaiseEvent(33, session.ReadyRound, new RaiseEventOptions { Receivers = ReceiverGroup.MasterClient }, SendOptions.SendReliable);
            yield return new WaitForSeconds(.3f);
            Check(session.ResultsWaiting == 1 && Mathf.Abs(Time.time + session.Remaining - resultsEnd) < .1f, "duplicate exit does not count twice or restart timer");
            yield return new WaitUntil(() => session.Phase == "Lobby");
            Check(Time.time >= resultsEnd, "results return automatically while second mobile stays on results");
            CheckUIPhase("Lobby");
            yield return new WaitForSeconds(.5f);
            Check(PhotonNetwork.CurrentRoom.IsOpen && clients.All(c => !(c.LocalPlayer.CustomProperties["ready"] is bool r) || !r), "next round reopens room and resets Ready");
            for (int round = 2; round <= 3; round++)
            {
                foreach (var client in clients) client.LocalPlayer.SetCustomProperties(new ExitGames.Client.Photon.Hashtable { { "ready", true }, { "readyRound", session.ReadyRound } });
                yield return new WaitUntil(() => session.Phase == "Waiting"); CheckUIPhase("Waiting");
                // A request from the previous round must not start this round.
                clients[0].OpRaiseEvent(34, new object[] { session.ReadyRound - 1, true }, new RaiseEventOptions { Receivers = ReceiverGroup.MasterClient }, SendOptions.SendReliable);
                yield return new WaitForSeconds(.5f);
                Check(session.Phase == "Waiting", "stale Confirm is ignored");
                Confirm(clients[0], true);
                yield return new WaitUntil(() => session.Phase == "Countdown"); CheckUIPhase("Countdown");
                typeof(SeaweedSession).GetField("phaseEnds", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).SetValue(session, Time.time + .1f);
                yield return new WaitUntil(() => session.Phase == "Playing"); CheckUIPhase("Playing");
                Check(session.Players.Values.All(p => p.Score == 0 && !p.Stunned), "round " + round + " resets scores and stun");
                typeof(SeaweedSession).GetField("phaseEnds", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).SetValue(session, Time.time + .1f);
                yield return new WaitUntil(() => session.Phase == "Results"); CheckUIPhase("Results");
                if (round == 3)
                {
                    var keyboard = UnityEngine.InputSystem.Keyboard.current ?? UnityEngine.InputSystem.InputSystem.AddDevice<UnityEngine.InputSystem.Keyboard>();
                    UnityEngine.InputSystem.InputSystem.QueueStateEvent(keyboard, new UnityEngine.InputSystem.LowLevel.KeyboardState(UnityEngine.InputSystem.Key.Enter));
                    yield return new WaitUntil(() => session.Phase == "Lobby");
                    UnityEngine.InputSystem.InputSystem.QueueStateEvent(keyboard, new UnityEngine.InputSystem.LowLevel.KeyboardState());
                }
                else
                {
                    foreach (var client in clients)
                        client.OpRaiseEvent(33, session.ReadyRound, new RaiseEventOptions { Receivers = ReceiverGroup.MasterClient }, SendOptions.SendReliable);
                    yield return new WaitUntil(() => session.Phase == "Lobby");
                    Check(!session.ResultsReturning && session.Remaining == 0, "all players exit immediately without leaving a pending timer");
                }
                CheckUIPhase("Lobby");
                yield return new WaitForSeconds(.5f);
                Check(session.Phase == "Lobby", "admin return stays on title without old Ready restarting countdown");
            }
            Debug.Log("SEAWEED_NETWORK_SMOKE_SUCCESS"); finished = true;
            foreach (var c in clients) c.Disconnect(); PhotonNetwork.Disconnect();
            yield return new WaitForSeconds(.5f); Application.Quit(0);
        }
        static int CountAuthoredWorld(SeaweedArena arena) => arena.GetComponentsInChildren<Transform>(true)
            .Count(t => t.GetComponentInParent<SeaweedFeedback>() == null && t.name != "Ground aim arrow");
        void Confirm(LoadBalancingClient client, bool confirmed)
        {
            Check(client.OpRaiseEvent(34, new object[] { session.ReadyRound, confirmed },
                new RaiseEventOptions { Receivers = ReceiverGroup.MasterClient }, SendOptions.SendReliable), "send Confirm=" + confirmed);
        }
        void CheckMobilePhases()
        {
            var flags = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
            var go = new GameObject("Smoke controller session"); go.SetActive(false);
            var mobileSession = go.AddComponent<SeaweedSession>(); mobileSession.settings = session.settings;
            typeof(SeaweedSession).GetField("<IsController>k__BackingField", flags).SetValue(mobileSession, true);
            typeof(SeaweedSession).GetField("hostLost", flags).SetValue(mobileSession, true);
            var ui = (SeaweedUI)typeof(SeaweedSession).GetField("controllerUI", flags).GetValue(session);
            ui.Initialize(mobileSession);
            typeof(SeaweedSession).GetField("hostLost", flags).SetValue(mobileSession, false);
            RectTransform Panel(string name) => (RectTransform)typeof(SeaweedUI).GetField(name, flags).GetValue(ui);
            for (int round = 1; round <= 3; round++)
                foreach (var phase in new[] { "Lobby", "Waiting", "Countdown", "Playing", "Results" })
                {
                    typeof(SeaweedSession).GetField("<Phase>k__BackingField", flags).SetValue(mobileSession, phase); ui.Refresh();
                    var input = (UnityEngine.UI.InputField)typeof(SeaweedUI).GetField("nameInput", flags).GetValue(ui);
                    Check(input.gameObject.activeSelf == (phase == "Lobby" || phase == "Waiting" || phase == "Countdown"), "mobile round " + round + " " + phase + " profile visibility");
                    Check(Panel("play").gameObject.activeSelf == (phase == "Playing"), "mobile round " + round + " " + phase + " controls visibility");
                    Check(Panel("results").gameObject.activeSelf == (phase == "Results"), "mobile round " + round + " " + phase + " results visibility");
                }
            Destroy(go);
        }
        void CheckUIPhase(string phase)
        {
            session.UI.Refresh();
            RectTransform Panel(string name) => (RectTransform)typeof(SeaweedUI).GetField(name, System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).GetValue(session.UI);
            Check(Panel("lobby").gameObject.activeSelf == (phase == "Lobby"), phase + " has exclusive title visibility");
            Check(Panel("results").gameObject.activeSelf == (phase == "Results"), phase + " has exclusive results visibility");
            Check(session.Arena.gameObject.activeSelf == (phase == "Playing" || phase == "Results"), phase + " has correct arena visibility");
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
