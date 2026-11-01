using System.Linq;
using UnityEngine;
using UnityEngine.InputSystem;

namespace ShadowVale.Map01
{
    /// <summary>
    /// The objective panel: the current objective and its hint, the reveal and fade, and toggling it.
    /// </summary>
    public sealed partial class Map01Hud
    {
        private void DrawObjective()
        {
            objectiveHeading ??= new GUIStyle(hudHeading) { fontSize = hudHeading.fontSize - 2 };
            objectiveBody ??= new GUIStyle(hudBody) { fontSize = hudBody.fontSize - 2 };
            objectiveSmall ??= new GUIStyle(hudSmall) { fontSize = hudSmall.fontSize - 2 };
            objectiveGuide ??= new GUIStyle(hudGuide) { fontSize = hudGuide.fontSize - 2 };
            var previousMatrix = GUI.matrix;
            float offset = mission.InventoryOpen || mission.MapOpen ? -158 : 160;
            GUI.matrix = previousMatrix * Matrix4x4.Translate(new Vector3(0, offset, 0));
            var translatedMatrix = GUI.matrix;
            var previousColor = GUI.color;
            float reveal = Mathf.SmoothStep(0f, 1f, objectiveReveal);
            float hintOpacity = Mathf.SmoothStep(0f, 1f, objectiveHintOpacity);
            if (hintOpacity > 0f)
            {
                // Fade the entire shortcut row together, including both icons and their outlines.
                GUI.color = new Color(previousColor.r, previousColor.g, previousColor.b, previousColor.a * hintOpacity);
                float highlight = .5f + .5f * Mathf.Sin(Time.unscaledTime * Mathf.PI * .8f);
                objectiveHintGlow ??= new GUIStyle(objectiveSmall) { normal = { textColor = HudGold } };
                objectiveKeySymbol ??= new GUIStyle(objectiveSmall) {
                    fontSize = 13, alignment = TextAnchor.MiddleCenter, wordWrap = false,
                    padding = new RectOffset(), normal = { textColor = HudPaper }
                };
                DrawObjectiveListIcon(new Rect(28, 220, 24, 30), highlight);
                const float hintX = 62, hintY = 223;
                float prefixWidth = objectiveSmall.CalcSize(new GUIContent("Nhấn")).x;
                ObjectiveHintLabel(new Rect(hintX, hintY, prefixWidth + 2, 27), "Nhấn", highlight);
                var keycap = new Rect(hintX + prefixWidth + 7, hintY - 3, 26, 30);
                DrawObjectiveRoundedOutline(keycap, highlight);
                ObjectiveHintLabel(new Rect(keycap.x + 1, keycap.y + 3, keycap.width - 2, 15), "`", highlight, objectiveKeySymbol);
                ObjectiveHintLabel(new Rect(keycap.x + 1, keycap.y + 13, keycap.width - 2, 15), "~", highlight, objectiveKeySymbol);
                ObjectiveHintLabel(new Rect(keycap.xMax + 7, hintY, 210, 27), "để xem nhiệm vụ", highlight);

            }
            if (reveal <= 0f)
            {
                GUI.color = previousColor;
                GUI.matrix = previousMatrix;
                return;
            }
            // Expand from the hint's anchor and fade; reverse smoothly when dismissed mid-animation.
            float panelScale = Mathf.Lerp(.78f, 1f, reveal);
            var pivot = new Vector3(28, 186, 0);
            GUI.matrix = translatedMatrix * Matrix4x4.Translate(pivot)
                * Matrix4x4.Scale(new Vector3(panelScale, panelScale, 1)) * Matrix4x4.Translate(-pivot);
            GUI.color = new Color(previousColor.r, previousColor.g, previousColor.b, previousColor.a * reveal);
            // Sized to the text: the longer objectives wrap to three lines and used to be cut off.
            var mapTwo=mission.MapTwo;
            string text = mapTwo!=null?mapTwo.Chapter.TrackedTask?.title??mapTwo.ObjectiveText:objectives?.ObjectiveText ?? "";
            float textHeight = objectiveBody.CalcHeight(new GUIContent(text), 292);
            bool guiding = (mapTwo!=null?mapTwo.TrackedObjective!=null:guide!=null&&guide.HasTarget) && !mission.Stopped;
            // Point 3 (Map01C3Mission): the optional objectives once the order is given, and the camp's state nearby.
            var c3 = GetComponent<Map01C3Mission>();
            var optional = c3 != null && quest != null && quest.Stage == Map01Quest.BossStage ? c3.OptionalObjectives().ToList() : null;
            string c3Status = c3 != null ? c3.StatusLine() : null;
            float c3Height = (optional != null ? optional.Count * 22 + 8 : 0) + (c3Status != null ? 28 : 0);
            string notes = mapTwo!=null?mapTwo.Chapter.Instruction:ObjectiveNotes();
            if(mapTwo?.Chapter.Companion!=null){var person=mapTwo.Chapter.Companion;float distance=Vector3.Distance(mapTwo.Player.position,person.transform.position);notes+="\n"+person.role+" · "+distance.ToString("0")+" m · Đang theo sau";}
            float notesHeight = string.IsNullOrEmpty(notes) ? 0 : objectiveSmall.CalcHeight(new GUIContent(notes), 292) + 18;
            float escortHeight = ShowObjectiveEscortHealth ? 45 : 0;
            float height = Mathf.Max(152, 258 + textHeight + (guiding ? 32 : 0) + c3Height + notesHeight + escortHeight + 14 - 186);
            // See-through: a faint shade and shadowed lettering instead of the plate, so the
            // world stays visible behind the objective.
            HudFill(new Rect(28, 186, 337, height), new Color(.02f, .03f, .02f, .22f * GUI.color.a));
            ShadowLabel(new Rect(48, 205, 296, 37), "NHIỆM VỤ", objectiveHeading);
            HudFill(new Rect(49, 246, 292, 1), new Color(.6f, .51f, .3f, .65f * GUI.color.a));
            ShadowLabel(new Rect(49, 258, 292, textHeight), text, objectiveBody);
            if (guiding) {
                float y = 258 + textHeight + 6;
                HudDiamond(new Vector2(58, y + 12), 7);
                string label=mapTwo!=null?"Mục tiêu đã ghim":guide.Label;
                float distance=mapTwo!=null?Vector3.Distance(mapTwo.Player.position,mapTwo.TrackedObjective.transform.position):guide.Distance;
                ShadowLabel(new Rect(72, y, 276, 26), $"{label}  ·  {distance:0} m", objectiveGuide);
            }
            float c3y = 258 + textHeight + (guiding ? 32 : 0) + 6;
            if (optional != null)
            {
                var old = GUI.color;
                foreach (var (what, done) in optional)
                {
                    GUI.color = done ? new Color(.72f, .86f, .55f, old.a) : old;
                    ShadowLabel(new Rect(49, c3y, 292, 22), "• " + what + (done ? " — xong" : ""), objectiveSmall);
                    c3y += 22;
                }
                GUI.color = old; c3y += 8;
            }
            if (c3Status != null) { ShadowLabel(new Rect(49, c3y, 292, 26), c3Status, objectiveGuide); c3y += 28; }
            if (notesHeight > 0)
            {
                float noteAlpha = GUI.color.a;
                HudFill(new Rect(49, c3y + 3, 292, 1), new Color(.6f, .51f, .3f, .5f * noteAlpha));
                ShadowLabel(new Rect(49, c3y + 12, 292, notesHeight - 18), notes, objectiveSmall);
                c3y += notesHeight;
            }
            if (ShowObjectiveEscortHealth) DrawObjectiveEscortHealth(c3y + 4);
            GUI.color = previousColor;
            GUI.matrix = previousMatrix;
        }

