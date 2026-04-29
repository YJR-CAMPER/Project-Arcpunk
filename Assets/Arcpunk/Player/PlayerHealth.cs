// ── PlayerHealth.cs ──
// 플레이어 체력 시스템.
// [추가] 피격 넉백, 화면 빨간 비네트, 게임오버 연출.

using UnityEngine;

namespace Arcpunk.Player
{
    public class PlayerHealth : MonoBehaviour
    {
        [Header("Settings")]
        [SerializeField] private float _maxHealth = 100f;

        [Header("Knockback")]
        [SerializeField] private float _knockbackForce = 6f;
        [SerializeField] private float _knockbackUpForce = 3f;

        [Header("Damage Vignette")]
        [SerializeField] private float _flashDuration = 0.4f;
        [SerializeField] private float _lowHealthThreshold = 0.3f; // 30% 이하에서 지속 비네트

        public float MaxHealth => _maxHealth;
        public float CurrentHealth { get; private set; }
        public bool IsDead => CurrentHealth <= 0;

        // 피격 시각 피드백
        private float _damageFlashTimer;
        private float _damageFlashIntensity;

        // 비네트 UI (런타임 생성)
        private UnityEngine.UI.Image _vignetteImage;
        private Canvas _vignetteCanvas;

        // 이벤트
        public event System.Action OnPlayerDeath;
        public event System.Action<float> OnDamageTaken; // amount

        private void Start()
        {
            CurrentHealth = _maxHealth;
            CreateVignetteUI();
        }

        /// <summary>데미지 + 넉백. attacker 위치에서 밀려남.</summary>
        public void TakeDamage(float amount, Vector3 attackerPosition)
        {
            if (IsDead) return;

            CurrentHealth = Mathf.Max(0, CurrentHealth - amount);

            // 넉백 적용
            ApplyKnockback(attackerPosition);

            // 비네트 플래시
            _damageFlashTimer = _flashDuration;
            _damageFlashIntensity = Mathf.Clamp01(amount / 30f); // 강한 공격일수록 진한 플래시

            OnDamageTaken?.Invoke(amount);

            Debug.Log($"[Player] Took {amount} damage. HP: {CurrentHealth}/{_maxHealth}");

            if (IsDead)
                OnDeath();
        }

        /// <summary>기존 호환용 오버로드 (공격자 위치 없이).</summary>
        public void TakeDamage(float amount)
        {
            // 공격자 위치를 모르면 뒤로 넉백
            Vector3 behindPlayer = transform.position - transform.forward * 2f;
            TakeDamage(amount, behindPlayer);
        }

        public void Heal(float amount)
        {
            if (IsDead) return;
            CurrentHealth = Mathf.Min(_maxHealth, CurrentHealth + amount);
        }

        private void ApplyKnockback(Vector3 attackerPosition)
        {
            var controller = PlayerController.Instance;
            if (controller == null) return;

            // 공격자 → 플레이어 방향으로 밀림
            Vector3 dir = (transform.position - attackerPosition).normalized;
            dir.y = 0;

            Vector3 knockback = dir * _knockbackForce + Vector3.up * _knockbackUpForce;
            controller.AddExternalVelocity(knockback);
        }

        private void OnDeath()
        {
            Debug.Log("[Player] DEAD!");
            OnPlayerDeath?.Invoke();

            // 게임오버 UI 표시
            var gameOverUI = UI.GameOverUI.Instance;
            if (gameOverUI != null)
                gameOverUI.Show();

            // 커서 해제
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }

        /// <summary>리스폰 처리.</summary>
        public void Respawn()
        {
            CurrentHealth = _maxHealth;
            _damageFlashTimer = 0;

            var world = Voxel.VoxelWorld.Instance;
            if (world != null)
            {
                var cc = GetComponent<CharacterController>();
                if (cc != null) cc.enabled = false;
                transform.position = world.GetSpawnPosition();
                if (cc != null) cc.enabled = true;
            }

            // 커서 잠금 복원
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }

        private void Update()
        {
            UpdateVignette();
        }

        // ═══════════════════════════════════════
        // 비네트 UI
        // ═══════════════════════════════════════

        private void CreateVignetteUI()
        {
            // 비네트 전용 캔버스 (최상단)
            var go = new GameObject("DamageVignette");
            go.transform.SetParent(transform);
            _vignetteCanvas = go.AddComponent<Canvas>();
            _vignetteCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
            _vignetteCanvas.sortingOrder = 999;

            // Raycast 차단 안 함
            var raycaster = go.GetComponent<UnityEngine.UI.GraphicRaycaster>();
            if (raycaster != null) Destroy(raycaster);

            // 비네트 이미지 (화면 전체 덮음)
            var imgGo = new GameObject("VignetteImage");
            imgGo.transform.SetParent(go.transform);

            _vignetteImage = imgGo.AddComponent<UnityEngine.UI.Image>();
            _vignetteImage.color = new Color(0.8f, 0, 0, 0);
            _vignetteImage.raycastTarget = false;

            // 화면 전체 채움
            var rect = _vignetteImage.rectTransform;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;

            // CanvasGroup으로 Raycast 완전 차단 해제
            var cg = go.AddComponent<CanvasGroup>();
            cg.blocksRaycasts = false;
            cg.interactable = false;
        }

        private void UpdateVignette()
        {
            if (_vignetteImage == null) return;

            float alpha = 0f;

            // 피격 플래시
            if (_damageFlashTimer > 0)
            {
                _damageFlashTimer -= Time.deltaTime;
                float t = _damageFlashTimer / _flashDuration;
                alpha = t * _damageFlashIntensity * 0.5f;
            }

            // 저체력 지속 비네트
            if (!IsDead)
            {
                float healthRatio = CurrentHealth / _maxHealth;
                if (healthRatio < _lowHealthThreshold)
                {
                    // 체력이 낮을수록 진하게 + 맥박 효과
                    float severity = 1f - (healthRatio / _lowHealthThreshold);
                    float pulse = (Mathf.Sin(Time.time * 3f) + 1f) * 0.5f;
                    float lowAlpha = severity * 0.25f * (0.6f + pulse * 0.4f);
                    alpha = Mathf.Max(alpha, lowAlpha);
                }
            }

            // 사망 시 어둡게
            if (IsDead)
                alpha = 0.6f;

            _vignetteImage.color = new Color(0.7f, 0, 0, alpha);
        }

        // ── UI용 프로퍼티 ──
        public float HealthRatio => CurrentHealth / _maxHealth;
        public bool IsFlashing => _damageFlashTimer > 0;
    }
}