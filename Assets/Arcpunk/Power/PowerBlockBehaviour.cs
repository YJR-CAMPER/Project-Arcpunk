// ── PowerBlockBehaviour.cs ──
// 전력 블록의 런타임 시각 피드백을 관리.
// VoxelWorld의 블록은 정적 데이터일 뿐이므로, 전력 블록이 설치되면
// 이 시스템이 해당 좌표에 Light 컴포넌트 등을 동적으로 추가/제거한다.
//
// VoxelWorld.SetBlock() 호출 시 → 이 시스템이 전력 블록 감지 → 시각 요소 생성.

using System.Collections.Generic;
using UnityEngine;
using Arcpunk.Voxel;

namespace Arcpunk.Power
{
    public class PowerBlockBehaviour : MonoBehaviour
    {
        public static PowerBlockBehaviour Instance { get; private set; }

        // 전력 블록 좌표 → 생성된 시각 오브젝트
        private Dictionary<Vector3Int, GameObject> _visualObjects = new();

        private VoxelWorld _world;

        private void Awake()
        {
            Instance = this;
        }

        private void Start()
        {
            _world = VoxelWorld.Instance;

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

        /// <summary>전력 블록이 설치되었을 때 호출. 시각 요소 생성.</summary>
        public void OnPowerBlockPlaced(Vector3Int worldPos, BlockType type)
        {
            ref BlockDef def = ref BlockData.Defs[(ushort)type];

            if (!def.IsPowerBlock) return;

            // 기존 시각 오브젝트가 있으면 제거
            RemoveVisual(worldPos);

            // 조명 블록이면 Point Light 생성
            if (def.EmitsLight)
            {
                GameObject lightObj = new GameObject($"PowerLight_{worldPos}");
                lightObj.transform.position = new Vector3(
                    worldPos.x + 0.5f, worldPos.y + 1.1f, worldPos.z + 0.5f);
                lightObj.transform.SetParent(transform);

                Light pointLight = lightObj.AddComponent<Light>();
                pointLight.type = LightType.Point;
                pointLight.range = def.LightLevel * 1.5f;
                pointLight.color = new Color(1f, 0.9f, 0.6f); // 따뜻한 빛
                pointLight.intensity = 0; // 초기에는 꺼짐 — 전력 공급 시 켜짐

                _visualObjects[worldPos] = lightObj;
            }
        }

        /// <summary>블록이 파괴되었을 때 호출. 시각 요소 제거.</summary>
        public void OnPowerBlockRemoved(Vector3Int worldPos)
        {
            RemoveVisual(worldPos);
        }

        private void RemoveVisual(Vector3Int pos)
        {
            if (_visualObjects.TryGetValue(pos, out GameObject obj))
            {
                Destroy(obj);
                _visualObjects.Remove(pos);
            }
        }

        /// <summary>매 틱 전력 상태에 따라 시각 업데이트.</summary>
        private void OnTick(int tickNumber)
        {
            var powerSystem = VoxelPowerSystem.Instance;
            if (powerSystem == null) return;

            foreach (var kvp in _visualObjects)
            {
                Vector3Int pos = kvp.Key;
                GameObject obj = kvp.Value;
                if (obj == null) continue;

                bool powered = powerSystem.IsBlockPowered(pos);
                Light light = obj.GetComponent<Light>();

                if (light != null)
                {
                    // 전력 있으면 켜기, 없으면 끄기
                    float targetIntensity = powered ? 2f : 0f;
                    light.intensity = Mathf.Lerp(light.intensity, targetIntensity,
                        Time.deltaTime * 8f);
                    var stimMgr = Ghoul.StimulusManager.Instance;
                    if (stimMgr != null)
                    {
                        if (powered)
                        {
                            stimMgr.RegisterContinuous(pos, new Ghoul.Stimulus
                            {
                                Type = Ghoul.StimulusType.Light,
                                Origin = new Vector3(pos.x + 0.5f, pos.y + 0.5f, pos.z + 0.5f),
                                Intensity = 15f,
                            });
                        }
                        else
                        {
                            stimMgr.RemoveContinuous(pos);
                        }
                    }
                }
            }
        }
    }
}