        /// <summary>Outlined clipboard and list, drawn as HUD geometry for crisp scaling.</summary>
        private static void DrawObjectiveListIcon(Rect rect, float highlight)
        {
            var ink = Color.Lerp(HudPaper, HudGold, .2f + .15f * highlight);
            ink.a = GUI.color.a;
            DrawObjectiveRoundedOutline(new Rect(rect.x, rect.y + 3, rect.width, rect.height - 3), highlight);
            ObjectiveIconStroke(new Vector2(rect.x + 7, rect.y + 6), new Vector2(rect.x + 7, rect.y + 2), ink);
            ObjectiveIconStroke(new Vector2(rect.x + 7, rect.y + 2), new Vector2(rect.x + 10, rect.y), ink);
            ObjectiveIconStroke(new Vector2(rect.x + 10, rect.y), new Vector2(rect.x + 14, rect.y), ink);
            ObjectiveIconStroke(new Vector2(rect.x + 14, rect.y), new Vector2(rect.x + 17, rect.y + 2), ink);
            ObjectiveIconStroke(new Vector2(rect.x + 17, rect.y + 2), new Vector2(rect.x + 17, rect.y + 6), ink);
            HudFill(new Rect(rect.x + 7, rect.y + 5, 10, 1.5f), ink);
            for (int row = 0; row < 5; row++)
                HudFill(new Rect(rect.x + 5, rect.y + 10 + row * 3.5f, row == 4 ? 8 : 14, 1.25f), ink);
        }

