using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace ShadowVale.Map01
{
    /// <summary>Nonblocking key hints; only the failure panel and menu buttons receive pointer events.</summary>
    public sealed partial class TutorialPromptUI : MonoBehaviour
    {
        [SerializeField] private Canvas canvas;
        [SerializeField] private Text key, instruction, lesson, progressText, failureText, subtitles;
        [SerializeField] private Image keyPlate, fill, fade;
        [SerializeField] private GameObject card, failure;
        private TutorialDirector director;
        private Map01Mission mission;
        private bool wasFailed;
        private static readonly Color Gold = new(.95f, .76f, .37f);
        private void Start()
        {
            director = FindFirstObjectByType<TutorialDirector>(); mission = director.GetComponent<Map01Mission>();
            if (canvas == null) Build();
            ImprovePresentation();
            if (director.GetComponent<TutorialRouteGuide>() == null) director.gameObject.AddComponent<TutorialRouteGuide>();
            if (FindFirstObjectByType<EventSystem>() == null)
                new GameObject("Tutorial Event System", typeof(EventSystem), typeof(InputSystemUIInputModule));
            foreach (var button in GetComponentsInChildren<Button>(true)) {
                var caption = button.GetComponentInChildren<Text>();
                if (caption != null) caption.text = button.name == "Retry" ? "THỬ LẠI BƯỚC NÀY" : "BỎ QUA TOÀN BỘ HƯỚNG DẪN";
                if (button.name == "Retry") button.onClick.AddListener(() => director.RetryStep());
                if (button.name == "Skip") button.onClick.AddListener(() => director.SkipAll());
            }
        }
        public void Build()
        {
            canvas = GetComponent<Canvas>(); if (canvas == null) canvas = gameObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.sortingOrder = 40;
            var scaler = GetComponent<CanvasScaler>(); if (scaler == null) scaler = gameObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize; scaler.referenceResolution = new Vector2(1600, 900); scaler.matchWidthOrHeight = .5f;
            if (GetComponent<GraphicRaycaster>() == null) gameObject.AddComponent<GraphicRaycaster>();
            var pane = Panel("Hint", transform, new Vector2(760, 156), new Vector2(.5f, 0), new Vector2(0, 130), new Color(.035f, .055f, .04f, .92f)); card = pane.gameObject;
            lesson = Label("Lesson", pane, new Vector2(710, 27), new Vector2(0, 55), 17, Gold);
            keyPlate = Panel("Key plate", pane, new Vector2(208, 68), new Vector2(.5f, .5f), new Vector2(-250, 0), Gold).GetComponent<Image>();
            key = Label("Key", keyPlate.transform, new Vector2(200, 64), Vector2.zero, 24, new Color(.07f, .1f, .06f)); key.fontStyle = FontStyle.Bold;
            instruction = Label("Instruction", pane, new Vector2(470, 72), new Vector2(110, 0), 23, new Color(.96f, .93f, .81f));
            Panel("Track", pane, new Vector2(710, 5), new Vector2(.5f, .5f), new Vector2(0, -53), new Color(.25f, .28f, .21f));
            fill = Panel("Progress", pane, new Vector2(710, 5), new Vector2(.5f, .5f), new Vector2(0, -53), Gold).GetComponent<Image>();
            progressText = Label("Progress text", pane, new Vector2(710, 21), new Vector2(0, -71), 14, Gold);
            MakeButton("Skip", transform, "BỎ QUA HƯỚNG DẪN", new Vector2(300, 45), new Vector2(1, 1), new Vector2(-175, -45));
            MakeButton("Retry", transform, "THỬ LẠI BƯỚC NÀY", new Vector2(280, 40), new Vector2(.5f, 0), new Vector2(0, 30));
            var failPanel = Panel("Failure", transform, new Vector2(850, 300), new Vector2(.5f, .5f), Vector2.zero, new Color(.03f, .05f, .035f, .98f)); failure = failPanel.gameObject;
            failureText = Label("Failure reason", failPanel, new Vector2(780, 155), new Vector2(0, 52), 26, new Color(.96f, .89f, .73f));
            MakeButton("Retry", failPanel, "THỬ LẠI BƯỚC NÀY", new Vector2(350, 60), new Vector2(.5f, .5f), new Vector2(-195, -85));
            MakeButton("Skip", failPanel, "BỎ QUA TOÀN BỘ", new Vector2(350, 60), new Vector2(.5f, .5f), new Vector2(195, -85));
            failure.SetActive(false);
            fade = Panel("Fade", transform, Vector2.zero, new Vector2(.5f, .5f), Vector2.zero, Color.clear).GetComponent<Image>();
            var fr = fade.rectTransform; fr.anchorMin = Vector2.zero; fr.anchorMax = Vector2.one; fr.offsetMin = fr.offsetMax = Vector2.zero;
            subtitles = Label("Capture subtitles", transform, new Vector2(1100, 90), new Vector2(0, 90), 26, Color.white);
            subtitles.rectTransform.anchorMin = subtitles.rectTransform.anchorMax = new Vector2(.5f, 0);
        }
        private static RectTransform Panel(string name, Transform parent, Vector2 size, Vector2 anchor, Vector2 position, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image)); go.transform.SetParent(parent, false);
            var rect = (RectTransform)go.transform; rect.anchorMin = rect.anchorMax = anchor; rect.sizeDelta = size; rect.anchoredPosition = position;
            var image = go.GetComponent<Image>(); image.color = color; image.raycastTarget = false; return rect;
        }
        private static Text Label(string name, Transform parent, Vector2 size, Vector2 position, int fontSize, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Text)); go.transform.SetParent(parent, false);
            var rect = (RectTransform)go.transform; rect.sizeDelta = size; rect.anchoredPosition = position;
            var text = go.GetComponent<Text>(); text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.fontSize = fontSize; text.color = color; text.alignment = TextAnchor.MiddleCenter; text.raycastTarget = false; return text;
        }
        private static void MakeButton(string name, Transform parent, string label, Vector2 size, Vector2 anchor, Vector2 position)
        {
            var panel = Panel(name, parent, size, anchor, position, new Color(.2f, .25f, .15f, .96f));
            panel.GetComponent<Image>().raycastTarget = true; panel.gameObject.AddComponent<Button>();
            Label("Caption", panel, size - new Vector2(12, 4), Vector2.zero, 18, Gold).text = label;
        }
        private void Update()
        {
            if (director == null || mission == null) return;
            canvas.enabled = !ForestMenu.Visible && !director.Transitioning && !mission.InventoryOpen && !mission.MapOpen;
            bool cinematic = mission.Cinematic;
            EnsureCinemaFrame();
            cinemaTop.gameObject.SetActive(cinematic); cinemaBottom.gameObject.SetActive(cinematic);
            var skipButton = transform.Find("Skip"); if (skipButton != null) skipButton.gameObject.SetActive(!cinematic);
            var retryButton = transform.Find("Retry"); if (retryButton != null) retryButton.gameObject.SetActive(!cinematic);
            card.SetActive(!director.Failed && !cinematic);
            failure.SetActive(director.Failed && !ForestMenu.Visible);
            if (director.Failed && !wasFailed) { Cursor.lockState = CursorLockMode.None; Cursor.visible = true; }
            if (!director.Failed && wasFailed) { Cursor.lockState = CursorLockMode.Locked; Cursor.visible = false; }
            wasFailed = director.Failed;
            key.text = director.KeyLabel; instruction.text = director.Instruction;
            lesson.text = $"<b>HƯỚNG DẪN {(int)director.Step + 1}/11</b> · {director.LessonTitle}";
            float period = Mathf.Max(.2f, mission.Settings?.tutorialPulseSeconds ?? 1.1f);
            float light = director.CuePending ? .5f + .5f * Mathf.Sin(Time.unscaledTime * Mathf.PI * 2 / period) : 0;
            UpdateControls(light);
            fill.rectTransform.localScale = new Vector3(Mathf.Clamp01(director.Progress), 1, 1);
            progressText.text = director.Progress >= 1 ? "HOÀN THÀNH" : !string.IsNullOrEmpty(director.Tip) ? director.Tip : SupportingHint();
            failureText.text = "THỬ LẠI HƯỚNG DẪN HIỆN TẠI\n\n" + director.FailureReason + "\nEnter: thử lại";
            var capture = director.GetComponent<TutorialCaptureCutscene>();
            fade.color = new Color(0, 0, 0, capture != null ? capture.Fade : 0);
            subtitles.text = director.RevealingTargets ? director.RevealCaption : cinematic ? mission.Dialogue + "\nGiữ Esc 1 giây để bỏ qua" : "";
            subtitles.color = new Color(1, 1, 1, capture != null ? 1 - capture.Fade : 1);
        }
        private UnityEngine.UI.Image cinemaTop, cinemaBottom;
        private void EnsureCinemaFrame()
        {
            if (cinemaTop != null) return;
            UnityEngine.UI.Image Bar(string name, bool top)
            {
                var existing = transform.Find(name) as RectTransform;
                var rect = existing != null ? existing : Panel(name, transform, Vector2.zero, Vector2.zero, Vector2.zero, Color.black);
                rect.anchorMin = new Vector2(0, top ? .92f : 0); rect.anchorMax = new Vector2(1, top ? 1 : .08f);
                rect.offsetMin = rect.offsetMax = Vector2.zero;
                rect.SetSiblingIndex(fade.transform.GetSiblingIndex());
                var image = rect.GetComponent<UnityEngine.UI.Image>(); image.raycastTarget = false; return image;
            }
            cinemaTop = Bar("Cinema top", true); cinemaBottom = Bar("Cinema bottom", false);
        }
    }
}
