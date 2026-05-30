// ── DebugHUD.cs ──
// 전력 + 체력만 표시하는 간소화 HUD.
// 위치: 우상단 (웨이브 타이머와 겹침 방지).

using UnityEngine;
using Arcpunk.Power;

namespace Arcpunk.Core
{
    public class DebugHUD : MonoBehaviour
    {
        private GUIStyle _boxStyle;
        private GUIStyle _labelStyle;
        private GUIStyle _headerStyle;
        private bool _stylesInitialized;

        private void InitStyles()
        {
            if (_stylesInitialized) return;

            _boxStyle = new GUIStyle(GUI.skin.box);
            _boxStyle.normal.background = MakeTex(2, 2, new Color(0, 0, 0, 0.7f));

            _labelStyle = new GUIStyle(GUI.skin.label);
            _labelStyle.fontSize = 14;
            _labelStyle.normal.textColor = Color.white;
            _labelStyle.alignment = TextAnchor.UpperRight;

            _headerStyle = new GUIStyle(GUI.skin.label);
            _headerStyle.fontSize = 16;
            _headerStyle.fontStyle = FontStyle.Bold;
            _headerStyle.normal.textColor = new Color(1f, 0.9f, 0.4f);
            _headerStyle.alignment = TextAnchor.UpperRight;

            _stylesInitialized = true;
        }

        private void OnGUI()
        {
            InitStyles();

            float w = 200, lineH = 20;
            float x = Screen.width - w - 10;
            float y = 10;

            // ── 전력 ──
            var power = VoxelPowerSystem.Instance;
            if (power != null)
            {
                var (stored, capacity) = power.GetGlobalPowerStats();

                GUI.Box(new Rect(x, y, w, lineH * 3 + 6), "", _boxStyle);

                GUI.Label(new Rect(x, y, w - 5, lineH), "POWER", _headerStyle);
                y += lineH;

                GUI.Label(new Rect(x, y, w - 5, lineH),
                    $"{stored:F0} / {capacity:F0}", _labelStyle);
                y += lineH;

                // 전력 바
                float barX = x + 5, barW = w - 10, barH = 14;
                float fill = capacity > 0 ? stored / capacity : 0;
                Color barColor = fill > 0.5f ? new Color(0.2f, 0.8f, 0.3f) :
                                 fill > 0.2f ? new Color(0.9f, 0.8f, 0.1f) :
                                               new Color(0.9f, 0.2f, 0.1f);

                GUI.DrawTexture(new Rect(barX, y, barW, barH),
                    MakeTex(1, 1, new Color(0.2f, 0.2f, 0.2f)));
                GUI.DrawTexture(new Rect(barX, y, barW * fill, barH),
                    MakeTex(1, 1, barColor));

                y += lineH + 10;
            }

            // ── 체력 ──
            var playerHP = Player.PlayerController.Instance?.GetComponent<Player.PlayerHealth>();
            if (playerHP != null)
            {
                GUI.Box(new Rect(x, y, w, lineH * 2 + 6), "", _boxStyle);

                GUI.Label(new Rect(x, y, w - 5, lineH), "HEALTH", _headerStyle);
                y += lineH;

                float barX2 = x + 5, barW2 = w - 10, barH2 = 14;
                GUI.DrawTexture(new Rect(barX2, y, barW2, barH2),
                    MakeTex(1, 1, new Color(0.3f, 0f, 0f)));
                GUI.DrawTexture(new Rect(barX2, y, barW2 * playerHP.HealthRatio, barH2),
                    MakeTex(1, 1, playerHP.IsFlashing ?
                        new Color(1f, 0.3f, 0.3f) : new Color(0.8f, 0.1f, 0.1f)));
            }
        }

        private void Update()
        {
            // 디버그 키는 유지
            if (Input.GetKeyDown(KeyCode.F5))
                Inventory.PlayerInventory.Instance?.DebugFillInventory();
        }

        private Texture2D MakeTex(int width, int height, Color color)
        {
            var pixels = new Color[width * height];
            for (int i = 0; i < pixels.Length; i++)
                pixels[i] = color;
            var tex = new Texture2D(width, height);
            tex.SetPixels(pixels);
            tex.Apply();
            return tex;
        }
    }
}