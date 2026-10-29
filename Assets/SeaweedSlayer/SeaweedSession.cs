using System;
using System.Linq;
using System.Collections;
using System.Collections.Generic;
using ExitGames.Client.Photon;
using Photon.Pun;
using Photon.Realtime;
using UnityEngine;
using UnityEngine.InputSystem;
using Hashtable = ExitGames.Client.Photon.Hashtable;

namespace SeaweedSlayer
{
    public sealed class SeaweedSession : MonoBehaviourPunCallbacks, IOnEventCallback
    {
        public SeaweedSettings settings;
        [Tooltip("For testing the mobile screen in the editor. WebGL selects the controller from its URL or build role.")]
        public bool editorController;
        public string editorRoom;
        public bool IsController { get; private set; }
        public bool Connected => PhotonNetwork.InRoom && !hostLost;
        public string Phase { get; private set; } = "Lobby";
        public string Status { get; private set; } = "CONNECTING";
        public string RoomCode { get; private set; }
        public string HostUid { get; private set; }
        public string JoinUrl { get; private set; }
        public bool IsReady => Connected && PhotonNetwork.LocalPlayer.CustomProperties["ready"] is bool r && r &&
            PhotonNetwork.LocalPlayer.CustomProperties["readyRound"] is int round && round == ReadyRound;
        public bool CanJoinRound => Phase == "Lobby" || Phase == "Waiting" || Phase == "Countdown";
        public bool ResultsReturning => Phase == "Results" && phaseEnds > 0;
        [SerializeField] SeaweedArena arena;
        [SerializeField] SeaweedUI pcUI, controllerUI;
        public SeaweedArena Arena { get; private set; }
        public SeaweedUI UI { get; private set; }
        public readonly Dictionary<int, SeaweedAvatar> Players = new Dictionary<int, SeaweedAvatar>();
        const byte MoveCode = 31, ThrowCode = 32, ExitResultsCode = 33, ConfirmCode = 34;
        readonly HashSet<int> resultPlayers = new HashSet<int>();
        readonly HashSet<int> exitedResults = new HashSet<int>();
        readonly HashSet<int> confirmedPlayers = new HashSet<int>();
        public bool IsConfirmed(int actor) => IsController
            ? Connected && PhotonNetwork.CurrentRoom.CustomProperties["confirmed"] is int[] ids && ids.Contains(actor)
            : confirmedPlayers.Contains(actor);
        int roundId;
        public bool HasExitedResults { get; private set; }
        public int ResultsWaiting => resultPlayers.Count - exitedResults.Count;
        int createAttempts, joinRetries;
        bool hostLost, rejoining, solo, retryJoin;
        float phaseEnds, nextPublish, nextReconnect, retryJoinAt;
        AudioSource musicSource, effectsSource;
        AudioClip shotClip, hurtClip, itemClip, countClip, startClip;
        public float Remaining => Mathf.Max(0, phaseEnds - (Phase == "Results" ? Time.unscaledTime : Time.time));

