// ── SimpleGhoul.cs ──
// 마인크래프트 좀비 스타일 구울.
// NavMesh 없이 복셀 지형을 직접 탐색.
// 자극(빛/소리)에 반응하여 접근, 플레이어 직접 감지 시 추적.
//
// 상태: Idle → Chase → Attack (3상태, 단순하게)
// 모델: 빨간 캡슐 (프로토타입)

using UnityEngine;
using Arcpunk.Voxel;
using Arcpunk.Player;

namespace Arcpunk.Ghoul
{
    public class SimpleGhoul : MonoBehaviour
    {
        [Header("Stats")]
        public float MaxHealth = 50f;
        public float Damage = 8f;
        public float MoveSpeed = 2.5f;
        public float PerceptionRadius = 30f;
        public float AttackRange = 2.2f;
        public float AttackCooldown = 1.2f;
        public float DirectDetectRange = 12f; // 플레이어 직접 감지 거리

        [Header("Movement")]
        [SerializeField] private float _gravity = -15f;
        [SerializeField] private float _jumpForce = 6f;
        [SerializeField] private float _stepHeight = 1.2f;
        [SerializeField] private float _groundCheckDist = 0.3f;

        // 런타임 상태
        public float Health { get; private set; }
        private Vector3 _velocity;
        private float _attackTimer;
        private GhoulState _state = GhoulState.Idle;
        private Vector3 _targetPosition;
        private float _idleTimer;
        private float _stuckTimer;
        private Vector3 _lastPosition;

        // 컴포넌트
        private CharacterController _cc;
        private Renderer _renderer;

        public enum GhoulState { Idle, Chase, Attack }

        private void Start()
        {
            Health = MaxHealth;

            // CharacterController 설정
            _cc = gameObject.AddComponent<CharacterController>();
            _cc.height = 1.8f;
            _cc.radius = 0.35f;
            _cc.center = new Vector3(0, 0.9f, 0);
            _cc.slopeLimit = 60f;
            _cc.stepOffset = _stepHeight;
            _cc.skinWidth = 0.05f;

            _lastPosition = transform.position;
        }

        private void Update()
        {
            if (Health <= 0) return;

            ApplyGravity();

            switch (_state)
            {
                case GhoulState.Idle:
                    UpdateIdle();
                    break;
                case GhoulState.Chase:
                    UpdateChase();
                    break;
                case GhoulState.Attack:
                    UpdateAttack();
                    break;
            }

            // 공격 쿨타임
            if (_attackTimer > 0)
                _attackTimer -= Time.deltaTime;

            // 스턱 감지
            CheckStuck();
        }

        // ═══════════════════════════════════════
        // 상태별 업데이트
        // ═══════════════════════════════════════

        private void UpdateIdle()
        {
            // 1. 자극 확인
            var stimulus = StimulusManager.Instance?.GetStrongest(
                transform.position, PerceptionRadius);

            if (stimulus.HasValue)
            {
                _targetPosition = stimulus.Value.Origin;
                _state = GhoulState.Chase;
                return;
            }

            // 2. 플레이어 직접 감지
            var player = PlayerController.Instance;
            if (player != null)
            {
                float dist = Vector3.Distance(transform.position, player.transform.position);
                if (dist < DirectDetectRange)
                {
                    _targetPosition = player.transform.position;
                    _state = GhoulState.Chase;
                    return;
                }
            }

            // 3. 배회
            _idleTimer -= Time.deltaTime;
            if (_idleTimer <= 0)
            {
                // 랜덤 방향으로 느리게 이동
                Vector2 rnd = Random.insideUnitCircle * 5f;
                _targetPosition = transform.position + new Vector3(rnd.x, 0, rnd.y);
                _idleTimer = Random.Range(3f, 6f);
            }

            MoveToward(_targetPosition, MoveSpeed * 0.3f);
        }

        private void UpdateChase()
        {
            var player = PlayerController.Instance;
            if (player == null)
            {
                _state = GhoulState.Idle;
                return;
            }

            float distToPlayer = Vector3.Distance(
                transform.position, player.transform.position);

            // 플레이어가 가까우면 직접 추적
            if (distToPlayer < DirectDetectRange)
            {
                _targetPosition = player.transform.position;
            }
            else
            {
                // 자극 재확인
                var stimulus = StimulusManager.Instance?.GetStrongest(
                    transform.position, PerceptionRadius);

                if (stimulus.HasValue)
                {
                    _targetPosition = stimulus.Value.Origin;
                }
                else
                {
                    // 자극도 없고 플레이어도 안 보이면 Idle로
                    _state = GhoulState.Idle;
                    return;
                }
            }

            // 공격 범위 진입 확인
            if (distToPlayer <= AttackRange)
            {
                _state = GhoulState.Attack;
                return;
            }

            MoveToward(_targetPosition, MoveSpeed);
        }

