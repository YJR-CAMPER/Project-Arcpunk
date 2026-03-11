// ── LightningVFX.cs ──
// 번개 시각/음향 효과. 외부 에셋 0개, Unity 내장 기능만 사용.
//
// 구성:
//   - LineRenderer: 하얀 지그재그 선 (Additive 블렌딩)
//   - Point Light: 낙뢰 지점 섬광
//   - AudioSource: 천둥 소리
//
// 씬에 빈 오브젝트로 배치하고 이 컴포넌트를 추가.
// Start()에서 LineRenderer와 Light를 자동 생성하므로 수동 세팅 불필요.

using System.Collections;
using UnityEngine;

namespace Arcpunk.Weather
{
    public class LightningVFX : MonoBehaviour
    {
        [Header("Visual")]
        [SerializeField] private int _segments = 10;
        [SerializeField] private float _jitter = 3f;        // 지그재그 흔들림 정도
        [SerializeField] private float _boltDuration = 0.12f; // 번개 표시 시간
        [SerializeField] private float _flashDuration = 0.3f; // 섬광 지속 시간
        [SerializeField] private float _flashIntensity = 6f;
        [SerializeField] private float _boltWidth = 0.3f;

        [Header("Audio")]
        [SerializeField] private AudioClip _thunderClip; // Inspector에서 할당 (없어도 작동)
        [SerializeField] private float _thunderDelay = 0.2f;

        // 런타임 생성 컴포넌트
        private LineRenderer _line;
        private Light _flash;
        private AudioSource _audio;

        private void Awake()
        {
            CreateComponents();
        }

        private void CreateComponents()
        {
            // ── LineRenderer 생성 ──
            GameObject lineObj = new GameObject("LightningBolt");
            lineObj.transform.SetParent(transform);
            _line = lineObj.AddComponent<LineRenderer>();
            _line.positionCount = 0;
            _line.startWidth = _boltWidth;
            _line.endWidth = _boltWidth * 0.3f;
            _line.numCapVertices = 2;
            _line.enabled = false;

            // Additive 머티리얼 생성 (순수 흰색, 빛나는 효과)
            Material boltMat = new Material(Shader.Find("Particles/Standard Unlit"));
            if (boltMat != null)
            {
                boltMat.SetFloat("_Mode", 1); // Additive
                boltMat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
                boltMat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.One);
                boltMat.SetColor("_Color", new Color(0.8f, 0.85f, 1f, 1f));
                boltMat.SetColor("_EmissionColor", new Color(0.8f, 0.85f, 1f, 1f));
            }
            else
            {
                // 폴백: 기본 Unlit 셰이더
                boltMat = new Material(Shader.Find("Unlit/Color"));
                boltMat.color = Color.white;
            }
            _line.material = boltMat;

            // Gradient: 중앙이 밝고 양 끝이 투명
            Gradient gradient = new Gradient();
            gradient.SetKeys(
                new[] {
                    new GradientColorKey(new Color(0.9f, 0.9f, 1f), 0f),
                    new GradientColorKey(Color.white, 0.3f),
                    new GradientColorKey(new Color(0.7f, 0.8f, 1f), 1f),
                },
                new[] {
                    new GradientAlphaKey(0.6f, 0f),
                    new GradientAlphaKey(1f, 0.2f),
                    new GradientAlphaKey(0.3f, 1f),
                }
            );
            _line.colorGradient = gradient;

            // ── Point Light 생성 ──
            GameObject lightObj = new GameObject("LightningFlash");
            lightObj.transform.SetParent(transform);
            _flash = lightObj.AddComponent<Light>();
            _flash.type = LightType.Point;
            _flash.range = 60f;
            _flash.color = new Color(0.85f, 0.9f, 1f);
            _flash.intensity = 0;
            _flash.shadows = LightShadows.None; // 성능상 그림자 끔

            // ── AudioSource 생성 ──
            _audio = gameObject.AddComponent<AudioSource>();
            _audio.spatialBlend = 0.7f; // 반 3D
            _audio.rolloffMode = AudioRolloffMode.Linear;
            _audio.maxDistance = 200f;
            _audio.playOnAwake = false;
        }

        /// <summary>번개를 발생시킨다. 하늘 → 타격 지점.</summary>
        public void Strike(Vector3 from, Vector3 to)
        {
            StartCoroutine(StrikeRoutine(from, to));
        }

        private IEnumerator StrikeRoutine(Vector3 from, Vector3 to)
        {
            // ── 1. 지그재그 경로 생성 ──
            _line.positionCount = _segments;
            for (int i = 0; i < _segments; i++)
            {
                float t = i / (float)(_segments - 1);
                Vector3 pos = Vector3.Lerp(from, to, t);

                // 양 끝점 제외하고 랜덤 오프셋
                if (i > 0 && i < _segments - 1)
                {
                    pos.x += Random.Range(-_jitter, _jitter);
                    pos.z += Random.Range(-_jitter, _jitter);
                    // Y는 덜 흔들림 (수직 방향이니까)
                    pos.y += Random.Range(-_jitter * 0.3f, _jitter * 0.3f);
                }

                _line.SetPosition(i, pos);
            }

            // ── 2. 번개 표시 ──
            _line.enabled = true;

            // ── 3. 섬광 ──
            _flash.transform.position = to;
            _flash.intensity = _flashIntensity;

            // ── 4. 천둥 소리 (약간의 딜레이 — 빛이 먼저) ──
            if (_thunderClip != null)
            {
                _audio.transform.position = to;
                _audio.clip = _thunderClip;
                _audio.PlayDelayed(_thunderDelay);
            }

            // ── 5. 번개 깜빡임 (1~2회) ──
            yield return new WaitForSeconds(_boltDuration);
            _line.enabled = false;

            // 짧은 재번쩍 (50% 확률)
            if (Random.value > 0.5f)
            {
                yield return new WaitForSeconds(0.05f);

                // 약간 다른 경로로 재생성
                for (int i = 1; i < _segments - 1; i++)
                {
                    Vector3 pos = _line.GetPosition(i);
                    pos.x += Random.Range(-1f, 1f);
                    pos.z += Random.Range(-1f, 1f);
                    _line.SetPosition(i, pos);
                }

                _line.enabled = true;
                yield return new WaitForSeconds(_boltDuration * 0.5f);
                _line.enabled = false;
            }

            // ── 6. 섬광 감쇠 ──
            float fadeTimer = _flashDuration;
            while (fadeTimer > 0)
            {
                fadeTimer -= Time.deltaTime;
                _flash.intensity = (fadeTimer / _flashDuration) * _flashIntensity;
                yield return null;
            }
            _flash.intensity = 0;
        }
    }
}