        void Start()
        {
            Application.runInBackground = true;
            IsController = editorController;
#if SEAWEED_CONTROLLER
            IsController = true;
#endif
            if (Application.platform == RuntimePlatform.WebGLPlayer &&
                (Query("mode") == "controller" || !string.IsNullOrEmpty(Query("room")) ||
                 Application.absoluteURL.Split('?')[0].TrimEnd('/').EndsWith("/Controller", StringComparison.OrdinalIgnoreCase))) IsController = true;
            if (arena == null || pcUI == null || controllerUI == null)
                throw new InvalidOperationException("Game scene needs authored Arena, PC UI and Controller UI references.");
            if (!IsController) InitializeAudio();
            arena.gameObject.SetActive(!IsController);
            pcUI.gameObject.SetActive(!IsController); controllerUI.gameObject.SetActive(IsController);
            UI = IsController ? controllerUI : pcUI; UI.Initialize(this);
            if (!IsController) { Arena = arena; Arena.Initialize(this); Arena.gameObject.SetActive(false); }
            PhotonNetwork.AutomaticallySyncScene = false;
            PhotonNetwork.GameVersion = "seaweed-slayer-v1";
            PhotonNetwork.KeepAliveInBackground = 60;
            if (IsController)
            {
                RoomCode = Query("room"); HostUid = Query("uid");
                if (string.IsNullOrEmpty(HostUid)) HostUid = Query("host");
                if (string.IsNullOrEmpty(RoomCode)) RoomCode = string.IsNullOrEmpty(editorRoom) ? PlayerPrefs.GetString("ss.room") : editorRoom;
                if (string.IsNullOrEmpty(HostUid)) HostUid = PlayerPrefs.GetString("ss.host");
                string uid = PlayerPrefs.GetString("ss.uid", "");
                if (string.IsNullOrEmpty(uid)) uid = Guid.NewGuid().ToString("N");
                PlayerPrefs.SetString("ss.uid", uid); PlayerPrefs.SetString("ss.room", RoomCode ?? "");
                PlayerPrefs.SetString("ss.host", HostUid ?? ""); PlayerPrefs.Save();
                PhotonNetwork.AuthValues = new AuthenticationValues(uid);
                if (string.IsNullOrEmpty(RoomCode)) { SetStatus("สแกน QR จากหน้าจอ PC เพื่อเข้าห้อง"); return; }
                rejoining = PlayerPrefs.GetString("ss.joined") == RoomCode;
            }
            else { HostUid = Guid.NewGuid().ToString("N"); PhotonNetwork.AuthValues = new AuthenticationValues(HostUid); }
            Connect();
        }
        void InitializeAudio()
        {
            shotClip = Resources.Load<AudioClip>("sounds/shot");
            hurtClip = Resources.Load<AudioClip>("sounds/hurt");
            itemClip = Resources.Load<AudioClip>("sounds/item");
            countClip = Resources.Load<AudioClip>("sounds/count");
            startClip = Resources.Load<AudioClip>("sounds/digit");
            musicSource = gameObject.AddComponent<AudioSource>();
            musicSource.playOnAwake = false;
            musicSource.spatialBlend = 0;
            musicSource.loop = true;
            musicSource.volume = .35f;
            musicSource.clip = Resources.Load<AudioClip>("sounds/BGM");
            if (musicSource.clip != null) musicSource.Play();

            // Keep effects separate so overlapping shots and hits never interrupt the music.
            effectsSource = gameObject.AddComponent<AudioSource>();
            effectsSource.playOnAwake = false;
            effectsSource.spatialBlend = 0;
        }
        public void PlayShotSound()
        { if (effectsSource != null && shotClip != null) effectsSource.PlayOneShot(shotClip); }
        public void PlayHurtSound()
        { if (effectsSource != null && hurtClip != null) effectsSource.PlayOneShot(hurtClip); }
        public void PlayItemSound()
        { if (effectsSource != null && itemClip != null) effectsSource.PlayOneShot(itemClip); }
        public void PlayCountSound()
        { if (effectsSource != null && countClip != null) effectsSource.PlayOneShot(countClip); }
        public void PlayStartSound()
        { if (effectsSource != null && startClip != null) effectsSource.PlayOneShot(startClip); }
        void Connect()
        {
            var app = PhotonNetwork.PhotonServerSettings.AppSettings.CopyTo(new AppSettings());
            app.FixedRegion = "asia"; app.Protocol = ConnectionProtocol.WebSocketSecure; app.AppVersion = "seaweed-slayer-v1";
            if (string.IsNullOrWhiteSpace(app.AppIdRealtime)) { SetStatus("กรุณาตั้งค่า Photon App ID"); return; }
            SetStatus("กำลังเชื่อมต่อ Photon..."); PhotonNetwork.ConnectUsingSettings(app);
        }
        static string Query(string key)
        {
            if (!Uri.TryCreate(Application.absoluteURL, UriKind.Absolute, out var uri)) return "";
            foreach (var pair in uri.Query.TrimStart('?').Split('&'))
            { var bits = pair.Split(new[] { '=' }, 2); if (bits.Length == 2 && bits[0] == key) return Uri.UnescapeDataString(bits[1]); }
            return "";
        }
        public void SetStatus(string message) { Status = message; UI?.Refresh(); }
        public override void OnConnectedToMaster()
        {
            if (IsController && retryJoin) return;
            // The PC moved to a new room; drop the connection instead of rejoining the old one in a loop.
            if (IsController && hostLost) { PhotonNetwork.Disconnect(); return; }
            if (IsController) { if (rejoining) PhotonNetwork.RejoinRoom(RoomCode); else PhotonNetwork.JoinRoom(RoomCode); }
            else CreateRoom();
        }
        void CreateRoom()
        {
            RoomCode = UnityEngine.Random.Range(100000, 1000000).ToString();
            PhotonNetwork.CreateRoom(RoomCode, new RoomOptions {
                MaxPlayers = (byte)(settings.PlayerCapacity + 1), IsVisible = false,
                PublishUserId = true, PlayerTtl = 60000, EmptyRoomTtl = 0,
                CustomRoomProperties = new Hashtable { { "host", HostUid }, { "phase", "Lobby" }, { "seconds", 0 } }
            });
        }
        public override void OnCreateRoomFailed(short code, string message)
        { if (code == ErrorCode.GameIdAlreadyExists && ++createAttempts < 5) CreateRoom(); else SetStatus("สร้างห้องไม่สำเร็จ: " + message); }
        public override void OnJoinRoomFailed(short code, string message)
        {
            if ((code == ErrorCode.JoinFailedFoundActiveJoiner || code == ErrorCode.JoinFailedFoundInactiveJoiner) && ++joinRetries <= 15)
            { rejoining = true; retryJoin = true; retryJoinAt = Time.time + 2; SetStatus("รอการเชื่อมต่อเดิมปิด กำลังกลับเข้าห้อง..."); return; }
            if (rejoining && code == ErrorCode.JoinFailedWithRejoinerNotFound)
            { rejoining = false; PhotonNetwork.JoinRoom(RoomCode); return; }
            SetStatus("เข้าห้องไม่สำเร็จ กรุณาสแกน QR ใหม่: " + message);
        }
        public override void OnJoinedRoom()
        {
            retryJoin = false; joinRetries = 0;
            if (IsController)
            {
                var host = PhotonNetwork.CurrentRoom.CustomProperties["host"] as string;
                if (PhotonNetwork.IsMasterClient || string.IsNullOrEmpty(host) || PhotonNetwork.MasterClient == null || PhotonNetwork.MasterClient.UserId != host || (!string.IsNullOrEmpty(HostUid) && HostUid != host))
                { hostLost = true; PhotonNetwork.LeaveRoom(); SetStatus("ห้อง PC สิ้นสุดแล้ว กรุณาสแกน QR ใหม่"); return; }
                HostUid = host; PlayerPrefs.SetString("ss.host", host); PlayerPrefs.SetString("ss.joined", RoomCode); PlayerPrefs.Save();
                Phase = PhotonNetwork.CurrentRoom.CustomProperties["phase"] as string ?? "Lobby";
                if (!(PhotonNetwork.LocalPlayer.CustomProperties["registered"] is bool b && b))
                    Register(PlayerPrefs.GetString("ss.name", "Player"), PlayerPrefs.GetInt("ss.color", 0));
                SetStatus("เชื่อมต่อแล้ว เลือกชื่อ สี และกด Ready");
            }
            else
            {
                SetControllerUrl(PlayerPrefs.GetString("ss.pc.url", settings.controllerUrl));
                Publish(); UI.Refresh();
            }
        }
        public void SetControllerUrl(string value)
        {
            if (IsController || !Connected) return;
            string root = (value ?? "").Trim().Split('#')[0];
            if (!Uri.TryCreate(root, UriKind.Absolute, out var endpoint) || (endpoint.Scheme != "https" && endpoint.Scheme != "http"))
            { SetStatus("ใส่ URL ของ WebGL controller แล้วกดสร้าง QR"); return; }
            PlayerPrefs.SetString("ss.pc.url", root); PlayerPrefs.Save();
            JoinUrl = root + (root.Contains("?") ? "&" : "?") + "room=" + RoomCode + "&uid=" + Uri.EscapeDataString(HostUid) + "&host=" + Uri.EscapeDataString(HostUid) + "&mode=controller";
            UI.ShowQR(JoinUrl); SetStatus("เปิดห้องแล้ว รอผู้เล่น Ready");
        }
        public void Register(string name, int color)
        {
            if (!IsController || !Connected || !CanJoinRound) return;
            name = (name ?? "").Trim(); if (name.Length == 0) { SetStatus("กรุณาใส่ชื่อผู้เล่น"); return; }
            name = name.Substring(0, Math.Min(20, name.Length)); color = Mathf.Clamp(color, 0, settings.palette.Length - 1);
            PhotonNetwork.NickName = name;
            PhotonNetwork.LocalPlayer.SetCustomProperties(new Hashtable { { "registered", true }, { "color", color }, { "ready", false } });
            PlayerPrefs.SetString("ss.name", name); PlayerPrefs.SetInt("ss.color", color); PlayerPrefs.Save();
        }
        public void Ready()
        {
            if (IsController && Connected && CanJoinRound)
                PhotonNetwork.LocalPlayer.SetCustomProperties(new Hashtable { { "ready", !IsReady }, { "readyRound", ReadyRound } });
        }
        public int ReadyRound => IsController && Connected && PhotonNetwork.CurrentRoom.CustomProperties["round"] is int value ? value : roundId;
        public void Confirm()
        {
            if (!IsController || !Connected || !IsReady || !CanJoinRound) return;
            PhotonNetwork.RaiseEvent(ConfirmCode, new object[] { ReadyRound, !IsConfirmed(PhotonNetwork.LocalPlayer.ActorNumber) },
                new RaiseEventOptions { Receivers = ReceiverGroup.MasterClient }, SendOptions.SendReliable);
        }
        public void ExitResults()
        {
            if (!IsController || !Connected || Phase != "Results" || HasExitedResults) return;
            HasExitedResults = PhotonNetwork.RaiseEvent(ExitResultsCode, ReadyRound,
                new RaiseEventOptions { Receivers = ReceiverGroup.MasterClient }, SendOptions.SendReliable);
            UI.Refresh();
        }
        public void AdminReturnToTitle()
        {
            if (!IsController && Phase == "Results") ReturnLobby();
        }
        public void Move(Vector2 direction)
        {
            if (!IsController || !Connected || Phase != "Playing") return;
            direction = Vector2.ClampMagnitude(direction, 1);
            PhotonNetwork.RaiseEvent(MoveCode, new object[] { direction.x, direction.y }, new RaiseEventOptions { Receivers = ReceiverGroup.MasterClient }, SendOptions.SendUnreliable);
        }
        public void Throw(Vector2 direction)
        {
            if (IsController && Connected && Phase == "Playing")
                PhotonNetwork.RaiseEvent(ThrowCode, new object[] { direction.x, direction.y }, new RaiseEventOptions { Receivers = ReceiverGroup.MasterClient }, SendOptions.SendReliable);
        }
        public void OnEvent(EventData e)
        {
            if (!IsController && !solo && CanJoinRound && e.Code == ConfirmCode &&
                e.CustomData is object[] confirmation && confirmation.Length == 2 &&
                confirmation[0] is int confirmRound && confirmRound == roundId && confirmation[1] is bool confirmed)
            {
                var peer = PhotonNetwork.CurrentRoom.GetPlayer(e.Sender);
                if (!IsPeerReady(peer)) return;
                if (confirmed) confirmedPlayers.Add(e.Sender);
                else
                {
                    confirmedPlayers.Remove(e.Sender);
                    if (Phase == "Countdown") RestartWaiting();
                }
                CheckReady();
                Publish(); UI.Refresh(); return;
            }
            if (!IsController && Phase == "Results" && e.Code == ExitResultsCode && e.CustomData is int round && round == roundId && resultPlayers.Contains(e.Sender))
            { exitedResults.Add(e.Sender); UI.Refresh(); return; }
            if (IsController || Phase != "Playing" || !Players.TryGetValue(e.Sender, out var avatar) || !avatar.gameObject.activeSelf) return;
            if (e.Code == MoveCode && e.CustomData is object[] a && a.Length == 2 && a[0] is float x && a[1] is float y && float.IsFinite(x) && float.IsFinite(y))
                avatar.SetInput(Vector2.ClampMagnitude(new Vector2(x, y), 1));
            else if (e.Code == ThrowCode && e.CustomData is object[] aim && aim.Length == 2 && aim[0] is float ax && aim[1] is float ay && float.IsFinite(ax) && float.IsFinite(ay))
            { avatar.SetInput(Vector2.ClampMagnitude(new Vector2(ax, ay), 1)); avatar.Throw(); }
        }
        public override void OnPlayerEnteredRoom(Player p) { if (!IsController) { SyncPlayers(); CheckReady(); } }
        public override void OnPlayerPropertiesUpdate(Player p, Hashtable changed)
        { if (!IsController) { SyncPlayers(); CheckReady(); } UI.Refresh(); }
        public override void OnRoomPropertiesUpdate(Hashtable changed)
        {
            if (IsController) { Phase = PhotonNetwork.CurrentRoom.CustomProperties["phase"] as string ?? "Lobby"; if (Phase != "Results") HasExitedResults = false; UI.Refresh(); }
        }
        public override void OnPlayerLeftRoom(Player p)
        {
            if (!IsController && Players.TryGetValue(p.ActorNumber, out var avatar))
            {
                avatar.SetInput(Vector2.zero); avatar.gameObject.SetActive(false);
                if (!p.IsInactive && CanJoinRound) { Arena.ReleaseAvatar(avatar); Players.Remove(p.ActorNumber); }
                if (Phase == "Results" && resultPlayers.Contains(p.ActorNumber)) { exitedResults.Add(p.ActorNumber); }
                CheckReady(); UI.Refresh();
            }
        }
        void SyncPlayers()
        {
            foreach (var peer in PhotonNetwork.PlayerList.Where(p => !p.IsLocal))
            {
                if (!(peer.CustomProperties["registered"] is bool registered && registered) || !(peer.CustomProperties["color"] is int color) || color < 0 || color >= settings.palette.Length) continue;
                if (!Players.TryGetValue(peer.ActorNumber, out var avatar))
                {
                    avatar = Arena.CreateAvatar(peer.ActorNumber, peer.NickName, settings.palette[color]);
                    if (avatar == null) continue;
                    Players.Add(peer.ActorNumber, avatar);
                }
                if (CanJoinRound) avatar.SetProfile(peer.NickName, settings.palette[color]);
                avatar.gameObject.SetActive(!peer.IsInactive);
            }
            UI.Refresh();
        }
        void CheckReady()
        {
            if (IsController || solo || !CanJoinRound) return;
            var peers = PhotonNetwork.PlayerList.Where(p => !p.IsLocal && !p.IsInactive && Players.ContainsKey(p.ActorNumber)).ToArray();
            bool lostConfirmation = confirmedPlayers.RemoveWhere(id => !peers.Any(p => p.ActorNumber == id && IsPeerReady(p))) > 0;
            bool anyReady = peers.Any(IsPeerReady);
            bool allConfirmed = peers.Length > 0 && peers.All(p => IsPeerReady(p) && confirmedPlayers.Contains(p.ActorNumber));
            if (!anyReady)
            {
                confirmedPlayers.Clear();
                if (Phase != "Lobby") { Phase = "Lobby"; phaseEnds = 0; Publish(); }
            }
            else if (Phase == "Lobby" || (Phase == "Countdown" && (lostConfirmation || !allConfirmed))) RestartWaiting();
            else if (Phase == "Waiting" && allConfirmed)
            { Phase = "Countdown"; phaseEnds = Time.time + 10; Publish(); }
            UI.Refresh();
        }
        void RestartWaiting()
        {
            confirmedPlayers.Clear();
            Phase = "Waiting"; phaseEnds = 0; Publish();
        }
        bool IsPeerReady(Player peer) => peer != null && !peer.IsLocal && !peer.IsInactive && Players.ContainsKey(peer.ActorNumber) &&
            peer.CustomProperties["ready"] is bool ready && ready && peer.CustomProperties["readyRound"] is int round && round == roundId;
        public void PlaySolo()
        {
            if (IsController || Phase != "Lobby" || Players.Values.Any(p => p.gameObject.activeSelf)) return;
            var avatar = Arena.CreateAvatar(0, "Solo", settings.palette[0]);
            if (avatar == null) return;
            solo = true; Players[0] = avatar;
            if (PhotonNetwork.InRoom) PhotonNetwork.CurrentRoom.IsOpen = false;
            Phase = "Countdown"; phaseEnds = Time.time + 10; Publish(); UI.Refresh();
        }
        void Update()
        {
            if (UI == null) return;
            if (IsController)
            {
                if (retryJoin && !hostLost && !PhotonNetwork.InRoom && PhotonNetwork.IsConnectedAndReady && Time.time >= retryJoinAt)
                { retryJoin = false; PhotonNetwork.RejoinRoom(RoomCode); }
                if (!Connected && !hostLost && rejoining && !PhotonNetwork.IsConnected && Time.time > nextReconnect)
                { nextReconnect = Time.time + 5; Connect(); }
                return;
            }
            // Photon property echoes can arrive after a rapid Ready/cancel/Ready sequence.
            // Keep the room's admission flag consistent with the current host phase.
            if (PhotonNetwork.InRoom && PhotonNetwork.CurrentRoom.IsOpen != (CanJoinRound && !solo))
                PhotonNetwork.CurrentRoom.IsOpen = CanJoinRound && !solo;
            if (Phase == "Results" && Keyboard.current != null &&
                (Keyboard.current.enterKey.wasPressedThisFrame || Keyboard.current.numpadEnterKey.wasPressedThisFrame)) AdminReturnToTitle();
            if (!PhotonNetwork.IsConnected && !solo && Time.time > nextReconnect) { nextReconnect = Time.time + 5; Connect(); }
            if (ResultsReturning && Remaining == 0) ReturnLobby();
            else if (Phase == "Countdown" && Remaining == 0)
            {
                if (PhotonNetwork.InRoom) PhotonNetwork.CurrentRoom.IsOpen = false;
                Arena.gameObject.SetActive(true);
                Phase = "Playing"; phaseEnds = Time.time + settings.roundSeconds; Arena.NewRound();
                foreach (var p in Players.Values) { p.Score = 0; p.ResetAt(Arena.FreePosition()); }
                Publish(); UI.Refresh();
            }
            else if (Phase == "Playing" && Remaining == 0)
            {
                Phase = "Results"; phaseEnds = Time.unscaledTime + 30; Arena.ClearProjectiles(); Arena.ClearEnemies(); Arena.ClearSnacks();
                resultPlayers.Clear(); exitedResults.Clear();
                foreach (var peer in PhotonNetwork.PlayerList.Where(p => !p.IsLocal && !p.IsInactive && Players.ContainsKey(p.ActorNumber))) resultPlayers.Add(peer.ActorNumber);
                foreach (var p in Players.Values) p.SetInput(Vector2.zero);
                Publish(); UI.Refresh();
            }
            if (Time.time > nextPublish) { nextPublish = Time.time + 1; Publish(); UI.Refresh(); }
        }
        void ReturnLobby()
        {
            Phase = "Lobby"; solo = false; phaseEnds = 0; roundId++;
            confirmedPlayers.Clear();
            resultPlayers.Clear(); exitedResults.Clear();
            // Every round opens a fresh room and QR so stale phones or room state never carry over.
            foreach (var avatar in Players.Values) Arena.ReleaseAvatar(avatar);
            Players.Clear();
            Arena.ClearSnacks(); Arena.ClearEnemies(); Arena.ClearProjectiles(); Arena.ClearFeedback();
            Arena.gameObject.SetActive(false);
            RoomCode = null; JoinUrl = null; createAttempts = 0; UI.ClearQR();
            // OnConnectedToMaster creates the next room once the old one is left.
            if (PhotonNetwork.InRoom) PhotonNetwork.LeaveRoom(false);
            else if (PhotonNetwork.IsConnectedAndReady) CreateRoom();
            SetStatus("กำลังสร้างห้องใหม่...");
        }
        void Publish()
        {
            if (!PhotonNetwork.InRoom || IsController) return;
            var ranking = Players.Values.OrderByDescending(p => p.Score).ThenBy(p => p.Actor).ToArray();
            PhotonNetwork.CurrentRoom.SetCustomProperties(new Hashtable {
                { "phase", Phase }, { "round", roundId }, { "seconds", Mathf.CeilToInt(Remaining) },
                { "confirmed", confirmedPlayers.ToArray() },
                { "ranking", string.Join("\n", ranking.Select((p, i) => (i + 1) + ". " + p.DisplayName + "  " + p.Score)) },
                { "rankNames", ranking.Select(p => p.DisplayName).ToArray() },
                { "rankScores", ranking.Select(p => p.Score).ToArray() },
                { "rankColors", ranking.Select(p => ColorUtility.ToHtmlStringRGB(p.Color)).ToArray() }
            });
        }
        public override void OnMasterClientSwitched(Player p)
        {
            if (!IsController) return;
            hostLost = true; PhotonNetwork.LeaveRoom(false);
            retryJoin = false; rejoining = false;
            PlayerPrefs.DeleteKey("ss.joined"); PlayerPrefs.DeleteKey("ss.room"); PlayerPrefs.DeleteKey("ss.host"); PlayerPrefs.Save();
            SetStatus(Phase == "Results" ? "จบรอบแล้ว สแกน QR ใหม่บนจอ PC เพื่อเล่นรอบต่อไป" : "PC หลุดจากห้อง กรุณาสแกน QR ใหม่");
        }
        public override void OnDisconnected(DisconnectCause cause)
        {
            retryJoin = false;
            if (IsController && hostLost) return;
            if (!IsController && solo) { SetStatus("เล่นเดี่ยว / Photon ไม่ได้เชื่อมต่อ"); return; }
            foreach (var p in Players.Values) p.SetInput(Vector2.zero);
            rejoining = IsController && !hostLost && !string.IsNullOrEmpty(RoomCode);
            nextReconnect = Time.time + 3;
            if (!IsController) { Phase = "Lobby"; solo = false; RoomCode = null; JoinUrl = null; UI.ClearQR(); resultPlayers.Clear(); exitedResults.Clear(); foreach (var p in Players.Values) Arena.ReleaseAvatar(p); Players.Clear(); Arena.ClearSnacks(); Arena.ClearProjectiles(); Arena.ClearEnemies(); Arena.ClearFeedback(); Arena.gameObject.SetActive(false); }
            SetStatus("การเชื่อมต่อขาด: " + cause + (rejoining ? " กำลังเชื่อมต่อใหม่..." : ""));
        }
    }
}
