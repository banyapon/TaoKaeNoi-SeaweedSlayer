using UnityEngine;
using UnityEngine.UI;
namespace SeaweedSlayer.Editor
{
    internal sealed class SeaweedUIAuthoring
    {
        SeaweedSession session;
        SeaweedUI target;
        RectTransform root, lobby, results, controller, play;
        Text status, title, roster, timer, readyText, hint, profilePreview;
        Image qr;
        InputField nameInput;
        Button ready, throwButton;
        Button[] colors;
        Text[] rosterRows, rankingRows, cardRanks, cardScores;
        RectTransform[] cards;
        float nextHud;
        FixedJoystick joystick;
        Image handle;
        CanvasScaler scaler;
        int selectedColor;
        float nextSend;
        InputField controllerUrlInput;
        bool controllerView;
        Sprite qrSprite; Texture2D qrTexture;
        readonly Color panelColor = new Color32(20, 43, 39, 242);
        public SeaweedUI Bake(SeaweedSession s, bool mobile)
        {
            session = s; controllerView = mobile;
            var canvas = new GameObject(mobile ? "Controller UI" : "PC UI", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster), typeof(SeaweedUI));
            target = canvas.GetComponent<SeaweedUI>(); root = canvas.GetComponent<RectTransform>();
            canvas.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
            scaler = canvas.GetComponent<CanvasScaler>(); scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = mobile ? new Vector2(720, 1280) : new Vector2(1280, 720); scaler.matchWidthOrHeight = .5f;
            if (mobile) BuildController(); else BuildPC();
            foreach (var field in typeof(SeaweedUIAuthoring).GetFields(System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic))
            {
                var destination = typeof(SeaweedUI).GetField(field.Name, System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
                if (destination != null && System.Attribute.IsDefined(destination, typeof(SerializeField))) destination.SetValue(target, field.GetValue(this));
            }
            UnityEditor.EditorUtility.SetDirty(target); return target;
        }
        public static Text MakeText(Transform parent, string name, Font font, int size)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Text)); go.transform.SetParent(parent, false);
            var t = go.GetComponent<Text>(); t.font = font; t.fontSize = size; t.color = Color.white;
            t.alignment = TextAnchor.MiddleCenter; t.supportRichText = false; t.raycastTarget = false;
            t.horizontalOverflow = HorizontalWrapMode.Wrap; t.verticalOverflow = VerticalWrapMode.Overflow;
            Place(t.rectTransform, Vector2.zero, Vector2.one); return t;
        }
        static void Place(RectTransform rect, Vector2 min, Vector2 max, float inset = 0)
        { rect.anchorMin = min; rect.anchorMax = max; rect.offsetMin = Vector2.one * inset; rect.offsetMax = Vector2.one * -inset; }
        RectTransform Panel(Transform parent, string name, Vector2 min, Vector2 max, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image)); go.transform.SetParent(parent, false);
            go.GetComponent<Image>().color = color; var r = go.GetComponent<RectTransform>(); Place(r, min, max, 8); return r;
        }
        Text Text(Transform parent, string name, string content, Vector2 min, Vector2 max, int size = 26, bool heading = false)
        {
            var t = MakeText(parent, name, heading ? session.settings.titleFont : session.settings.uiFont, size);
            t.text = content; t.resizeTextForBestFit = true; t.resizeTextMinSize = 10; t.resizeTextMaxSize = size;
            Place(t.rectTransform, min, max, 3); return t;
        }
        Button Button(Transform parent, string name, string caption, Vector2 min, Vector2 max, UnityEngine.Events.UnityAction click)
        {
            var r = Panel(parent, name, min, max, new Color32(64, 123, 78, 255));
            var b = r.gameObject.AddComponent<Button>(); b.targetGraphic = r.GetComponent<Image>(); if (click != null) UnityEditor.Events.UnityEventTools.AddPersistentListener(b.onClick, click);
            Text(r, "Caption", caption, Vector2.zero, Vector2.one, 28); return b;
        }
        InputField Input(Transform parent, string name, string value, Vector2 min, Vector2 max)
        {
            var r = Panel(parent, name, min, max, new Color32(234, 244, 219, 255));
            var input = r.gameObject.AddComponent<InputField>(); var text = Text(r, "Value", "", Vector2.zero, Vector2.one, 26);
            text.color = new Color32(25, 49, 40, 255); input.textComponent = text; input.targetGraphic = r.GetComponent<Image>();
            input.characterLimit = 20; input.text = value; return input;
        }
        void BuildPC()
        {
            var hud = Panel(root, "Live rank overlay", new Vector2(.77f, .025f), new Vector2(.995f, .985f), new Color(0, 0, 0, 0));
            var header = Panel(hud, "Scoreboard header", new Vector2(0, .85f), Vector2.one, panelColor);
            title = Text(header, "Game title", "SEAWEED SLAYER", new Vector2(0, .65f), Vector2.one, 23, true);
            timer = Text(header, "Timer", "", new Vector2(0, .3f), new Vector2(1, .68f), 26, true);
            roster = Text(header, "Players", "", new Vector2(0, 0), new Vector2(1, .3f), 15);
            status = Text(hud, "Connection", "", new Vector2(0, -.025f), new Vector2(1, .01f), 12);
            status.verticalOverflow = VerticalWrapMode.Overflow;
            var cardList = Panel(hud, "Rank card list", Vector2.zero, new Vector2(1, .845f), Color.clear);
            var layout = cardList.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.spacing = 3; layout.childControlWidth = true; layout.childControlHeight = true;
            layout.childForceExpandWidth = true; layout.childForceExpandHeight = false;
            layout.childAlignment = TextAnchor.UpperCenter;
            cards = new RectTransform[20]; cardRanks = new Text[20]; cardScores = new Text[20]; rosterRows = new Text[20];
            for (int i = 0; i < 20; i++)
            {
                cards[i] = Panel(cardList, "Rank card " + (i + 1), Vector2.zero, Vector2.one, panelColor);
                var rowLayout = cards[i].gameObject.AddComponent<LayoutElement>(); rowLayout.minHeight = 18; rowLayout.preferredHeight = 48;
                cardRanks[i] = Text(cards[i], "Rank", RankLabel(i + 1), new Vector2(0, 0), new Vector2(.21f, 1), 20, true);
                var accent = Panel(cards[i], "Player accent", new Vector2(.215f, .16f), new Vector2(.235f, .84f), Color.white);
                Place(accent, new Vector2(.215f, .16f), new Vector2(.235f, .84f));
                rosterRows[i] = Text(cards[i], "Name", "", new Vector2(.25f, 0), new Vector2(.73f, 1), 18);
                cardScores[i] = Text(cards[i], "Score", "", new Vector2(.73f, 0), Vector2.one, 18);
                foreach (var text in new[] { cardRanks[i], rosterRows[i], cardScores[i] })
                { text.resizeTextForBestFit = true; text.resizeTextMinSize = 10; text.resizeTextMaxSize = text.fontSize; text.verticalOverflow = VerticalWrapMode.Overflow; Place(text.rectTransform, text.rectTransform.anchorMin, text.rectTransform.anchorMax, 2); }
                rosterRows[i].alignment = TextAnchor.MiddleLeft; cards[i].gameObject.SetActive(false);
            }
            lobby = Panel(root, "Welcome / QR Lobby", new Vector2(.04f, .06f), new Vector2(.58f, .94f), panelColor);
            var logo = Panel(lobby, "TaoKaeNoi logo", new Vector2(.22f, .78f), new Vector2(.78f, .98f), Color.white);
            logo.GetComponent<Image>().sprite = session.settings.logo; logo.GetComponent<Image>().preserveAspect = true; logo.GetComponent<Image>().raycastTarget = false;
            Text(lobby, "Join title", "SCAN / READY / SLAY", new Vector2(0, .7f), new Vector2(1, .79f), 34, true);
            var image = Panel(lobby, "QR Image Sprite", new Vector2(.29f, .34f), new Vector2(.71f, .7f), Color.white);
            qr = image.GetComponent<Image>(); qr.preserveAspect = true;
            hint = Text(lobby, "QR status", "กำลังสร้างห้อง...", new Vector2(.03f, .28f), new Vector2(.97f, .34f), 18);
            controllerUrlInput = Input(lobby, "Controller URL", PlayerPrefs.GetString("ss.pc.url", session.settings.controllerUrl), new Vector2(.04f, .22f), new Vector2(.96f, .28f));
            controllerUrlInput.characterLimit = 512; controllerUrlInput.text = PlayerPrefs.GetString("ss.pc.url", session.settings.controllerUrl);
            controllerUrlInput.textComponent.fontSize = 14;
            Button(lobby, "Create QR", "สร้าง QR / เข้าห้องด้วยมือถือ", new Vector2(.04f, .15f), new Vector2(.96f, .22f), target.CreateQR);
            Text(lobby, "Instructions", "เก็บขนม +25 / ยิงศัตรู +50 / ปาเพื่อนหยุด 2 วินาที", new Vector2(0, .095f), new Vector2(1, .15f), 16);
            Button(lobby, "Solo", "เล่นคนเดียว (WASD + SPACE)", new Vector2(.04f, .005f), new Vector2(.96f, .095f), target.PlaySolo);
            results = Panel(root, "Unused result container", Vector2.zero, Vector2.zero, Color.clear);
            rankingRows = Rows(results, .05f, .87f, 19); results.gameObject.SetActive(false);
        }
        public static string RankLabel(int rank) => rank == 1 ? "1st" : rank == 2 ? "2nd" : rank == 3 ? "3rd" : rank + "th";
        Text[] Rows(Transform parent, float bottom, float top, int size)
        {
            var rows = new Text[20];
            for (int i = 0; i < rows.Length; i++)
            {
                float y = top - i * (top - bottom) / rows.Length;
                rows[i] = Text(parent, "Player row " + i, "", new Vector2(.05f, y - (top - bottom) / rows.Length), new Vector2(.95f, y), size);
                rows[i].alignment = TextAnchor.MiddleLeft;
                rows[i].verticalOverflow = VerticalWrapMode.Overflow;
                rows[i].resizeTextForBestFit = true; rows[i].resizeTextMinSize = 12; rows[i].resizeTextMaxSize = size;
                rows[i].rectTransform.offsetMin = Vector2.zero; rows[i].rectTransform.offsetMax = Vector2.zero;
            }
            return rows;
        }
        void BuildController()
        {
            Panel(root, "Controller background", Vector2.zero, Vector2.one, new Color32(13, 32, 30, 255));
            controller = Panel(root, "Profile Panel", new Vector2(0, .47f), Vector2.one, panelColor);
            Text(controller, "Title", "SEAWEED SLAYER", new Vector2(0, .82f), Vector2.one, 42, true);
            status = Text(controller, "Status", "", new Vector2(0, .66f), new Vector2(1, .83f), 20);
            nameInput = Input(controller, "Name input", PlayerPrefs.GetString("ss.name", "Player"), new Vector2(.04f, .43f), new Vector2(.54f, .65f));
            ready = Button(controller, "Ready", "READY", new Vector2(.57f, .43f), new Vector2(.96f, .65f), target.CommitReady);
            readyText = ready.GetComponentInChildren<Text>();
            selectedColor = Mathf.Clamp(PlayerPrefs.GetInt("ss.color", 0), 0, session.settings.palette.Length - 1);
            colors = new Button[session.settings.palette.Length];
            for (int i = 0; i < colors.Length; i++)
            {
                int c = i; float left = .04f + (i % 5) * .92f / 5;
                float top = .41f - (i / 5) * .105f;
                colors[i] = Button(controller, "Color " + i, "", new Vector2(left, top - .105f), new Vector2(left + .92f / 5, top), null);
                UnityEditor.Events.UnityEventTools.AddIntPersistentListener(colors[i].onClick, target.SelectColor, c);
                colors[i].GetComponent<Image>().color = session.settings.palette[i];
                var ring = Panel(colors[i].transform, "Selection outline", Vector2.zero, Vector2.one, Color.white);
                ring.GetComponent<Image>().sprite = session.settings.playerOutline; ring.GetComponent<Image>().preserveAspect = true; ring.GetComponent<Image>().raycastTarget = false;
            }
            profilePreview = Text(controller, "Profile preview", "", new Vector2(0, 0), new Vector2(1, .19f), 24);
            play = Panel(root, "Joystick Panel", Vector2.zero, new Vector2(1, .47f), new Color32(29, 62, 50, 255));
            var joy = Object.Instantiate(session.settings.joystickPrefab, play);
            joy.name = "Joystick Pack / Fixed Joystick"; joystick = joy.GetComponent<FixedJoystick>(); joystick.DeadZone = .12f;
            var joyRect = joy.GetComponent<RectTransform>(); joyRect.anchorMin = joyRect.anchorMax = new Vector2(.25f, .5f);
            joyRect.anchoredPosition = Vector2.zero; joyRect.sizeDelta = new Vector2(230, 230);
            handle = joy.transform.Find("Handle")?.GetComponent<Image>();
            throwButton = Button(play, "Throw cube", "THROW", new Vector2(.56f, .18f), new Vector2(.94f, .78f), target.Throw);
            var throwRect = (RectTransform)throwButton.transform;
            throwRect.anchorMin = throwRect.anchorMax = new Vector2(.75f, .54f);
            throwRect.anchoredPosition = Vector2.zero; throwRect.sizeDelta = Vector2.one * 230;
            throwButton.GetComponent<Image>().sprite = handle.sprite;
            throwButton.GetComponent<Image>().preserveAspect = true;
            Text(play, "Controls hint", "ลากจอยเพื่อเดิน / ปาไปทางที่หัน", new Vector2(0, 0), new Vector2(1, .16f), 21);
            results = Panel(root, "Controller Results Panel", new Vector2(.02f, .02f), new Vector2(.98f, .98f), panelColor);
            Text(results, "Result title", "ROUND RESULTS", new Vector2(0, .89f), Vector2.one, 40, true);
            rankingRows = Rows(results, .04f, .89f, 22); results.gameObject.SetActive(false);

        }
    }
}
