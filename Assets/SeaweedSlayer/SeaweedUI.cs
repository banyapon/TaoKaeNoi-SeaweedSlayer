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
        int? firingPointer;
        float nextHeldThrow;
        [SerializeField] InputField controllerUrlInput;
        [SerializeField] bool controllerView;
        Sprite qrSprite; Texture2D qrTexture;
        [SerializeField] Text countdown;
        [SerializeField] Image countdownQR;
        string previousPhase;
        int lastCountdownNumber = -1;
        float startMessageEnds;
        Text resultsHint;
        Button exitResults;
        RectTransform waitingPanel;
        Text waitingRoster, confirmText;
        Button confirm;
        RectTransform waitingBackdrop;
        RectTransform nameStep;
        bool layoutJoining, layoutReady;
        RectTransform[] resultCards;
        Text[] resultScores;
        Image[] resultAccents;
        public void Initialize(SeaweedSession s)
        {
            session = s;
            if (controllerView)
            {
                nameInput.text = PlayerPrefs.GetString("ss.name", "Player");
                SelectColor(Mathf.Clamp(PlayerPrefs.GetInt("ss.color", 0), 0, colors.Length - 1));
            }
            else
            {
                controllerUrlInput.text = PlayerPrefs.GetString("ss.pc.url", s.settings.controllerUrl);
                if (countdown == null)
                {
                    var go = new GameObject("Big countdown", typeof(RectTransform), typeof(Text), typeof(Outline));
                    go.transform.SetParent(root, false);
                    countdown = go.GetComponent<Text>();
                    countdown.font = s.settings.titleFont != null ? s.settings.titleFont : s.settings.uiFont;
                    countdown.fontSize = 180; countdown.fontStyle = FontStyle.Bold;
                    countdown.alignment = TextAnchor.MiddleCenter; countdown.color = Color.white;
                    countdown.raycastTarget = false;
                    countdown.resizeTextForBestFit = true; countdown.resizeTextMinSize = 60; countdown.resizeTextMaxSize = 180;
                    countdown.rectTransform.anchorMin = new Vector2(.3f, .2f);
                    countdown.rectTransform.anchorMax = new Vector2(.7f, .8f);
                    countdown.rectTransform.offsetMin = countdown.rectTransform.offsetMax = Vector2.zero;
                    go.GetComponent<Outline>().effectColor = Color.black;
                    go.GetComponent<Outline>().effectDistance = new Vector2(4, -4);
                }
                if (countdownQR == null)
                {
                    countdownQR = Instantiate(qr, root, false);
                    countdownQR.name = "Countdown QR";
                    var rect = countdownQR.rectTransform;
                    rect.anchorMin = rect.anchorMax = rect.pivot = Vector2.one * .5f;
                    rect.anchoredPosition = new Vector2(0, -195);
                    rect.sizeDelta = Vector2.one * 180;
                    rect.localScale = Vector3.one;
                    rect.localRotation = Quaternion.identity;
                    countdownQR.preserveAspect = true;
                    countdownQR.raycastTarget = false;
                    countdownQR.gameObject.SetActive(false);
                }
                AddQRBorder(qr);
                AddQRBorder(countdownQR);
                waitingBackdrop = CreateResultPanel("Waiting backdrop", root, Vector2.zero, Vector2.one, Color.white);
                waitingBackdrop.SetAsFirstSibling();
                var art = CreateResultPanel("Waiting artwork", waitingBackdrop, Vector2.zero, Vector2.one, Color.white);
                var artwork = art.GetComponent<Image>();
                artwork.sprite = Resources.Load<Sprite>("sprites/End");
                var fit = art.gameObject.AddComponent<AspectRatioFitter>();
                fit.aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent;
                if (artwork.sprite != null) fit.aspectRatio = artwork.sprite.rect.width / artwork.sprite.rect.height;
                CreateResultPanel("Waiting shade", waitingBackdrop, Vector2.zero, Vector2.one, new Color(0, .08f, .1f, .3f));
                AddWaitingLogo("TaoKaeNoi", "sprites/logo", new Vector2(.025f, .88f), new Vector2(.14f, .99f));
                AddWaitingLogo("DPU", "sprites/dpu", new Vector2(.85f, .90f), new Vector2(.98f, .99f));
            }
            if (controllerView) InitializeHoldFire();
            InitializeResults();
            if (controllerView)
            {
                InitializeWaiting();
                nameStep = CreateResultPanel("Name instruction", controller, Vector2.zero, Vector2.one, new Color32(255, 221, 87, 255));
                var caption = CreateResultText("Name instruction caption", 28);
                caption.transform.SetParent(nameStep, false);
                caption.text = "1. พิมพ์ชื่อผู้เล่น";
                caption.color = new Color32(13, 32, 30, 255);
                SetRegion(caption.rectTransform, Vector2.zero, Vector2.one, 4);
                readyText.resizeTextForBestFit = true;
                readyText.resizeTextMaxSize = 30;
                readyText.resizeTextMinSize = 14;
            }
            if (joystick != null) joystick.DeadZone = .06f;
            Refresh();
        }
        public void CreateQR() => session.SetControllerUrl(controllerUrlInput.text);
        void AddWaitingLogo(string name, string resource, Vector2 min, Vector2 max)
        {
            var logo = CreateResultPanel(name, waitingBackdrop, min, max, Color.white).GetComponent<Image>();
            logo.sprite = Resources.Load<Sprite>(resource); logo.preserveAspect = true;
        }
        static void AddQRBorder(Image image)
        {
            // A separate white backing keeps the QR texture and its square modules intact.
            var go = new GameObject("QR white border", typeof(RectTransform), typeof(Image));
            go.transform.SetParent(image.transform, false); go.transform.SetAsFirstSibling();
            var rect = (RectTransform)go.transform;
            rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.one * -6; rect.offsetMax = Vector2.one * 6;
            go.GetComponent<Image>().color = Color.white; go.GetComponent<Image>().raycastTarget = false;
            // Child graphics draw after their parent, so draw the QR on top of the backing.
            var face = new GameObject("QR face", typeof(RectTransform), typeof(Image));
            face.transform.SetParent(go.transform, false);
            var faceRect = (RectTransform)face.transform;
            faceRect.anchorMin = Vector2.zero; faceRect.anchorMax = Vector2.one;
            faceRect.offsetMin = Vector2.one * 6; faceRect.offsetMax = Vector2.one * -6;
            face.GetComponent<Image>().preserveAspect = true; face.GetComponent<Image>().raycastTarget = false;
        }
        public void PlaySolo() => session.PlaySolo();
        public void Throw() => session.Throw(joystick != null ? joystick.Direction : Vector2.zero);
        void InitializeHoldFire()
        {
            // Pointer-down fires immediately; pointer-up must not add a second click shot.
            throwButton.onClick = new Button.ButtonClickedEvent();
            var trigger = throwButton.gameObject.AddComponent<EventTrigger>();
            AddFireEvent(trigger, EventTriggerType.PointerDown, data => {
                var pointer = data as PointerEventData;
                if (pointer == null || pointer.button != PointerEventData.InputButton.Left ||
                    firingPointer.HasValue || !throwButton.IsInteractable() || !session.Connected || session.Phase != "Playing") return;
                firingPointer = pointer.pointerId;
                FireHeldShot();
            });
            foreach (var type in new[] { EventTriggerType.PointerUp, EventTriggerType.PointerExit })
                AddFireEvent(trigger, type, data => {
                    if (data is PointerEventData pointer && firingPointer == pointer.pointerId) firingPointer = null;
                });
            AddFireEvent(trigger, EventTriggerType.Cancel, data => firingPointer = null);
            AddFireEvent(trigger, EventTriggerType.Submit, data => { if (throwButton.IsInteractable()) Throw(); });
        }
        static void AddFireEvent(EventTrigger trigger, EventTriggerType type, UnityEngine.Events.UnityAction<BaseEventData> action)
        {
            var entry = new EventTrigger.Entry { eventID = type };
            entry.callback.AddListener(action); trigger.triggers.Add(entry);
        }
        void FireHeldShot()
        {
            Throw();
            nextHeldThrow = Time.unscaledTime + SeaweedAvatar.ThrowInterval + .02f;
        }
        public static string RankLabel(int rank) => rank == 1 ? "1st" : rank == 2 ? "2nd" : rank == 3 ? "3rd" : rank + "th";
        public void SelectColor(int color)
        {
            if (session == null || color < 0 || color >= colors.Length || color >= session.settings.palette.Length) return;
            selectedColor = color;
            for (int i = 0; i < colors.Length; i++) colors[i].transform.Find("Selection outline").gameObject.SetActive(i == color);
            if (handle != null) handle.color = session.settings.palette[color];
            throwButton.GetComponent<Image>().color = session.settings.palette[color];
            throwButton.GetComponentInChildren<Text>().color = new Color32(13, 32, 30, 255);
            profilePreview.color = new Color32(255, 221, 87, 255); profilePreview.text = "2. เลือกสีตัวละคร";
            if (session.Connected && session.CanJoinRound) session.Register(nameInput.text, selectedColor);
        }
        public void CommitReady()
        {
            if (!session.Connected || !session.CanJoinRound) return;
            var name = nameInput.text.Trim(); if (name.Length == 0) { session.SetStatus("กรุณาใส่ชื่อผู้เล่น"); return; }
            PhotonNetwork.NickName = name;
            PhotonNetwork.LocalPlayer.SetCustomProperties(new ExitGames.Client.Photon.Hashtable {
                { "registered", true }, { "color", selectedColor }, { "ready", !session.IsReady }, { "readyRound", session.ReadyRound }
            });
            PlayerPrefs.SetString("ss.name", name); PlayerPrefs.SetInt("ss.color", selectedColor); PlayerPrefs.Save();
        }
        void InitializeWaiting()
        {
            waitingPanel = CreateResultPanel("Ready waiting room", controller, new Vector2(.04f, .29f), new Vector2(.96f, .70f), new Color32(13, 32, 30, 255));
            waitingRoster = CreateResultText("Waiting players", 24);
            waitingRoster.transform.SetParent(waitingPanel, false);
            SetRegion(waitingRoster.rectTransform, new Vector2(.02f, .27f), new Vector2(.98f, .98f), 2);
            var go = new GameObject("Confirm start", typeof(RectTransform), typeof(Image), typeof(Button));
            go.transform.SetParent(waitingPanel, false);
            SetRegion((RectTransform)go.transform, new Vector2(.02f, .02f), new Vector2(.98f, .25f), 0);
            go.GetComponent<Image>().color = new Color32(64, 123, 78, 255);
            confirm = go.GetComponent<Button>(); confirm.onClick.AddListener(session.Confirm);
            confirmText = CreateResultText("Confirm caption", 26); confirmText.transform.SetParent(go.transform, false);
            SetRegion(confirmText.rectTransform, Vector2.zero, Vector2.one, 0);
        }
        string WaitingPlayers()
        {
            if (!session.Connected) return "";
            var peers = PhotonNetwork.PlayerList.Where(p => p.UserId != session.HostUid && !p.IsInactive &&
                p.CustomProperties["registered"] is bool registered && registered).ToArray();
            bool Ready(Photon.Realtime.Player p) => p.CustomProperties["ready"] is bool r && r &&
                p.CustomProperties["readyRound"] is int round && round == session.ReadyRound;
            return "ผู้เล่น " + peers.Length + "/" + session.settings.PlayerCapacity + " คน / พร้อม " + peers.Count(Ready) + " คน\n" +
                string.Join("  •  ", peers.Select(p => p.NickName + (session.IsConfirmed(p.ActorNumber) ? " ✓ Confirm" : Ready(p) ? " ✓ Ready" : " (รอ)")));
        }
        void InitializeResults()
        {
            // Upgrade the existing authored result container without replacing the scene.
            SetRegion(results, Vector2.zero, Vector2.one, 0);
            results.GetComponent<Image>().color = new Color32(13, 32, 30, 255);
            results.SetAsLastSibling();
            var heading = results.Find("Result title");
            if (heading == null) heading = CreateResultText("Result title", 48).transform;
            heading.GetComponent<Text>().text = "TOP 3";
            SetRegion((RectTransform)heading, new Vector2(.05f, .82f), new Vector2(.95f, .96f));
            if (!controllerView)
            {
                var background = results.GetComponent<Image>();
                // Fill the screen without distorting the background artwork.
                var artwork = Resources.Load<Sprite>("sprites/End");
                var art = new GameObject("Results artwork", typeof(RectTransform), typeof(Image), typeof(AspectRatioFitter));
                art.transform.SetParent(results, false); art.transform.SetAsFirstSibling();
                SetRegion((RectTransform)art.transform, Vector2.zero, Vector2.one, 0);
                var image = art.GetComponent<Image>(); image.sprite = artwork; image.raycastTarget = false;
                var fit = art.GetComponent<AspectRatioFitter>(); fit.aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent;
                if (image.sprite != null) fit.aspectRatio = image.sprite.rect.width / image.sprite.rect.height;
                background.sprite = null; background.color = new Color32(13, 32, 30, 255);
                CreateResultPanel("Results shade", results, Vector2.zero, Vector2.one, new Color(0, .08f, .1f, .3f));
                heading.SetAsLastSibling();
                SetRegion((RectTransform)heading, new Vector2(.22f, .84f), new Vector2(.78f, .98f));
                var brandLogo = CreateResultPanel("Ranking TaoKaeNoi logo", results,
                    new Vector2(.02f, .84f), new Vector2(.15f, .98f), Color.white).GetComponent<Image>();
                brandLogo.sprite = Resources.Load<Sprite>("sprites/logo");
                brandLogo.preserveAspect = true;
                var universityLogo = CreateResultPanel("Ranking DPU logo", results,
                    new Vector2(.80f, .84f), new Vector2(.98f, .98f), Color.white).GetComponent<Image>();
                universityLogo.sprite = Resources.Load<Sprite>("sprites/dpu");
                universityLogo.preserveAspect = true;
                resultCards = new RectTransform[rankingRows.Length];
                resultScores = new Text[rankingRows.Length];
                resultAccents = new Image[rankingRows.Length];
            }
            for (int i = 0; i < rankingRows.Length; i++)
            {
                if (controllerView)
                {
                    rankingRows[i].gameObject.SetActive(i < 3);
                    float top = .78f - i * .18f;
                    SetRegion(rankingRows[i].rectTransform, new Vector2(.08f, top - .16f), new Vector2(.92f, top));
                    rankingRows[i].alignment = TextAnchor.MiddleCenter;
                    rankingRows[i].resizeTextMaxSize = 42;
                    continue;
                }
                bool podium = i < 3;
                int column = podium ? i : (i - 3) % 4;
                int row = podium ? 0 : (i - 3) / 4;
                float width = podium ? .3f : .225f;
                float left = .04f + column * (podium ? .31f : .232f);
                float topY = podium ? .81f : .53f - row * .083f;
                var card = CreateResultPanel("Result rank " + (i + 1), results,
                    new Vector2(left, topY - (podium ? .16f : .073f)), new Vector2(left + width, topY), new Color32(27, 141, 173, 255));
                var face = CreateResultPanel("Card face", card, Vector2.zero, Vector2.one, new Color32(13, 56, 67, 255));
                SetRegion(face, Vector2.zero, Vector2.one, podium ? 7 : 4);
                resultCards[i] = card;
                var rank = CreateResultText("Rank", podium ? 32 : 22); rank.transform.SetParent(card, false);
                rank.text = RankLabel(i + 1).ToUpperInvariant();
                rank.font = session.settings.titleFont != null ? session.settings.titleFont : session.settings.uiFont;
                rank.color = i == 0 ? new Color32(255, 214, 85, 255) : i == 1 ? new Color32(208, 224, 231, 255) : i == 2 ? new Color32(218, 162, 113, 255) : Color.white;
                SetRegion(rank.rectTransform, Vector2.zero, new Vector2(.21f, 1), 2);
                resultAccents[i] = CreateResultPanel("Player accent", card, new Vector2(.215f, .16f), new Vector2(.235f, .84f), Color.white).GetComponent<Image>();
                rankingRows[i].transform.SetParent(card, false);
                SetRegion(rankingRows[i].rectTransform, new Vector2(.27f, .08f), new Vector2(.76f, .92f), 2);
                rankingRows[i].alignment = TextAnchor.MiddleLeft;
                rankingRows[i].resizeTextMaxSize = podium ? 30 : 21;
                rankingRows[i].verticalOverflow = VerticalWrapMode.Truncate;
                resultScores[i] = CreateResultText("Score", podium ? 32 : 22); resultScores[i].transform.SetParent(card, false);
                SetRegion(resultScores[i].rectTransform, new Vector2(.77f, 0), new Vector2(.98f, 1), 2);
                card.gameObject.SetActive(false);
            }
            if (!controllerView)
            {
                var others = CreateResultText("Other rankings title", 24); others.text = "RANKINGS";
                SetRegion(others.rectTransform, new Vector2(.04f, .55f), new Vector2(.96f, .62f), 0);
            }
            resultsHint = CreateResultText("Results wait", 24);
            SetRegion(resultsHint.rectTransform, new Vector2(.05f, .04f), new Vector2(.95f, controllerView ? .2f : .1f));
            if (controllerView)
            {
                var go = new GameObject("Exit results", typeof(RectTransform), typeof(Image), typeof(Button));
                go.transform.SetParent(results, false);
                SetRegion((RectTransform)go.transform, new Vector2(.18f, .04f), new Vector2(.82f, .15f));
                go.GetComponent<Image>().color = new Color32(64, 123, 78, 255);
                exitResults = go.GetComponent<Button>(); exitResults.onClick.AddListener(session.ExitResults);
                var caption = CreateResultText("Exit caption", 28); caption.transform.SetParent(go.transform, false);
                SetRegion(caption.rectTransform, Vector2.zero, Vector2.one, 0); caption.text = "ออกจากเกม";
                SetRegion(resultsHint.rectTransform, new Vector2(.05f, .15f), new Vector2(.95f, .24f));
            }
        }
        RectTransform CreateResultPanel(string name, Transform parent, Vector2 min, Vector2 max, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image)); go.transform.SetParent(parent, false);
            var rect = (RectTransform)go.transform; SetRegion(rect, min, max, 0);
            go.GetComponent<Image>().color = color; go.GetComponent<Image>().raycastTarget = false;
            return rect;
        }
        Text CreateResultText(string name, int size)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Text)); go.transform.SetParent(results, false);
            var text = go.GetComponent<Text>(); text.font = session.settings.uiFont; text.fontSize = size;
            text.color = Color.white; text.alignment = TextAnchor.MiddleCenter; text.raycastTarget = false;
            text.resizeTextForBestFit = true; text.resizeTextMinSize = 12; text.resizeTextMaxSize = size;
            return text;
        }
        public void Refresh()
        {
            if (status == null) return;
            status.text = session.Status;
            if (session.IsController)
            {
                bool isPlaying = session.Connected && session.Phase == "Playing";
                if (previousPhase != session.Phase && joystick != null) joystick.OnPointerUp(null);
                previousPhase = session.Phase;
                controller.gameObject.SetActive(!session.Connected || session.Phase != "Results");
                // Profile and gameplay controls occupy separate phases and never overlap.
                nameInput.gameObject.SetActive(session.CanJoinRound && !session.IsReady);
                nameStep.gameObject.SetActive(session.CanJoinRound && !session.IsReady);
                ready.gameObject.SetActive(session.CanJoinRound);
                foreach (var b in colors) b.gameObject.SetActive(session.CanJoinRound && !session.IsReady);
                waitingPanel.gameObject.SetActive(session.Connected && session.CanJoinRound && session.IsReady);
                waitingRoster.text = WaitingPlayers();
                confirm.interactable = session.Connected && session.IsReady && (session.Phase == "Waiting" || session.Phase == "Countdown");
                confirmText.text = session.Connected && session.IsConfirmed(PhotonNetwork.LocalPlayer.ActorNumber) ? "ยกเลิก Confirm" : "CONFIRM / ครบทุกคนแล้วนับ 10 วินาที";
                profilePreview.gameObject.SetActive(session.CanJoinRound);
                play.gameObject.SetActive(isPlaying);
                resultsHint.text = "จบรอบแล้ว รอสแกน QR ใหม่บนจอ PC";
                exitResults.interactable = session.Connected && session.Phase == "Results" && !session.HasExitedResults;
                bool editable = session.Connected && session.CanJoinRound;
                ready.interactable = editable; nameInput.interactable = editable && !session.IsReady;
                foreach (var b in colors) b.interactable = editable && !session.IsReady;
                readyText.text = session.IsReady ? "ยกเลิก Ready" : "3. พร้อมแล้วกด Ready";
                throwButton.interactable = session.Connected && session.Phase == "Playing";
                if (!throwButton.interactable) firingPointer = null;
                results.gameObject.SetActive(session.Connected && session.Phase == "Results");
                if (session.Connected)
                {
                    string seconds = PhotonNetwork.CurrentRoom.CustomProperties["seconds"]?.ToString() ?? "";
                    status.text = "ห้อง " + session.RoomCode + " / " + (session.Phase == "Lobby" ? "มาเริ่มเล่นเกม Seaweed Slayer" :
                        session.Phase == "Waiting" ? "รอผู้เล่น " + seconds + " วินาที / รอ Confirm ครบทุกคน" : session.Phase + "  " + seconds);
                    profilePreview.text = session.IsReady ? (session.Phase == "Countdown" ? "เริ่มเกมใน " + seconds + " วินาที" : "พร้อมแล้ว รอเพื่อนในห้อง") : "2. เลือกสีตัวละคร";
                    var props = PhotonNetwork.CurrentRoom.CustomProperties;
                    if (session.Phase == "Results" && props["seconds"] is int remaining && remaining > 0)
                        resultsHint.text = "สิ้นสุด Session ใน " + remaining + " วินาที แล้วสแกน QR ใหม่บนจอ PC";
                    if (session.Phase == "Results" && props["rankNames"] is string[] names && props["rankScores"] is int[] scores && props["rankColors"] is string[] colorValues)
                    {
                        for (int i = 0; i < rankingRows.Length; i++)
                        {
                            rankingRows[i].text = i < names.Length && i < scores.Length ? (i + 1) + ". " + names[i] + "  " + scores[i] : "";
                            if (i < colorValues.Length && ColorUtility.TryParseHtmlString("#" + colorValues[i], out var color)) rankingRows[i].color = color;
                        }
                    }
                }
                if (!session.IsReady) profilePreview.text = "2. เลือกสีตัวละคร";
                profilePreview.color = new Color32(255, 221, 87, 255);
                return;
            }
            bool counting = session.Phase == "Countdown";
            bool waiting = session.Phase == "Waiting";
            lobby.gameObject.SetActive(session.Phase == "Lobby"); results.gameObject.SetActive(session.Phase == "Results");
            resultsHint.text = (session.ResultsReturning ? "กลับหน้า Title ใน " + Mathf.CeilToInt(session.Remaining) + " วินาที" :
                "รอผู้เล่นออกจากเกมอีก " + session.ResultsWaiting + " คน") + " / Admin กด Enter กลับ Title";
            qr.gameObject.SetActive(session.Phase == "Lobby" && qr.sprite != null);
            waitingBackdrop.gameObject.SetActive(counting || waiting);
            var hudLogo = root.Find("Live rank overlay/Logo");
            if (hudLogo != null) hudLogo.gameObject.SetActive(!counting && !waiting);
            countdownQR.sprite = qr.sprite;
            qr.transform.Find("QR white border/QR face").GetComponent<Image>().sprite = qr.sprite;
            countdownQR.transform.Find("QR white border/QR face").GetComponent<Image>().sprite = qr.sprite;
            countdownQR.gameObject.SetActive((counting || waiting) && countdownQR.sprite != null);
            if (previousPhase == "Countdown" && session.Phase == "Playing")
            {
                startMessageEnds = Time.unscaledTime + 1;
                session.PlayStartSound();
            }
            if (session.Phase != "Playing") startMessageEnds = 0;
            previousPhase = session.Phase;
            countdown.gameObject.SetActive(waiting || counting || (session.Phase == "Playing" && Time.unscaledTime < startMessageEnds));
            int countdownNumber = counting ? Mathf.CeilToInt(session.Remaining) : -1;
            countdown.text = waiting ? "WAITING" : counting ? countdownNumber.ToString() : "START!";
            // Refresh can run several times per frame; sound only follows a new displayed number.
            if (countdownNumber != lastCountdownNumber && countdownNumber >= 1 && countdownNumber <= 10)
                session.PlayCountSound();
            lastCountdownNumber = countdownNumber;
            title.text = session.Phase == "Results" ? "ROUND RESULTS" : "SEAWEED SLAYER";
            timer.text = session.Phase == "Lobby" ? "ROOM " + (session.RoomCode ?? "------") : waiting ? "WAIT " + Mathf.CeilToInt(session.Remaining) + " / รอ Confirm" : session.Phase == "Results" ? "TOP 3" : session.Phase == "Countdown" ? "START " + Mathf.CeilToInt(session.Remaining) : Mathf.CeilToInt(session.Remaining) + " SEC";
            if (hint != null) hint.text = string.IsNullOrEmpty(session.JoinUrl) ? "ตั้ง controllerUrl ใน SeaweedSettings" : "ห้อง " + session.RoomCode + " / กด Ready บนมือถือ";
            var ordered = session.Players.Values.OrderByDescending(p => p.Score).ThenBy(p => p.Actor).ToArray();
            roster.text = "PLAYERS " + ordered.Count(p => p.gameObject.activeSelf) + "/" + session.settings.PlayerCapacity;
            for (int i = 0; i < cards.Length; i++)
            {
                bool occupied = i < ordered.Length && i < session.settings.PlayerCapacity;
                cards[i].gameObject.SetActive(occupied);
                rosterRows[i].text = occupied ? ordered[i].DisplayName : "";
                cardScores[i].text = occupied ? (session.CanJoinRound ? ReadyLabel(ordered[i].Actor) : ordered[i].Score.ToString()) : "";
                cardRanks[i].color = i == 0 ? new Color32(255, 214, 85, 255) : i == 1 ? new Color32(208, 224, 231, 255) : i == 2 ? new Color32(218, 162, 113, 255) : Color.white;
                if (occupied) cards[i].Find("Player accent").GetComponent<Image>().color = ordered[i].Color;
                resultCards[i].gameObject.SetActive(occupied);
                rankingRows[i].text = occupied ? ordered[i].DisplayName : "";
                resultScores[i].text = occupied ? ordered[i].Score.ToString() : "";
                if (occupied) resultAccents[i].color = ordered[i].Color;
                if (occupied) rosterRows[i].color = rankingRows[i].color = ordered[i].Color;
            }
        }
        string ReadyLabel(int actor)
        {
            var peer = PhotonNetwork.InRoom ? PhotonNetwork.CurrentRoom.GetPlayer(actor) : null;
            if (peer == null) return "";
            return peer.IsInactive ? "หลุด" : peer.CustomProperties["ready"] is bool r && r &&
                peer.CustomProperties["readyRound"] is int round && round == session.ReadyRound ? "READY" : "รอ";
        }
        public void ShowQR(string url) { StopAllCoroutines(); StartCoroutine(LoadQR(url)); }
        // The old room's QR must never stay on screen while the next room is being created.
        public void ClearQR()
        {
            StopAllCoroutines();
            if (qr != null) qr.sprite = null;
            if (countdownQR != null) countdownQR.sprite = null;
            if (qrSprite != null) Destroy(qrSprite); if (qrTexture != null) Destroy(qrTexture);
            qrSprite = null; qrTexture = null;
        }
        IEnumerator LoadQR(string url)
        {
            var endpoint = "https://api.qrserver.com/v1/create-qr-code/?size=300x300&data=" + Uri.EscapeDataString(url);
            using (var request = UnityWebRequestTexture.GetTexture(endpoint))
            {
                request.timeout = 15; yield return request.SendWebRequest();
                if (request.result != UnityWebRequest.Result.Success)
                {
                    session.SetStatus("โหลด QR ไม่สำเร็จ กำลังลองใหม่: " + request.error);
                    yield return new WaitForSeconds(3);
                    if (url == session.JoinUrl) StartCoroutine(LoadQR(url));
                    yield break;
                }
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
            if (!session.Connected || session.Phase != "Playing" || !throwButton.IsActive() || !throwButton.IsInteractable()) firingPointer = null;
            if (firingPointer.HasValue && Time.unscaledTime >= nextHeldThrow) FireHeldShot();
            if (joystick == null) return;
            if (Time.unscaledTime > nextSend) { nextSend = Time.unscaledTime + .05f; session.Move(joystick.Direction); }
        }
        void LateUpdate()
        {
            if (!controllerView || root == null || play == null || joystick == null) return;
            var size = root.rect.size;
            var safe = Screen.safeArea;
            if (size.x <= 0 || size.y <= 0 || Screen.width <= 0 || Screen.height <= 0) return;
            bool joining = session.CanJoinRound;
            bool isReady = session.IsReady;
            if (size == layoutSize && safe == layoutSafeArea && joining == layoutJoining && isReady == layoutReady) return;
            layoutSize = size; layoutSafeArea = safe;
            layoutJoining = joining; layoutReady = isReady;
            var min = new Vector2(safe.xMin / Screen.width, safe.yMin / Screen.height);
            var max = new Vector2(safe.xMax / Screen.width, safe.yMax / Screen.height);
            bool portrait = safe.height > safe.width;
            if (portrait)
            {
                float split = Mathf.Lerp(min.y, max.y, .47f);
                SetRegion(controller, new Vector2(min.x, split), max);
                SetRegion(play, min, new Vector2(max.x, split));
            }
            else
            {
                float split = Mathf.Lerp(min.x, max.x, .42f);
                SetRegion(controller, min, new Vector2(split, max.y));
                SetRegion(play, new Vector2(split, min.y), max);
            }
            var logoTitle = controller.Find("Title") as RectTransform;
            if (logoTitle != null)
            {
                SetRegion(logoTitle, new Vector2(.04f, .80f), new Vector2(.96f, .98f), 2);
                var logo = logoTitle.Find("Image") as RectTransform;
                if (logo != null)
                {
                    logo.localScale = Vector3.one;
                    SetRegion(logo, Vector2.zero, Vector2.one, 0);
                    logo.GetComponent<Image>().preserveAspect = true;
                }
            }
            SetRegion(status.rectTransform, new Vector2(.04f, .71f), new Vector2(.96f, .80f), 2);
            SetRegion(nameInput.GetComponent<RectTransform>(), new Vector2(.04f, .56f), new Vector2(.96f, .70f), 2);
            SetRegion((RectTransform)ready.transform, new Vector2(.04f, .04f), new Vector2(.96f, .21f), 2);
            SetRegion(profilePreview.rectTransform, new Vector2(.04f, .22f), new Vector2(.96f, .28f), 0);
            int columns = Mathf.Min(5, colors.Length);
            int rows = Mathf.CeilToInt((float)colors.Length / columns);
            for (int i = 0; i < colors.Length; i++)
            {
                float left = .04f + (i % columns) * .92f / columns;
                float top = .55f - (i / columns) * .26f / rows;
                SetRegion((RectTransform)colors[i].transform,
                    new Vector2(left, top - .26f / rows), new Vector2(left + .92f / columns, top), 2);
            }
            if (joining)
            {
                // Before play, use the entire safe area for the setup instructions.
                SetRegion(controller, min, max);
                if (!isReady)
                {
                    if (logoTitle != null) SetRegion(logoTitle,
                        portrait ? new Vector2(.12f, .83f) : new Vector2(.04f, .80f),
                        portrait ? new Vector2(.88f, .98f) : new Vector2(.32f, .98f), 2);
                    SetRegion(status.rectTransform,
                        portrait ? new Vector2(.04f, .74f) : new Vector2(.35f, .80f), new Vector2(.96f, portrait ? .83f : .98f), 2);
                    SetRegion(nameStep, new Vector2(.04f, portrait ? .67f : .65f), new Vector2(portrait ? .96f : .46f, .73f), 2);
                    SetRegion(nameInput.GetComponent<RectTransform>(),
                        new Vector2(.04f, portrait ? .56f : .43f), new Vector2(portrait ? .96f : .46f, portrait ? .66f : .63f), 2);
                    SetRegion(profilePreview.rectTransform,
                        new Vector2(portrait ? .04f : .52f, portrait ? .47f : .65f), new Vector2(.96f, portrait ? .55f : .76f), 2);
                    SetRegion((RectTransform)ready.transform,
                        new Vector2(.04f, portrait ? .06f : .15f), new Vector2(portrait ? .96f : .46f, portrait ? .19f : .33f), 2);
                    float gridLeft = portrait ? .04f : .52f;
                    float gridWidth = portrait ? .92f : .44f;
                    float gridTop = portrait ? .47f : .63f;
                    float gridHeight = portrait ? .24f : .38f;
                    for (int i = 0; i < colors.Length; i++)
                    {
                        float left = gridLeft + i % columns * gridWidth / columns;
                        float top = gridTop - i / columns * gridHeight / rows;
                        SetRegion((RectTransform)colors[i].transform,
                            new Vector2(left, top - gridHeight / rows), new Vector2(left + gridWidth / columns, top), 2);
                    }
                }
            }
            // Both controls use one diameter in canvas units, never stretched anchors.
            float diameter = Mathf.Min(230, play.rect.width * .38f, play.rect.height * .62f);
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
        void OnDisable() { firingPointer = null; }
        void OnApplicationPause(bool paused) { if (paused) { firingPointer = null; if (session != null) session.Move(Vector2.zero); } }
        void OnApplicationFocus(bool focus) { if (!focus) { firingPointer = null; if (session != null) session.Move(Vector2.zero); } }
        void OnDestroy() { if (qrSprite != null) Destroy(qrSprite); if (qrTexture != null) Destroy(qrTexture); }
    }
}
