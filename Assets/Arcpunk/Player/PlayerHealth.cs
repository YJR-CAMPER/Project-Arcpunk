// ── PlayerHealth.cs ──
// 플레이어 체력 시스템. 구울 공격 시 데미지, 사망 처리.
// Player 오브젝트에 부착.

using UnityEngine;

namespace Arcpunk.Player
{
    public class PlayerHealth : MonoBehaviour
    {
        [Header("Settings")]
        [SerializeField] private float _maxHealth = 100f;

        public float MaxHealth => _maxHealth;
        public float CurrentHealth { get; private set; }
        public bool IsDead => CurrentHealth <= 0;

        // 피격 시각 피드백용
        private float _damageFlashTimer;

        private void Start()
        {
            CurrentHealth = _maxHealth;
        }

        public void TakeDamage(float amount)
        {
            if (IsDead) return;

            CurrentHealth = Mathf.Max(0, CurrentHealth - amount);
            _damageFlashTimer = 0.3f;

            Debug.Log($"[Player] Took {amount} damage. HP: {CurrentHealth}/{_maxHealth}");

            if (IsDead)
                OnDeath();
        }

        public void Heal(float amount)
        {
            CurrentHealth = Mathf.Min(_maxHealth, CurrentHealth + amount);
        }

        private void OnDeath()
        {
            Debug.Log("[Player] DEAD! Press R to respawn.");
        }

        private void Update()
        {
            // R키로 리스폰 (디버그)
            if (IsDead && Input.GetKeyDown(KeyCode.R))
            {
                CurrentHealth = _maxHealth;
                var world = Voxel.VoxelWorld.Instance;
                if (world != null)
                    transform.position = world.GetSpawnPosition();
            }

            if (_damageFlashTimer > 0)
                _damageFlashTimer -= Time.deltaTime;
        }

        // UI용
        public float HealthRatio => CurrentHealth / _maxHealth;
        public bool IsFlashing => _damageFlashTimer > 0;
    }
}
