// ── SentryBehaviour.cs ──
// 센트리 블록이 전력 공급받으면 자동으로 가장 가까운 구울을 사격.
// PowerBlockBehaviour와 연동하여, Sentry 타입 블록이 설치되면 활성화.
// TickSystem에 구독하여 매 틱 타겟팅 + 발사.

using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Arcpunk.Voxel;
using Arcpunk.Power;
using Arcpunk.Ghoul;

namespace Arcpunk.Combat
{
    public class SentryBehaviour : MonoBehaviour
    {
        public static SentryBehaviour Instance { get; private set; }

        [Header("Settings")]
        [SerializeField] private float _detectRange = 20f;
        [SerializeField] private float _damage = 15f;
        [SerializeField] private int _ticksPerShot = 3; // 몇 틱마다 발사

        // 활성 센트리 좌표 목록
        private HashSet<Vector3Int> _sentryPositions = new();
        // 센트리 발사 이펙트용 LineRenderer 풀
        private Dictionary<Vector3Int, LineRenderer> _sentryLines = new();

        private int _tickCounter;

        private void Awake()
        {
            Instance = this;
        }

        private void Start()
        {
            var tick = Core.TickSystem.Instance;
            if (tick != null)
                tick.OnTick += OnTick;
        }

        private void OnDestroy()
        {
            var tick = Core.TickSystem.Instance;
            if (tick != null)
                tick.OnTick -= OnTick;
        }

        /// <summary>센트리 블록이 설치되었을 때 호출.</summary>
        public void RegisterSentry(Vector3Int pos)
        {
            _sentryPositions.Add(pos);

            // 발사 이펙트용 LineRenderer 생성
            if (!_sentryLines.ContainsKey(pos))
            {
                GameObject lineObj = new GameObject($"SentryBeam_{pos}");
                lineObj.transform.SetParent(transform);
                var lr = lineObj.AddComponent<LineRenderer>();
                lr.startWidth = 0.06f;
                lr.endWidth = 0.02f;
                lr.positionCount = 0;
                lr.enabled = false;

                Material mat = new Material(Shader.Find("Particles/Standard Unlit"));
                if (mat != null)
                {
                    mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
                    mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.One);
                    mat.SetColor("_Color", new Color(1f, 0.3f, 0.2f, 0.8f));
                }
                lr.material = mat;

                _sentryLines[pos] = lr;
            }
        }

        /// <summary>센트리 블록이 제거되었을 때 호출.</summary>
        public void UnregisterSentry(Vector3Int pos)
        {
            _sentryPositions.Remove(pos);
            if (_sentryLines.TryGetValue(pos, out var lr))
            {
                if (lr != null) Destroy(lr.gameObject);
                _sentryLines.Remove(pos);
            }
        }

        private void OnTick(int tickNumber)
        {
            _tickCounter++;
            if (_tickCounter < _ticksPerShot) return;
            _tickCounter = 0;

            var powerSys = VoxelPowerSystem.Instance;
            if (powerSys == null) return;

            // 모든 활성 구울 수집
            var ghouls = FindObjectsOfType<SimpleGhoul>();

            foreach (var sentryPos in _sentryPositions)
            {
                // 전력 체크
                if (!powerSys.IsBlockPowered(sentryPos))
                    continue;

                Vector3 sentryWorldPos = new Vector3(
                    sentryPos.x + 0.5f, sentryPos.y + 0.5f, sentryPos.z + 0.5f);

                // 가장 가까운 구울 찾기
                SimpleGhoul closest = null;
                float closestDist = _detectRange;

                foreach (var ghoul in ghouls)
                {
                    if (!ghoul.IsAlive) continue;
                    float dist = Vector3.Distance(sentryWorldPos, ghoul.transform.position);
                    if (dist < closestDist)
                    {
                        closestDist = dist;
                        closest = ghoul;
                    }
                }

                if (closest == null) continue;

                // 발사!
                closest.TakeDamage(_damage);

                // 자극 발생 (센트리 사격 소리)
                StimulusManager.Instance?.OnWeaponFired(sentryWorldPos, 30f);

                // 시각 이펙트
                if (_sentryLines.TryGetValue(sentryPos, out var lr) && lr != null)
                {
                    StartCoroutine(SentryBeamVFX(lr, sentryWorldPos,
                        closest.transform.position + Vector3.up * 0.9f));
                }
            }
        }

        private IEnumerator SentryBeamVFX(LineRenderer lr, Vector3 from, Vector3 to)
        {
            lr.positionCount = 2;
            lr.SetPosition(0, from);
            lr.SetPosition(1, to);
            lr.enabled = true;

            yield return new WaitForSeconds(0.08f);
            lr.enabled = false;
        }
    }
}
