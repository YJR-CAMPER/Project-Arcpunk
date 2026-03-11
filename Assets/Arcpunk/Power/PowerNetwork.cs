// ── PowerNetwork.cs ──
// BFS로 발견된 연결 블록들의 집합.
// 매 틱: 발전 → 충전 → 소비.

using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Arcpunk.Voxel;
using Arcpunk.Weather;

namespace Arcpunk.Power
{
    public class PowerNetwork
    {
        public int Id { get; }

        private List<Vector3Int> _producers = new();  // 피뢰침
        private List<Vector3Int> _storages = new();   // 배터리
        private List<ConsumerEntry> _consumers = new(); // 조명, 센트리 등

        private Dictionary<Vector3Int, float> _batteryCharge = new();
        private HashSet<Vector3Int> _poweredConsumers = new();

        // 즉시 충전 버퍼 (낙뢰 직격 보너스)
        private float _instantPowerBuffer;

        public float TotalStored => _batteryCharge.Values.Sum();
        public float TotalCapacity => _storages.Sum(pos =>
            BlockData.Defs[(ushort)VoxelWorld.Instance.GetBlock(pos.x, pos.y, pos.z)]
                .BatteryCapacity);

        private struct ConsumerEntry
        {
            public Vector3Int Pos;
            public float Draw;
            public PowerRole Role;
        }

        public PowerNetwork(int id) => Id = id;

        public void AddBlock(Vector3Int pos, ref BlockDef def)
        {
            switch (def.PowerRole)
            {
                case PowerRole.Producer:
                    _producers.Add(pos);
                    break;

                case PowerRole.Storage:
                    _storages.Add(pos);
                    if (!_batteryCharge.ContainsKey(pos))
                        _batteryCharge[pos] = 0f;
                    break;

                case PowerRole.Consumer:
                    _consumers.Add(new ConsumerEntry
                    {
                        Pos = pos,
                        Draw = def.PowerDraw,
                        Role = def.PowerRole,
                    });
                    break;

                case PowerRole.Conductor:
                    // 전도만 함 — 네트워크 연결 역할
                    break;
            }
        }

        public bool IsConsumerPowered(Vector3Int pos) => _poweredConsumers.Contains(pos);

        public void AddInstantPower(float amount)
        {
            _instantPowerBuffer += amount;
        }

        public void Tick(WeatherState weather, VoxelWorld world)
        {
            _poweredConsumers.Clear();

            // ═══════════════════════════════════
            // Phase 1: 발전
            // ═══════════════════════════════════
            float generated = 0f;

            foreach (var pos in _producers)
            {
                BlockType bt = world.GetBlock(pos.x, pos.y, pos.z);
                ref BlockDef def = ref BlockData.Defs[(ushort)bt];

                // 높이 보정: 높을수록 효율 증가
                float heightMult = 1f + pos.y * 0.05f;

                // 서지 배율
                float surgeOutput = def.BaseOutput * heightMult * weather.SurgeMultiplier;
                generated += surgeOutput;
            }

            // 낙뢰 직격 보너스 추가
            generated += _instantPowerBuffer;
            _instantPowerBuffer = 0;

            // ═══════════════════════════════════
            // Phase 2: 잉여 전력 → 배터리 충전
            // ═══════════════════════════════════
            float surplus = generated;

            foreach (var pos in _storages)
            {
                if (surplus <= 0) break;

                BlockType bt = world.GetBlock(pos.x, pos.y, pos.z);
                float capacity = BlockData.Defs[(ushort)bt].BatteryCapacity;
                float current = _batteryCharge.GetValueOrDefault(pos, 0f);
                float space = capacity - current;
                float toCharge = Mathf.Min(surplus, space);

                _batteryCharge[pos] = current + toCharge;
                surplus -= toCharge;
            }

            // ═══════════════════════════════════
            // Phase 3: 소비자에 전력 배분
            // ═══════════════════════════════════
            // 잉여 발전 + 배터리 방전으로 소비자에게 배분
            // 우선순위: 센트리(Sentry) > 조명(Light) > 전기로(Furnace)
            float available = surplus; // 발전 잉여

            // 우선순위 정렬 (PowerDraw가 높은 것 = 더 중요한 것 먼저)
            // 센트리(5) > 전기로(8) > 조명(2) → Draw 기반이 아니라 블록 타입 기반이 나을 수 있지만,
            // 프로토타입에서는 Draw 역순으로 단순화
            var sortedConsumers = _consumers
                .OrderByDescending(c => c.Draw)
                .ToList();

            foreach (var consumer in sortedConsumers)
            {
                if (available >= consumer.Draw)
                {
                    // 발전 잉여로 충당
                    available -= consumer.Draw;
                    _poweredConsumers.Add(consumer.Pos);
                }
                else
                {
                    // 배터리에서 방전 시도
                    float needed = consumer.Draw - available;
                    float discharged = DischargeFromBatteries(needed);

                    if (available + discharged >= consumer.Draw)
                    {
                        available = available + discharged - consumer.Draw;
                        _poweredConsumers.Add(consumer.Pos);
                    }
                    else
                    {
                        available += discharged; // 부족 — 이 소비자는 전력 못 받음
                    }
                }
            }
        }

        private float DischargeFromBatteries(float amount)
        {
            float discharged = 0;

            foreach (var pos in _storages)
            {
                float remaining = amount - discharged;
                if (remaining <= 0) break;

                float current = _batteryCharge.GetValueOrDefault(pos, 0f);
                float take = Mathf.Min(remaining, current);
                _batteryCharge[pos] = current - take;
                discharged += take;
            }

            return discharged;
        }
    }
}
