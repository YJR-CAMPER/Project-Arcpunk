// ── LightningSystem.cs ──
// 낙뢰 타겟팅. WeatherSystem이 OnLightningRequested를 발생시키면,
// 가장 높은 피뢰침을 가중 확률로 선택하여 낙뢰를 떨어뜨린다.
// 피뢰침이 없으면 월드 랜덤 위치에 낙뢰.

using System.Collections.Generic;
using UnityEngine;

namespace Arcpunk.Weather
{
    public class LightningSystem : MonoBehaviour
    {
        public static LightningSystem Instance { get; private set; }

        [Header("References")]
        [SerializeField] private LightningVFX _lightningVFX;

        [Header("Settings")]
        [SerializeField] private float _skyHeight = 80f; // 번개 시작 높이

        /// <summary>낙뢰가 특정 월드 좌표에 떨어졌을 때 발생. 전력 시스템 + 자극 시스템이 구독.</summary>
        public event System.Action<Vector3> OnLightningStrike;

        private void Awake()
        {
            Instance = this;
        }

        private void Start()
        {
            var weather = WeatherSystem.Instance;
            if (weather != null)
                weather.OnLightningRequested += HandleLightningRequest;
        }

        private void OnDestroy()
        {
            var weather = WeatherSystem.Instance;
            if (weather != null)
                weather.OnLightningRequested -= HandleLightningRequest;
        }

        private void HandleLightningRequest()
        {
            // VoxelPowerSystem에서 모든 피뢰침 좌표를 가져온다
            var rods = Power.VoxelPowerSystem.Instance?.GetAllProducerPositions();

            Vector3 strikeWorldPos;

            if (rods != null && rods.Count > 0)
            {
                // 높이 기반 가중 랜덤 선택
                Vector3Int chosen = ChooseRodByHeight(rods);
                strikeWorldPos = new Vector3(
                    chosen.x + 0.5f,
                    chosen.y + 0.5f,
                    chosen.z + 0.5f
                );
            }
            else
            {
                // 피뢰침 없으면 랜덤 위치
                strikeWorldPos = GetRandomSurfacePosition();
            }

            // 시각 효과
            Vector3 skyPos = new Vector3(
                strikeWorldPos.x + Random.Range(-3f, 3f),
                _skyHeight,
                strikeWorldPos.z + Random.Range(-3f, 3f)
            );

            if (_lightningVFX != null)
                _lightningVFX.Strike(skyPos, strikeWorldPos);

            // 이벤트 발생 → 전력 시스템, 자극 시스템이 처리
            OnLightningStrike?.Invoke(strikeWorldPos);
        }

        private Vector3Int ChooseRodByHeight(List<Vector3Int> rods)
        {
            // 높이 기반 가중치: 높을수록 맞을 확률 높음
            float totalWeight = 0f;
            foreach (var rod in rods)
                totalWeight += Mathf.Max(1f, rod.y); // 최소 가중치 1

            float roll = Random.value * totalWeight;
            float cumulative = 0f;

            foreach (var rod in rods)
            {
                cumulative += Mathf.Max(1f, rod.y);
                if (roll <= cumulative)
                    return rod;
            }

            return rods[rods.Count - 1]; // 폴백
        }

        private Vector3 GetRandomSurfacePosition()
        {
            var world = Voxel.VoxelWorld.Instance;
            int maxX = world.WorldSizeX * Voxel.Chunk.SIZE;
            int maxZ = world.WorldSizeZ * Voxel.Chunk.SIZE;

            int rx = Random.Range(0, maxX);
            int rz = Random.Range(0, maxZ);
            int surfaceY = world.GetSurfaceY(rx, rz);
            if (surfaceY < 0) surfaceY = 30;

            return new Vector3(rx + 0.5f, surfaceY + 1f, rz + 0.5f);
        }
    }
}
