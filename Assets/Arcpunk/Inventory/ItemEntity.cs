// ── ItemEntity.cs ──
// 월드에 떠다니는 드롭 아이템.
// 블록 파괴 시 스폰, 플레이어 접근 시 자동 수집.
// 작은 큐브 + 회전 + 위아래 부유.

using UnityEngine;
using Arcpunk.Inventory;

namespace Arcpunk.Inventory
{
    public class ItemEntity : MonoBehaviour
    {
        public ItemType ItemType { get; private set; }
        public int Count { get; private set; }

        private float _lifetime = 300f; // 5분
        private float _pickupDelay = 0.5f;
        private float _pickupRange = 2f;
        private float _magnetRange = 4f; // 이 거리 안이면 플레이어 쪽으로 끌려옴
        private float _magnetSpeed = 8f;

        private Vector3 _velocity;
        private float _spawnTime;
        private float _bobPhase;
        private Renderer _renderer;

        public static ItemEntity Spawn(Vector3 position, ItemType type, int count)
        {
            // 작은 큐브 생성
            GameObject obj = GameObject.CreatePrimitive(PrimitiveType.Cube);
            obj.name = $"Item_{ItemDatabase.Get(type).Name}";
            obj.transform.position = position;
            obj.transform.localScale = new Vector3(0.3f, 0.3f, 0.3f);

            // 물리 충돌 제거 (자체 이동 로직 사용)
            var collider = obj.GetComponent<BoxCollider>();
            if (collider != null) Object.Destroy(collider);

            // 색상 (아이콘 인덱스 기반으로 대략적 색상)
            var renderer = obj.GetComponent<Renderer>();
            if (renderer != null)
            {
                renderer.material = new Material(Shader.Find("Standard"));
                renderer.material.color = GetItemColor(type);
                renderer.material.EnableKeyword("_EMISSION");
                renderer.material.SetColor("_EmissionColor",
                    GetItemColor(type) * 0.3f);
            }

            var entity = obj.AddComponent<ItemEntity>();
            entity.ItemType = type;
            entity.Count = count;
            entity._spawnTime = Time.time;
            entity._bobPhase = Random.Range(0f, Mathf.PI * 2);
            entity._renderer = renderer;

            // 랜덤 방향으로 튀어나감
            entity._velocity = new Vector3(
                Random.Range(-2f, 2f),
                Random.Range(3f, 5f),
                Random.Range(-2f, 2f)
            );

            return entity;
        }

        private void Update()
        {
            // 수명 체크
            if (Time.time - _spawnTime > _lifetime)
            {
                Destroy(gameObject);
                return;
            }

            // 픽업 딜레이
            if (Time.time - _spawnTime < _pickupDelay)
            {
                ApplyPhysics();
                return;
            }

            // 플레이어와의 거리 체크
            var player = Player.PlayerController.Instance;
            if (player != null)
            {
                float dist = Vector3.Distance(
                    transform.position, player.transform.position);

                // 자석 효과 (가까이 오면 빨려감)
                if (dist < _magnetRange)
                {
                    Vector3 dir = (player.transform.position - transform.position).normalized;
                    float speed = _magnetSpeed * (1f - dist / _magnetRange);
                    transform.position += dir * speed * Time.deltaTime;
                }

                // 수집
                if (dist < _pickupRange)
                {
                    TryPickup();
                }
            }

            ApplyPhysics();

            // 회전 + 부유
            transform.Rotate(0, 90f * Time.deltaTime, 0);
            float bob = Mathf.Sin((Time.time + _bobPhase) * 2f) * 0.1f;
            transform.position += new Vector3(0, bob * Time.deltaTime, 0);
        }

        private void ApplyPhysics()
        {
            // 간단한 중력 + 지면 체크
            _velocity.y -= 15f * Time.deltaTime;

            Vector3 nextPos = transform.position + _velocity * Time.deltaTime;

            // 복셀 지면 체크
            var world = Voxel.VoxelWorld.Instance;
            if (world != null)
            {
                Vector3Int blockBelow = new Vector3Int(
                    Mathf.FloorToInt(nextPos.x),
                    Mathf.FloorToInt(nextPos.y - 0.2f),
                    Mathf.FloorToInt(nextPos.z)
                );

                if (Voxel.BlockData.IsSolid(world.GetBlock(
                    blockBelow.x, blockBelow.y, blockBelow.z)))
                {
                    // 지면에 착지
                    nextPos.y = blockBelow.y + 1.2f;
                    _velocity.y = 0;
                    _velocity.x *= 0.8f; // 마찰
                    _velocity.z *= 0.8f;
                }
            }

            transform.position = nextPos;
        }

        private void TryPickup()
        {
            var inv = PlayerInventory.Instance;
            if (inv == null) return;

            int remaining = inv.AddItem(ItemType, Count);

            if (remaining == 0)
            {
                // 전부 수집됨
                Destroy(gameObject);
            }
            else
            {
                // 일부만 수집 (인벤토리 가득)
                Count = remaining;
            }
        }

        private static Color GetItemColor(ItemType type)
        {
            // 아이템 타입별 대략적 색상
            return type switch
            {
                ItemType.Stone or ItemType.StoneChip or ItemType.StoneBrick
                    => new Color(0.6f, 0.6f, 0.6f),
                ItemType.Dirt => new Color(0.5f, 0.35f, 0.2f),
                ItemType.DeadWood or ItemType.Stick
                    => new Color(0.4f, 0.3f, 0.15f),
                ItemType.CopperOre or ItemType.CopperNugget or ItemType.CopperIngot
                    or ItemType.CopperPlate or ItemType.CopperRod or ItemType.CopperWire
                    or ItemType.CopperBattery
                    => new Color(0.85f, 0.5f, 0.15f),
                ItemType.IronOre or ItemType.IronNugget or ItemType.IronIngot
                    or ItemType.IronPlate
                    => new Color(0.7f, 0.7f, 0.75f),
                ItemType.MushroomBlock or ItemType.Mushroom
                    => new Color(0.5f, 0.2f, 0.6f),
                ItemType.Light => new Color(1f, 0.9f, 0.4f),
                ItemType.Sentry => new Color(0.8f, 0.2f, 0.15f),
                _ => Color.white,
            };
        }
    }
}
