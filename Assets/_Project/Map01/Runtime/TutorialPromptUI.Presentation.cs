using UnityEngine;

namespace ShadowVale.Map01
{
    /// <summary>
    /// Restyles the tutorial's existing hint UI in code (surfaces, shadowed text, buttons, the progress bar), so
    /// the scene's and prefabs' references stay intact.
    /// </summary>
    public sealed partial class TutorialPromptUI
    {
        private UnityEngine.UI.Text controlMode, controlCaption;
        private readonly UnityEngine.UI.Image[] caps = new UnityEngine.UI.Image[4];
        private readonly UnityEngine.UI.Text[] capLabels = new UnityEngine.UI.Text[4];
        private TutorialMouseGlyph mouseGlyph;
        private UnityEngine.UI.Image trackImage;
        private Material glassMaterial;
        private readonly Material[] capMaterials = new Material[4];
        private UnityEngine.Rendering.Universal.UniversalAdditionalCameraData glassCamera;
        private UnityEngine.Rendering.Universal.CameraOverrideOption previousColorOption;
        private string shownControl;
        private static readonly Color Ink = new(.035f, .055f, .048f, .97f);
        private static readonly Color Paper = new(.96f, .96f, .94f);
        private static readonly Color LightGold = new(1, .9f, .59f);

        // Restyle the existing hint hierarchy so scene/prefab references and buttons stay intact.
        private void ImprovePresentation()
        {
            var rect = (RectTransform)card.transform;
            rect.sizeDelta = new Vector2(880, 228); rect.anchoredPosition = new Vector2(0, 188);
            var background = card.GetComponent<UnityEngine.UI.Image>(); background.color = Color.white;
            var border = card.GetComponent<UnityEngine.UI.Outline>(); if (border != null) border.enabled = false;
            glassMaterial = Surface("Tutorial glass panel", rect.sizeDelta, 18, 0, true);
            glassMaterial.SetColor("_Frame", new Color(.7f, .74f, .68f, .5f));
            glassMaterial.SetColor("_Fill", new Color(.045f, .055f, .04f, .44f));
            background.material = glassMaterial;
            glassCamera = mission.gameCamera.GetComponent<UnityEngine.Rendering.Universal.UniversalAdditionalCameraData>();
            if (glassCamera != null) { previousColorOption = glassCamera.requiresColorOption; glassCamera.requiresColorTexture = true; }
            Place(lesson.rectTransform, new Vector2(832, 30), new Vector2(0, 85));
            lesson.alignment = TextAnchor.MiddleLeft; lesson.fontSize = 21; lesson.fontStyle = FontStyle.Normal; lesson.color = new Color(.96f, .8f, .43f);
            ShadowText(lesson);
            Place(keyPlate.rectTransform, new Vector2(198, 156), new Vector2(-311, -10));
            keyPlate.enabled = false;
            key.enabled = false; // Preserve the original serialized label for existing consumers.
            Place(instruction.rectTransform, new Vector2(570, 72), new Vector2(115, 25));
            instruction.alignment = TextAnchor.MiddleLeft; instruction.fontSize = 28; instruction.color = Paper;
            ShadowText(instruction);
            instruction.horizontalOverflow = HorizontalWrapMode.Wrap; instruction.verticalOverflow = VerticalWrapMode.Truncate;
            Place(progressText.rectTransform, new Vector2(570, 60), new Vector2(115, -40));
            progressText.alignment = TextAnchor.MiddleLeft; progressText.fontSize = 21;
            progressText.color = new Color(.83f, .85f, .82f); progressText.horizontalOverflow = HorizontalWrapMode.Wrap;
            ShadowText(progressText);
            var track = card.transform.Find("Track") as RectTransform;
            if (track != null) { Place(track, new Vector2(570, 2), new Vector2(115, -84)); trackImage = track.GetComponent<UnityEngine.UI.Image>(); }
            Place(fill.rectTransform, new Vector2(570, 2), new Vector2(115, -84));
            // Keep the left edge fixed as progress increases.
            fill.rectTransform.pivot = new Vector2(0, .5f); fill.rectTransform.anchoredPosition = new Vector2(-170, -84);
            controlMode = Label("Control mode", keyPlate.transform, new Vector2(194, 24), new Vector2(0, 66), 16, Paper);
            controlMode.fontStyle = FontStyle.Bold; ShadowText(controlMode);
            controlCaption = Label("Control caption", keyPlate.transform, new Vector2(194, 24), new Vector2(0, -66), 15, Paper); ShadowText(controlCaption);
            for (int i = 0; i < caps.Length; i++) {
                caps[i] = Panel("Keycap " + i, keyPlate.transform, Vector2.one * 60, new Vector2(.5f, .5f), Vector2.zero, Color.white).GetComponent<UnityEngine.UI.Image>();
                capMaterials[i] = Surface("Tutorial key " + i, new Vector2(60, 60), 4, 9, false);
                caps[i].material = capMaterials[i];
                capLabels[i] = Label("Glyph", caps[i].transform, new Vector2(40, 40), Vector2.zero, 26, LightGold);
                capLabels[i].fontStyle = FontStyle.Bold; ShadowText(capLabels[i]);
            }
            var mouseObject = new GameObject("Mouse glyph", typeof(RectTransform), typeof(CanvasRenderer), typeof(TutorialMouseGlyph));
            mouseObject.transform.SetParent(keyPlate.transform, false);
            mouseGlyph = mouseObject.GetComponent<TutorialMouseGlyph>(); mouseGlyph.raycastTarget = false;
            Place(mouseGlyph.rectTransform, new Vector2(52, 67), Vector2.zero);
            StyleButton(transform.Find("Skip"), new Vector2(1, 1), new Vector2(-186, -40), new Vector2(324, 40), 16);
            StyleButton(transform.Find("Retry"), new Vector2(1, 1), new Vector2(-186, -83), new Vector2(324, 32), 14);
        }

