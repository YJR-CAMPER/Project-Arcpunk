// ── DebugHUD.cs ──
// 화면 좌상단에 날씨/전력 정보를 표시하는 디버그 HUD.
// 프로토타입용 — 나중에 제대로 된 UI로 교체.
// 아무 오브젝트에 붙이면 작동.

using UnityEngine;
using Arcpunk.Weather;
using Arcpunk.Power;
using Arcpunk.Ghoul;
using Arcpunk.Player;

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

            _headerStyle = new GUIStyle(GUI.skin.label);
            _headerStyle.fontSize = 16;
            _headerStyle.fontStyle = FontStyle.Bold;
            _headerStyle.normal.textColor = new Color(1f, 0.9f, 0.4f);

            _stylesInitialized = true;
        }

        private void OnGUI()
        {
            InitStyles();

            float x = 10, y = 10, w = 280, lineH = 20;

            // ── 날씨 정보 ──
            var weather = WeatherSystem.Instance;
            if (weather != null)
            {
                var state = weather.CurrentWeather;

                GUI.Box(new Rect(x, y, w, lineH * 6 + 10), "", _boxStyle);

                GUI.Label(new Rect(x + 5, y, w, lineH), "⚡ WEATHER", _headerStyle);
                y += lineH;

                string phaseIcon = state.Phase switch
                {
                    WeatherPhase.Calm => "☀",
                    WeatherPhase.Building => "☁",
                    WeatherPhase.Storm => "⛈",
                    WeatherPhase.Subsiding => "🌥",
                    _ => "?"
                };

                GUI.Label(new Rect(x + 5, y, w, lineH),
                    $"Phase: {phaseIcon} {state.Phase}", _labelStyle);
                y += lineH;

                string surgeColor = state.SurgeLevel switch
                {
                    SurgeLevel.Surge1 => "<color=yellow>SURGE I</color>",
                    SurgeLevel.Surge2 => "<color=red>SURGE II</color>",
                    _ => "None",
                };
                GUI.Label(new Rect(x + 5, y, w, lineH),
                    $"Surge: {state.SurgeLevel}", _labelStyle);
                y += lineH;

                GUI.Label(new Rect(x + 5, y, w, lineH),
                    $"Power Mult: {state.SurgeMultiplier:F1}x", _labelStyle);
                y += lineH;

                GUI.Label(new Rect(x + 5, y, w, lineH),
                    $"Lightning: {state.LightningChance:P0}/tick", _labelStyle);
                y += lineH + 10;
            }

            // ── 전력 정보 ──
            var power = VoxelPowerSystem.Instance;
            if (power != null)
            {
                var (stored, capacity) = power.GetGlobalPowerStats();

                GUI.Box(new Rect(x, y, w, lineH * 4 + 10), "", _boxStyle);

                GUI.Label(new Rect(x + 5, y, w, lineH), "🔋 POWER", _headerStyle);
                y += lineH;

                GUI.Label(new Rect(x + 5, y, w, lineH),
                    $"Stored: {stored:F0} / {capacity:F0}", _labelStyle);
                y += lineH;

                // 전력 바
                float barX = x + 5, barY = y, barW = w - 10, barH = 16;
                float fillRatio = capacity > 0 ? stored / capacity : 0;

                // 배경
                GUI.DrawTexture(new Rect(barX, barY, barW, barH),
                    MakeTex(1, 1, new Color(0.2f, 0.2f, 0.2f)));
                // 채움
                Color barColor = fillRatio > 0.5f ? new Color(0.2f, 0.8f, 0.3f) :
                                 fillRatio > 0.2f ? new Color(0.9f, 0.8f, 0.1f) :
                                                    new Color(0.9f, 0.2f, 0.1f);
                GUI.DrawTexture(new Rect(barX, barY, barW * fillRatio, barH),
                    MakeTex(1, 1, barColor));
                y += lineH + lineH + 10;
            }

            // ── 구울 정보 ──
            var spawner = Ghoul.GhoulSpawner.Instance;
            if (spawner != null)
            {
                GUI.Box(new Rect(x, y, w, lineH * 2 + 10), "", _boxStyle);
                GUI.Label(new Rect(x + 5, y, w, lineH), "GHOULS", _headerStyle);
                y += lineH;
                GUI.Label(new Rect(x + 5, y, w, lineH),
                    $"Active: {spawner.ActiveGhoulCount}", _labelStyle);
                y += lineH + 10;
            }

            // ── 플레이어 체력 ──
            var playerHP = Player.PlayerController.Instance?.GetComponent<Player.PlayerHealth>();
            if (playerHP != null)
            {
                GUI.Box(new Rect(x, y, w, lineH * 2 + 10), "", _boxStyle);
                GUI.Label(new Rect(x + 5, y, w, lineH), "PLAYER", _headerStyle);
                y += lineH;

                float barX2 = x + 5, barW2 = w - 10, barH2 = 16;
                GUI.DrawTexture(new Rect(barX2, y, barW2, barH2),
                    MakeTex(1, 1, new Color(0.3f, 0f, 0f)));
                GUI.DrawTexture(new Rect(barX2, y, barW2 * playerHP.HealthRatio, barH2),
                    MakeTex(1, 1, playerHP.IsFlashing ?
                        new Color(1f, 0.3f, 0.3f) : new Color(0.8f, 0.1f, 0.1f)));
                y += lineH + 10;
            }

            // ── 조작 안내 ──
            GUI.Box(new Rect(x, y, w, lineH * 3 + 10), "", _boxStyle);
            GUI.Label(new Rect(x + 5, y, w, lineH), "CONTROLS", _headerStyle);
            y += lineH;
            GUI.Label(new Rect(x + 5, y, w, lineH),
                "1-9: Block select | F1: Force Surge", _labelStyle);
            y += lineH;
            GUI.Label(new Rect(x + 5, y, w, lineH),
                "LMB: Break | RMB: Place | Scroll: Cycle", _labelStyle);
        }

        private void Update()
        {
            // F1: 강제 서지 1
            if (Input.GetKeyDown(KeyCode.F1))
                WeatherSystem.Instance?.DebugForceSurge(SurgeLevel.Surge1);

            // F2: 강제 서지 2
            if (Input.GetKeyDown(KeyCode.F2))
                WeatherSystem.Instance?.DebugForceSurge(SurgeLevel.Surge2);

            // F3: 모든 구울 제거
            if (Input.GetKeyDown(KeyCode.F3))
                GhoulSpawner.Instance?.KillAll();

            // F5: 인벤토리 디버그 채우기
            if (Input.GetKeyDown(KeyCode.F5))
                Inventory.PlayerInventory.Instance?.DebugFillInventory();
        }

        private static Texture2D _cachedTex;
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
