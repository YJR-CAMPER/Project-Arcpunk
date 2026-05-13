// ── StormAtmosphere.cs ──
// 날씨 Phase에 따라 스카이박스, 안개, 조명을 동적으로 변경.
// 런타임에 머티리얼을 생성하므로 에디터 에셋 불필요.
// 아무 오브젝트에 붙이면 작동.

using UnityEngine;
using Arcpunk.Weather;

namespace Arcpunk.Core
{
    public class StormAtmosphere : MonoBehaviour
    {
        [Header("Sky Colors")]
        [SerializeField] private Color _calmSkyTop = new Color(0.25f, 0.28f, 0.35f);
        [SerializeField] private Color _calmSkyBottom = new Color(0.4f, 0.38f, 0.35f);
        [SerializeField] private Color _stormSkyTop = new Color(0.08f, 0.06f, 0.1f);
        [SerializeField] private Color _stormSkyBottom = new Color(0.15f, 0.12f, 0.13f);

        [Header("Fog")]
        [SerializeField] private Color _calmFogColor = new Color(0.35f, 0.33f, 0.32f);
        [SerializeField] private Color _stormFogColor = new Color(0.1f, 0.08f, 0.1f);
        [SerializeField] private float _calmFogStart = 40f;
        [SerializeField] private float _calmFogEnd = 120f;
        [SerializeField] private float _stormFogStart = 15f;
        [SerializeField] private float _stormFogEnd = 60f;

        [Header("Ambient Light")]
        [SerializeField] private Color _calmAmbient = new Color(0.4f, 0.38f, 0.35f);
        [SerializeField] private Color _stormAmbient = new Color(0.15f, 0.1f, 0.12f);

        [Header("Directional Light")]
        [SerializeField] private float _calmLightIntensity = 0.8f;
        [SerializeField] private float _stormLightIntensity = 0.2f;
        [SerializeField] private Color _calmLightColor = new Color(0.9f, 0.85f, 0.7f);
        [SerializeField] private Color _stormLightColor = new Color(0.5f, 0.45f, 0.55f);

        [Header("Transition")]
        [SerializeField] private float _transitionSpeed = 0.5f; // 전환 속도

        private Material _skyMaterial;
        private Light _sun;
        private float _targetBlend; // 0=Calm, 1=Storm
        private float _currentBlend;

        private void Start()
        {
            CreateSkybox();
            FindSun();
            ApplyAtmosphere(0); // 초기: Calm
        }

        private void Update()
        {
            var weather = WeatherSystem.Instance;
            if (weather == null) return;

            // Phase에 따라 목표 블렌드 설정
            _targetBlend = weather.CurrentWeather.Phase switch
            {
                WeatherPhase.Calm      => 0f,
                WeatherPhase.Building  => 0.4f,
                WeatherPhase.Storm     => 1f,
                WeatherPhase.Subsiding => 0.6f,
                _ => 0f,
            };

            // 부드러운 전환
            _currentBlend = Mathf.MoveTowards(_currentBlend, _targetBlend,
                Time.deltaTime * _transitionSpeed);

            ApplyAtmosphere(_currentBlend);
        }

        private void ApplyAtmosphere(float t)
        {
            // 스카이박스
            if (_skyMaterial != null)
            {
                _skyMaterial.SetColor("_Color1", Color.Lerp(_calmSkyTop, _stormSkyTop, t));
                _skyMaterial.SetColor("_Color2", Color.Lerp(_calmSkyBottom, _stormSkyBottom, t));
            }

            // 안개
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogColor = Color.Lerp(_calmFogColor, _stormFogColor, t);
            RenderSettings.fogStartDistance = Mathf.Lerp(_calmFogStart, _stormFogStart, t);
            RenderSettings.fogEndDistance = Mathf.Lerp(_calmFogEnd, _stormFogEnd, t);

            // 앰비언트
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = Color.Lerp(_calmAmbient, _stormAmbient, t);

            // 태양광
            if (_sun != null)
            {
                _sun.intensity = Mathf.Lerp(_calmLightIntensity, _stormLightIntensity, t);
                _sun.color = Color.Lerp(_calmLightColor, _stormLightColor, t);
            }

            // 카메라 배경색도 안개색에 맞춤
            if (Camera.main != null)
                Camera.main.backgroundColor = RenderSettings.fogColor;
        }

        private void CreateSkybox()
        {
            // 그래디언트 스카이박스 셰이더가 없으면 단색 배경 사용
            var shader = Shader.Find("Skybox/Procedural");
            if (shader != null)
            {
                _skyMaterial = new Material(shader);
                _skyMaterial.SetFloat("_SunSize", 0); // 태양 숨김 (뇌우 세계)
                _skyMaterial.SetFloat("_SunSizeConvergence", 1);
                _skyMaterial.SetFloat("_AtmosphereThickness", 1.5f);
                _skyMaterial.SetFloat("_Exposure", 0.5f);
                _skyMaterial.SetColor("_SkyTint", _calmSkyTop);
                _skyMaterial.SetColor("_GroundColor", _calmSkyBottom);
                RenderSettings.skybox = _skyMaterial;
            }
            else
            {
                // 폴백: 카메라 단색 배경
                if (Camera.main != null)
                {
                    Camera.main.clearFlags = CameraClearFlags.SolidColor;
                    Camera.main.backgroundColor = _calmFogColor;
                }
            }
        }

        private void FindSun()
        {
            // Directional Light 찾기
            var lights = FindObjectsOfType<Light>();
            foreach (var light in lights)
            {
                if (light.type == LightType.Directional)
                {
                    _sun = light;
                    break;
                }
            }

            if (_sun == null)
            {
                // 없으면 생성
                var sunGo = new GameObject("Sun_Directional");
                _sun = sunGo.AddComponent<Light>();
                _sun.type = LightType.Directional;
                _sun.shadows = LightShadows.Soft;
                sunGo.transform.rotation = Quaternion.Euler(50, -30, 0);
            }
        }

        private void OnDestroy()
        {
            if (_skyMaterial != null)
                Destroy(_skyMaterial);
        }
    }
}