        private static Material Surface(string name, Vector2 size, float radius, float inset, bool glass)
        {
            var material = new Material(Resources.Load<Shader>("TutorialGlass")) { name = name, hideFlags = HideFlags.DontSave };
            material.SetVector("_Size", new Vector4(size.x, size.y, 0, 0)); material.SetFloat("_Radius", radius); material.SetFloat("_Inset", inset);
            material.SetFloat("_Glass", glass ? 1 : 0); material.SetFloat("_Border", glass ? 1.3f : 1.7f); material.SetFloat("_Glow", glass ? 0 : 1);
            material.SetColor("_Fill", new Color(.06f, .065f, .04f, .24f)); material.SetColor("_Frame", LightGold); return material;
        }
        private static void ShadowText(UnityEngine.UI.Text text)
        {
            var shadow = text.GetComponent<UnityEngine.UI.Shadow>() ?? text.gameObject.AddComponent<UnityEngine.UI.Shadow>();
            shadow.effectColor = new Color(0, 0, 0, .6f); shadow.effectDistance = new Vector2(1, -2);
        }

        private static void Place(RectTransform rect, Vector2 size, Vector2 position)
        {
            rect.anchorMin = rect.anchorMax = new Vector2(.5f, .5f); rect.pivot = new Vector2(.5f, .5f);
            rect.sizeDelta = size; rect.anchoredPosition = position;
        }
        private static void StyleButton(Transform target, Vector2 anchor, Vector2 position, Vector2 size, int fontSize)
        {
            if (target == null) return;
            var rect = (RectTransform)target; rect.anchorMin = rect.anchorMax = anchor; rect.sizeDelta = size; rect.anchoredPosition = position;
            target.GetComponent<UnityEngine.UI.Image>().color = Ink;
            var text = target.GetComponentInChildren<UnityEngine.UI.Text>();
            text.fontSize = fontSize; text.rectTransform.sizeDelta = size - new Vector2(16, 4);
            var button = target.GetComponent<UnityEngine.UI.Button>(); var colors = button.colors;
            colors.highlightedColor = new Color(1.4f, 1.4f, 1.2f); colors.pressedColor = Gold; button.colors = colors;
        }
        private void UpdateControls(float pulse)
        {
            if (controlMode == null) return;
            bool complete = director.Progress >= 1;
            var accent = LightGold;
            if (shownControl != director.KeyLabel) {
                shownControl = director.KeyLabel;
                foreach (var cap in caps) cap.gameObject.SetActive(false);
                mouseGlyph.gameObject.SetActive(shownControl.StartsWith("CHUỘT"));
                controlCaption.rectTransform.anchoredPosition = new Vector2(0, -66);
                mouseGlyph.Button = shownControl == "CHUỘT TRÁI" ? 1 : shownControl == "CHUỘT PHẢI" ? 2 : 0;
                if (shownControl == "W A S D") {
                    Cap(0, "W", new Vector2(0, 28), 42); Cap(1, "A", new Vector2(-49, -21), 42);
                    Cap(2, "S", new Vector2(0, -21), 42); Cap(3, "D", new Vector2(49, -21), 42);
                    controlCaption.text = "DI CHUYỂN";
                } else if (shownControl == "CTRL / C") {
                    Cap(0, "Ctrl", new Vector2(-39, 0), 76); Cap(1, "C", new Vector2(45, 0), 42);
                    controlCaption.text = "CTRL HOẶC C";
                } else if (mouseGlyph.gameObject.activeSelf) {
                    controlCaption.text = mouseGlyph.Button == 1 ? "CHUỘT TRÁI" : mouseGlyph.Button == 2 ? "CHUỘT PHẢI" : "DI CHUỘT";
                } else {
                    bool keyboard = shownControl.Length <= 5;
                    Cap(0, shownControl, Vector2.zero, keyboard ? 108 : 164);
                    capLabels[0].fontSize = keyboard ? 26 : 18;
                    controlCaption.text = keyboard ? "BÀN PHÍM" : "MỤC TIÊU";
                }
            }
            bool movement = shownControl == "W A S D";
            controlMode.text = complete ? "ĐÃ XONG" : movement || shownControl == "CHUỘT PHẢI" || shownControl == "F" ? "GIỮ" :
                shownControl == "CHUỘT" ? "QUAN SÁT" : shownControl.Length <= 5 || shownControl == "CTRL / C" || shownControl == "CHUỘT TRÁI" ? "NHẤN" : "THỰC HIỆN";
            controlMode.color = Paper;
            for (int i = 0; i < caps.Length; i++) {
                capMaterials[i].SetColor("_Frame", new Color(accent.r, accent.g, accent.b, director.CuePending ? .7f + pulse * .3f : .85f));
                capMaterials[i].SetFloat("_Glow", director.CuePending ? .65f + pulse * .65f : .7f);
            }
            mouseGlyph.color = Color.Lerp(Paper, accent, .25f + pulse * .5f);
            fill.color = accent;
            bool showProgress = director.Progress > 0 && !complete;
            if (trackImage != null) trackImage.enabled = showProgress; fill.enabled = showProgress;
        }
        private void Cap(int index, string caption, Vector2 position, float width)
        {
            const float height = 42;
            caps[index].gameObject.SetActive(true); Place(caps[index].rectTransform, new Vector2(width + 18, height + 18), position);
            capMaterials[index].SetVector("_Size", new Vector4(width + 18, height + 18, 0, 0));
            capLabels[index].fontSize = 26; capLabels[index].text = caption; capLabels[index].rectTransform.sizeDelta = new Vector2(width - 4, height);
        }
        private void OnDestroy()
        {
            if (glassCamera != null) glassCamera.requiresColorOption = previousColorOption;
            if (glassMaterial != null) Destroy(glassMaterial);
            foreach (var material in capMaterials) if (material != null) Destroy(material);
        }
        private string SupportingHint()
        {
            if (director.AwaitingArrival) return "Đi theo các dấu > sáng trên mặt đường tới điểm hướng dẫn.";
            return director.Step switch {
                TutorialStep.Move => "Đi theo dấu >. Di chuột để xoay góc nhìn.",
                TutorialStep.Sprint => "Nhấn một lần để bật chạy nhanh. Nhấn lại để tắt.",
                TutorialStep.Crouch => "Nhấn một lần để khom. Nhấn lại để đứng lên.",
                TutorialStep.Weapons => "Phím 1: súng  ·  Phím 2: dao",
                TutorialStep.Shoot => "Giữ chuột phải để ngắm  ·  Chuột trái để bắn  ·  R nạp đạn",
                TutorialStep.Heal => "Dùng băng để hồi lượng máu đã mất.",
                TutorialStep.Squad => "Tận dụng chỗ nấp và các thao tác vừa học.",
                TutorialStep.Scout => "Đặt tâm ngắm lên kiện hàng. Giữ F đến khi quét xong.",
                TutorialStep.Return => "Theo các dấu > để rời cầu gỗ, quay về phía căn cứ.",
                _ => "Làm theo thao tác đang sáng để tiếp tục."
            };
        }
    }