        private void UpdateAttack()
        {
            var player = PlayerController.Instance;
            if (player == null)
            {
                _state = GhoulState.Idle;
                return;
            }

            float dist = Vector3.Distance(
                transform.position, player.transform.position);

            // 공격 범위 밖이면 추적으로 복귀
            if (dist > AttackRange * 1.5f)
            {
                _state = GhoulState.Chase;
                return;
            }

            // 플레이어를 바라봄
            Vector3 lookDir = (player.transform.position - transform.position).normalized;
            lookDir.y = 0;
            if (lookDir.sqrMagnitude > 0.01f)
                transform.rotation = Quaternion.Slerp(
                    transform.rotation,
                    Quaternion.LookRotation(lookDir),
                    Time.deltaTime * 8f);

            // 공격
            if (_attackTimer <= 0 && dist <= AttackRange)
            {
                // 플레이어에게 데미지
                var playerHealth = player.GetComponent<PlayerHealth>();
                if (playerHealth != null)
                    playerHealth.TakeDamage(Damage);

                _attackTimer = AttackCooldown;

                // 공격 시각 피드백: 빨간색 깜빡
                StartCoroutine(AttackFlash());
            }
        }

        // ═══════════════════════════════════════
        // 이동 (마크 좀비 스타일)
        // ═══════════════════════════════════════

        private void MoveToward(Vector3 target, float speed)
        {
            Vector3 dir = (target - transform.position);
            dir.y = 0;

            if (dir.sqrMagnitude < 0.5f) return; // 도착

            dir = dir.normalized;

            // 타겟 방향으로 회전
            if (dir.sqrMagnitude > 0.01f)
                transform.rotation = Quaternion.Slerp(
                    transform.rotation,
                    Quaternion.LookRotation(dir),
                    Time.deltaTime * 5f);

            // 이동
            Vector3 move = dir * speed * Time.deltaTime;
            move.y = _velocity.y * Time.deltaTime;
            _cc.Move(move);
        }

        private void ApplyGravity()
        {
            if (_cc.isGrounded && _velocity.y < 0)
                _velocity.y = -1f;
            else
                _velocity.y += _gravity * Time.deltaTime;
        }

        private void CheckStuck()
        {
            float moved = Vector3.Distance(
                new Vector3(transform.position.x, 0, transform.position.z),
                new Vector3(_lastPosition.x, 0, _lastPosition.z));

            if (moved < 0.05f && _state == GhoulState.Chase)
            {
                _stuckTimer += Time.deltaTime;

                // 1초 이상 막혀있으면 점프 시도
                if (_stuckTimer > 1f && _cc.isGrounded)
                {
                    _velocity.y = _jumpForce;
                    _stuckTimer = 0;
                }
            }
            else
            {
                _stuckTimer = 0;
            }

            _lastPosition = transform.position;
        }

        // ═══════════════════════════════════════
        // 데미지
        // ═══════════════════════════════════════

        public void TakeDamage(float amount)
        {
            Health -= amount;

            // 피격 시각 피드백
            StartCoroutine(DamageFlash());

            if (Health <= 0)
                Die();
            else
            {
                // 피격 시 공격자 방향으로 추적 시작
                _state = GhoulState.Chase;
            }
        }

        private void Die()
        {
            // 간단한 사망: 크기 줄이면서 사라짐
            StartCoroutine(DeathRoutine());
        }

        // ═══════════════════════════════════════
        // 시각 피드백 코루틴
        // ═══════════════════════════════════════

        private System.Collections.IEnumerator DamageFlash()
        {
            if (_renderer == null) _renderer = GetComponentInChildren<Renderer>();
            if (_renderer == null) yield break;

            Color original = _renderer.material.color;
            _renderer.material.color = Color.white;
            yield return new WaitForSeconds(0.1f);
            _renderer.material.color = original;
        }

        private System.Collections.IEnumerator AttackFlash()
        {
            if (_renderer == null) _renderer = GetComponentInChildren<Renderer>();
            if (_renderer == null) yield break;

            Color original = _renderer.material.color;
            _renderer.material.color = new Color(1f, 0.3f, 0.3f);
            yield return new WaitForSeconds(0.15f);
            if (_renderer != null)
                _renderer.material.color = original;
        }

        private System.Collections.IEnumerator DeathRoutine()
        {
            // CharacterController 비활성화 (물리 간섭 방지)
            if (_cc != null) _cc.enabled = false;

            float t = 0.5f;
            Vector3 startScale = transform.localScale;
            while (t > 0)
            {
                t -= Time.deltaTime;
                transform.localScale = startScale * (t / 0.5f);
                transform.position += Vector3.down * Time.deltaTime * 2f;
                yield return null;
            }

            Destroy(gameObject);
        }

        // ═══════════════════════════════════════
        // 공개 API
        // ═══════════════════════════════════════

        public bool IsAlive => Health > 0;
        public GhoulState CurrentState => _state;
    }
}
