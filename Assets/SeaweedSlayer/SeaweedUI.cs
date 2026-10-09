using System;
using System.Collections;
using System.Linq;
using Photon.Pun;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.Networking;
using UnityEngine.UI;

namespace SeaweedSlayer
{
    public sealed class SeaweedUI : MonoBehaviour
    {
        SeaweedSession session;
        [SerializeField] RectTransform root, lobby, results, controller, play;
        [SerializeField] Text status, title, roster, timer, readyText, hint, profilePreview;
        [SerializeField] Image qr;
        [SerializeField] InputField nameInput;
        [SerializeField] Button ready, throwButton;
        [SerializeField] Button[] colors;
        [SerializeField] Text[] rosterRows, rankingRows, cardRanks, cardScores;
        [SerializeField] RectTransform[] cards;
        float nextHud;
        [SerializeField] FixedJoystick joystick;
        [SerializeField] Image handle;
        [SerializeField] CanvasScaler scaler;
        int selectedColor;
        Vector2 layoutSize;
        Rect layoutSafeArea;
        float nextSend;
        [SerializeField] InputField controllerUrlInput;
        [SerializeField] bool controllerView;
        Sprite qrSprite; Texture2D qrTexture;
        public void Initialize(SeaweedSession s)
        {
            session = s;
            if (controllerView)
            {
                nameInput.text = PlayerPrefs.GetString("ss.name", "Player");
                SelectColor(Mathf.Clamp(PlayerPrefs.GetInt("ss.color", 0), 0, colors.Length - 1));
            }
            else controllerUrlInput.text = PlayerPrefs.GetString("ss.pc.url", s.settings.controllerUrl);
            Refresh();
        }
        public void CreateQR() => session.SetControllerUrl(controllerUrlInput.text);
        public void PlaySolo() => session.PlaySolo();
        public void Throw() => session.Throw();
        public static string RankLabel(int rank) => rank == 1 ? "1st" : rank == 2 ? "2nd" : rank == 3 ? "3rd" : rank + "th";
        public void SelectColor(int color)
        {
            if (session == null || color < 0 || color >= colors.Length || color >= session.settings.palette.Length) return;
            selectedColor = color;
            for (int i = 0; i < colors.Length; i++) colors[i].transform.Find("Selection outline").gameObject.SetActive(i == color);
            if (handle != null) handle.color = session.settings.palette[color];
            throwButton.GetComponent<Image>().color = session.settings.palette[color];
            throwButton.GetComponentInChildren<Text>().color = new Color32(13, 32, 30, 255);
            profilePreview.color = session.settings.palette[color]; profilePreview.text = "สีผู้เล่นและวงใต้เท้า";
            if (session.Connected && session.Phase == "Lobby") session.Register(nameInput.text, selectedColor);
        }
        public void CommitReady()
        {
            if (!session.Connected || session.Phase != "Lobby") return;
            var name = nameInput.text.Trim(); if (name.Length == 0) { session.SetStatus("กรุณาใส่ชื่อผู้เล่น"); return; }
            PhotonNetwork.NickName = name;
            PhotonNetwork.LocalPlayer.SetCustomProperties(new ExitGames.Client.Photon.Hashtable {
                { "registered", true }, { "color", selectedColor }, { "ready", !session.IsReady }
            });
            PlayerPrefs.SetString("ss.name", name); PlayerPrefs.SetInt("ss.color", selectedColor); PlayerPrefs.Save();
        }
        public void Refresh()
        {
            if (status == null) return;
            status.text = session.Status;
            if (session.IsController)
            {
                bool editable = session.Connected && session.Phase == "Lobby";
                ready.interactable = editable; nameInput.interactable = editable && !session.IsReady;
                foreach (var b in colors) b.interactable = editable && !session.IsReady;
                readyText.text = session.IsReady ? "ยกเลิก Ready" : "READY";
                throwButton.interactable = session.Connected && session.Phase == "Playing";
                results.gameObject.SetActive(session.Connected && session.Phase == "Results");
                if (session.Connected)
                {
                    string seconds = PhotonNetwork.CurrentRoom.CustomProperties["seconds"]?.ToString() ?? "";
                    status.text = "ห้อง " + session.RoomCode + " / " + (session.Phase == "Lobby" ? "รอทุกคนกด Ready" : session.Phase + "  " + seconds);
                    profilePreview.text = session.IsReady ? "พร้อมแล้ว รอเพื่อนในห้อง" : "สีผู้เล่นและวงใต้เท้า";
                    var props = PhotonNetwork.CurrentRoom.CustomProperties;
                    if (session.Phase == "Results" && props["rankNames"] is string[] names && props["rankScores"] is int[] scores && props["rankColors"] is string[] colorValues)
                    {
                        for (int i = 0; i < rankingRows.Length; i++)
                        {
                            rankingRows[i].text = i < names.Length && i < scores.Length ? (i + 1) + ". " + names[i] + "  " + scores[i] : "";
                            if (i < colorValues.Length && ColorUtility.TryParseHtmlString("#" + colorValues[i], out var color)) rankingRows[i].color = color;
                        }
                    }
                }
                return;
            }
            lobby.gameObject.SetActive(session.Phase == "Lobby"); results.gameObject.SetActive(false);
            title.text = session.Phase == "Results" ? "ROUND RESULTS" : "SEAWEED SLAYER";
            timer.text = session.Phase == "Lobby" ? "ROOM " + (session.RoomCode ?? "------") : session.Phase == "Countdown" ? "START " + Mathf.CeilToInt(session.Remaining) : Mathf.CeilToInt(session.Remaining) + " SEC";
            if (hint != null) hint.text = string.IsNullOrEmpty(session.JoinUrl) ? "ตั้ง controllerUrl ใน SeaweedSettings" : "ห้อง " + session.RoomCode + " / กด Ready บนมือถือ";
            var ordered = session.Players.Values.OrderByDescending(p => p.Score).ThenBy(p => p.Actor).ToArray();
            roster.text = "PLAYERS " + ordered.Length + "/" + session.settings.maximumPlayers;
            for (int i = 0; i < 20; i++)
            {
                bool occupied = i < ordered.Length;
                cards[i].gameObject.SetActive(occupied);
                rosterRows[i].text = occupied ? ordered[i].DisplayName : "";
                cardScores[i].text = occupied ? (session.Phase == "Lobby" ? ReadyLabel(ordered[i].Actor) : ordered[i].Score.ToString()) : "";
                cardRanks[i].color = i == 0 ? new Color32(255, 214, 85, 255) : i == 1 ? new Color32(208, 224, 231, 255) : i == 2 ? new Color32(218, 162, 113, 255) : Color.white;
                if (occupied) cards[i].Find("Player accent").GetComponent<Image>().color = ordered[i].Color;
                rankingRows[i].text = occupied ? (i + 1) + ". " + ordered[i].DisplayName + "   " + ordered[i].Score + " pts" : "";
                if (occupied) rosterRows[i].color = rankingRows[i].color = ordered[i].Color;
            }
        }
        string ReadyLabel(int actor)
        {
            var peer = PhotonNetwork.InRoom ? PhotonNetwork.CurrentRoom.GetPlayer(actor) : null;
            if (peer == null) return "";
            return peer.IsInactive ? "หลุด" : peer.CustomProperties["ready"] is bool r && r ? "READY" : "รอ";
        }
        public void ShowQR(string url) { StartCoroutine(LoadQR(url)); }
        IEnumerator LoadQR(string url)
        {
            var endpoint = "https://api.qrserver.com/v1/create-qr-code/?size=300x300&data=" + Uri.EscapeDataString(url);
            using (var request = UnityWebRequestTexture.GetTexture(endpoint))
            {
                request.timeout = 15; yield return request.SendWebRequest();
                if (request.result != UnityWebRequest.Result.Success) { session.SetStatus("โหลด QR ไม่สำเร็จ: " + request.error); yield break; }
                if (qrSprite != null) Destroy(qrSprite); if (qrTexture != null) Destroy(qrTexture);
                qrTexture = DownloadHandlerTexture.GetContent(request); qrTexture.filterMode = FilterMode.Point;
                qrSprite = Sprite.Create(qrTexture, new Rect(0, 0, qrTexture.width, qrTexture.height), Vector2.one * .5f);
                qr.sprite = qrSprite;
            }
        }
        void Update()
        {
            if (session == null) return;
            if (!session.IsController) { if (Time.unscaledTime >= nextHud) { nextHud = Time.unscaledTime + .15f; Refresh(); } return; }
            if (joystick == null) return;
            if (Time.unscaledTime > nextSend) { nextSend = Time.unscaledTime + .05f; session.Move(joystick.Direction); }
        }
        void LateUpdate()
        {
            if (!controllerView || root == null || play == null || joystick == null) return;
            var size = root.rect.size;
            var safe = Screen.safeArea;
            if (size.x <= 0 || size.y <= 0 || Screen.width <= 0 || Screen.height <= 0) return;
            if (size == layoutSize && safe == layoutSafeArea) return;
            layoutSize = size; layoutSafeArea = safe;
            var min = new Vector2(safe.xMin / Screen.width, safe.yMin / Screen.height);
            var max = new Vector2(safe.xMax / Screen.width, safe.yMax / Screen.height);
            float split = Mathf.Lerp(min.y, max.y, .47f);
            SetRegion(controller, new Vector2(min.x, split), max);
            SetRegion(play, min, new Vector2(max.x, split));
            bool portrait = safe.height > safe.width;
            int columns = portrait ? 5 : colors.Length;
            int rows = Mathf.CeilToInt((float)colors.Length / columns);
            for (int i = 0; i < colors.Length; i++)
            {
                float left = .04f + (i % columns) * .92f / columns;
                float top = .41f - (i / columns) * .21f / rows;
                SetRegion((RectTransform)colors[i].transform,
                    new Vector2(left, top - .21f / rows), new Vector2(left + .92f / columns, top), 4);
            }
            // Both controls use one diameter in canvas units, never stretched anchors.
            float diameter = Mathf.Min(230, play.rect.width * .38f, play.rect.height * .68f);
            SetCircle((RectTransform)joystick.transform, new Vector2(.25f, .54f), diameter);
            SetCircle((RectTransform)throwButton.transform, new Vector2(.75f, .54f), diameter);
            throwButton.GetComponent<Image>().preserveAspect = true;
        }
        static void SetRegion(RectTransform rect, Vector2 min, Vector2 max, float inset = 8)
        {
            rect.anchorMin = min; rect.anchorMax = max;
            rect.offsetMin = Vector2.one * inset; rect.offsetMax = Vector2.one * -inset;
        }
        static void SetCircle(RectTransform rect, Vector2 anchor, float diameter)
        {
            rect.anchorMin = rect.anchorMax = anchor; rect.pivot = Vector2.one * .5f;
            rect.anchoredPosition = Vector2.zero; rect.sizeDelta = Vector2.one * diameter;
        }
        void OnApplicationFocus(bool focus) { if (!focus && session != null) session.Move(Vector2.zero); }
        void OnDestroy() { if (qrSprite != null) Destroy(qrSprite); if (qrTexture != null) Destroy(qrTexture); }
    }
}