    /// <summary>Vector mouse icon: only the requested mouse button receives the gold fill.</summary>
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class TutorialMouseGlyph : UnityEngine.UI.MaskableGraphic
    {
        private int button;
        public int Button { get => button; set { if (button != value) { button = value; SetVerticesDirty(); } } }
        protected override void OnPopulateMesh(UnityEngine.UI.VertexHelper mesh)
        {
            mesh.Clear(); var r = rectTransform.rect;
            void Quad(float x0, float y0, float x1, float y1, Color tint) {
                int n = mesh.currentVertCount;
                mesh.AddVert(new Vector3(x0 * r.width, y0 * r.height), tint, Vector2.zero);
                mesh.AddVert(new Vector3(x0 * r.width, y1 * r.height), tint, Vector2.zero);
                mesh.AddVert(new Vector3(x1 * r.width, y1 * r.height), tint, Vector2.zero);
                mesh.AddVert(new Vector3(x1 * r.width, y0 * r.height), tint, Vector2.zero);
                mesh.AddTriangle(n, n + 1, n + 2); mesh.AddTriangle(n, n + 2, n + 3);
            }
            Quad(-.5f, -.5f, .5f, .5f, color); Quad(-.43f, -.43f, .43f, .43f, new Color(.08f, .11f, .09f));
            Quad(-.4f, .05f, -.04f, .4f, button == 1 ? color : new Color(.3f, .36f, .31f));
            Quad(.04f, .05f, .4f, .4f, button == 2 ? color : new Color(.3f, .36f, .31f));
            Quad(-.035f, .18f, .035f, .32f, color);
        }
    }
}