        private void ObjectiveHintLabel(Rect rect, string text, float highlight, GUIStyle style = null)
        {
            style ??= objectiveSmall;
            var saved = GUI.color;
            var glowStyle = style == objectiveSmall ? objectiveHintGlow : style;
            GUI.color = new Color(HudGold.r, HudGold.g, HudGold.b, saved.a * (.12f + .12f * highlight));
            for (int i = 0; i < 8; i++)
            {
                float angle = i * Mathf.PI / 4;
                GUI.Label(new Rect(rect.x + Mathf.Cos(angle), rect.y + Mathf.Sin(angle), rect.width, rect.height), text, glowStyle);
            }
            GUI.color = saved;
            ShadowLabel(rect, text, style);
        }

        private static void DrawObjectiveRoundedOutline(Rect rect, float highlight)
        {
            // Antialiased rounded outline only: no background box behind the shortcut.
            objectiveRoundedOutline ??= MakeTexture(64, (u, v) => {
                var q = new Vector2(Mathf.Abs(u - .5f) - .37f, Mathf.Abs(v - .5f) - .37f);
                float distance = new Vector2(Mathf.Max(q.x, 0), Mathf.Max(q.y, 0)).magnitude
                    + Mathf.Min(Mathf.Max(q.x, q.y), 0) - .09f;
                return new Color(1, 1, 1, Mathf.Clamp01(1f - Mathf.Abs(distance) / .027f));
            });
            var saved = GUI.color;
            GUI.color = new Color(HudGold.r, HudGold.g, HudGold.b, saved.a * (.12f + .12f * highlight));
            for (int i = 0; i < 8; i++)
            {
                float angle = i * Mathf.PI / 4;
                GUI.DrawTexture(new Rect(rect.x + Mathf.Cos(angle), rect.y + Mathf.Sin(angle), rect.width, rect.height), objectiveRoundedOutline);
            }
            var ink = Color.Lerp(HudPaper, HudGold, .15f + .15f * highlight);
            ink.a = saved.a;
            GUI.color = ink;
            GUI.DrawTexture(rect, objectiveRoundedOutline);
            GUI.color = saved;
        }

        private static void ObjectiveIconStroke(Vector2 from, Vector2 to, Color color)
        {
            var previous = RotateGui(from, Mathf.Atan2(to.y - from.y, to.x - from.x) * Mathf.Rad2Deg);
            HudFill(new Rect(from.x, from.y - .75f, Vector2.Distance(from, to), 1.5f), color);
            GUI.matrix = previous;
        }

        /// <summary>[`] toggles the objective panel with a fresh five-second preview.</summary>
        public bool ObjectiveShown { get; private set; } = true;
        public void ToggleObjective()
        {
            ObjectiveShown = !ObjectiveShown;
            objectiveSecondsLeft = ObjectiveShown ? 5f : 0f;
        }
    }
}
