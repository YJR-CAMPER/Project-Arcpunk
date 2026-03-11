// ── TickSystem.cs ──
// 고정 간격 시뮬레이션 틱. 전력/날씨/스폰의 심장박동.
// Update()와 독립적으로 0.5초마다 OnTick 이벤트를 발생시킨다.
// GameBootstrap 또는 VoxelWorld에서 초기화.

using System;
using UnityEngine;

namespace Arcpunk.Core
{
    public class TickSystem : MonoBehaviour
    {
        public static TickSystem Instance { get; private set; }

        [Header("Settings")]
        [SerializeField] private float _tickInterval = 0.5f;

        /// <summary>매 틱마다 발생. int 파라미터는 현재 틱 번호.</summary>
        public event Action<int> OnTick;

        public int CurrentTick { get; private set; }
        public float TickInterval => _tickInterval;

        private float _timer;

        private void Awake()
        {
            Instance = this;
        }

        private void Update()
        {
            _timer += Time.deltaTime;

            while (_timer >= _tickInterval)
            {
                _timer -= _tickInterval;
                CurrentTick++;
                OnTick?.Invoke(CurrentTick);
            }
        }

        /// <summary>다음 틱까지 남은 시간 비율 (0~1). UI 부드러운 보간에 사용.</summary>
        public float TickProgress => _timer / _tickInterval;
    }
}
