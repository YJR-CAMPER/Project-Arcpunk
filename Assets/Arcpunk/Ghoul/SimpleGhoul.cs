// ── SimpleGhoul.cs ──
// 마인크래프트 좀비 스타일 구울.
// NavMesh 없이 복셀 지형을 직접 탐색.
// 자극(빛/소리)에 반응하여 접근, 플레이어 직접 감지 시 추적.
// [추가] 블록 파괴 AI — 경로 상 블록을 공격하여 파괴 가능.
// [추가] 보스 플래그 — 보스 구울은 더 크고 강하며 블록 파괴 가능.
//
// 상태: Idle → Chase → Attack → BreakBlock (4상태)

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
        public float DirectDetectRange = 12f;

        [Header("Block Breaking")]
        public bool CanBreakBlocks = false;          // 블록 파괴 가능 여부
        public float BlockDamage = 10f;              // 블록에 가하는 데미지 (미래: 블록 내구도 시스템)
        public float BlockAttackCooldown = 1.5f;     // 블록 공격 간격
        public float BlockDetectRange = 1.5f;        // 전방 블록 감지 거리

        [Header("Boss")]
        public bool IsBoss = false;

        [Header("Knockback")]
        [SerializeField] private float _ghoulKnockbackForce = 4f;
        [SerializeField] private float _ghoulKnockbackDecay = 6f;

        [Header("Movement")]
        [SerializeField] private float _gravity = -15f;
        [SerializeField] private float _jumpForce = 6f;
        [SerializeField] private float _stepHeight = 1.2f;

        // 런타임 상태
        [HideInInspector] public float Health;
        private Vector3 _velocity;
        private Vector3 _knockbackVel;       // 넉백 벡터
        private float _attackTimer;
        private float _blockAttackTimer;
        private GhoulState _state = GhoulState.Idle;
        private Vector3 _targetPosition;
        private float _idleTimer;
        private float _stuckTimer;
        private Vector3 _lastPosition;
        private Vector3Int _targetBlock;    // 현재 공격 중인 블록 좌표

        // 컴포넌트
        private CharacterController _cc;
        private Renderer _renderer;
        private GhoulAnimator _ghoulAnim;

        public enum GhoulState { Idle, Chase, Attack, BreakBlock }

        private void Start()
        {
            if (Health <= 0) Health = MaxHealth;

            _cc = gameObject.AddComponent<CharacterController>();
            _cc.height = 1.8f;
            _cc.radius = 0.35f;
            _cc.center = new Vector3(0, 0.9f, 0);
            _cc.slopeLimit = 60f;
            _cc.stepOffset = _stepHeight;
            _cc.skinWidth = 0.05f;

            _lastPosition = transform.position;

            // Mixamo 모델의 Animator 연동
            _ghoulAnim = GetComponent<GhoulAnimator>();
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
                case GhoulState.BreakBlock:
                    UpdateBreakBlock();
                    break;
            }

            if (_attackTimer > 0) _attackTimer -= Time.deltaTime;
            if (_blockAttackTimer > 0) _blockAttackTimer -= Time.deltaTime;

            CheckStuck();
        }

        // ═══════════════════════════════════════
        // 상태별 업데이트
        // ═══════════════════════════════════════

        private void UpdateIdle()
        {
            var stimulus = StimulusManager.Instance?.GetStrongest(
                transform.position, PerceptionRadius);

            if (stimulus.HasValue)
            {
                _targetPosition = stimulus.Value.Origin;
                _state = GhoulState.Chase;
                return;
            }

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

            _idleTimer -= Time.deltaTime;
            if (_idleTimer <= 0)
            {
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

            if (distToPlayer < DirectDetectRange)
            {
                _targetPosition = player.transform.position;
            }
            else
            {
                var stimulus = StimulusManager.Instance?.GetStrongest(
                    transform.position, PerceptionRadius);

                if (stimulus.HasValue)
                    _targetPosition = stimulus.Value.Origin;
                else
                {
                    _state = GhoulState.Idle;
                    return;
                }
            }

            // 공격 범위 진입
            if (distToPlayer <= AttackRange)
            {
                _state = GhoulState.Attack;
                return;
            }

            // 전방 블록 감지 — 막혀있고 블록 파괴 가능하면 BreakBlock으로
            if (CanBreakBlocks && _stuckTimer > 0.5f)
            {
                if (TryFindBlockToBreak(out Vector3Int blockPos))
                {
                    _targetBlock = blockPos;
                    _state = GhoulState.BreakBlock;
                    return;
                }
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

            if (_attackTimer <= 0 && dist <= AttackRange)
            {
                var playerHealth = player.GetComponent<PlayerHealth>();
                if (playerHealth != null)
                    playerHealth.TakeDamage(Damage, transform.position);

                _attackTimer = AttackCooldown;
                StartCoroutine(AttackFlash());
            }
        }

        // ── 블록 파괴 상태 ──
        private void UpdateBreakBlock()
        {
            var world = VoxelWorld.Instance;
            if (world == null) { _state = GhoulState.Chase; return; }

            // 대상 블록이 이미 Air이면 → 파괴 완료, 추적으로 복귀
            if (world.GetBlock(_targetBlock.x, _targetBlock.y, _targetBlock.z) == BlockType.Air)
            {
                _state = GhoulState.Chase;
                _stuckTimer = 0;
                return;
            }

            // 블록을 바라봄
            Vector3 blockCenter = VoxelWorld.BlockCoordToWorldPos(_targetBlock);
            Vector3 lookDir = (blockCenter - transform.position).normalized;
            lookDir.y = 0;
            if (lookDir.sqrMagnitude > 0.01f)
                transform.rotation = Quaternion.Slerp(
                    transform.rotation,
                    Quaternion.LookRotation(lookDir),
                    Time.deltaTime * 8f);

            // 블록 공격
            if (_blockAttackTimer <= 0)
            {
                // 블록 파괴 (지금은 즉시 파괴, 미래: 내구도 시스템)
                // TODO: BlockData에 내구도 추가 시 여기서 데미지 누적
                world.SetBlock(_targetBlock.x, _targetBlock.y, _targetBlock.z, BlockType.Air);

                _blockAttackTimer = BlockAttackCooldown;
                _state = GhoulState.Chase;
                _stuckTimer = 0;

                // 블록 파괴 시각 피드백
                StartCoroutine(AttackFlash());

                Debug.Log($"[Ghoul] Broke block at {_targetBlock}!");
            }

            // 플레이어가 가까이 오면 플레이어 공격 우선
            var player = PlayerController.Instance;
            if (player != null)
            {
                float dist = Vector3.Distance(transform.position, player.transform.position);
                if (dist <= AttackRange)
                {
                    _state = GhoulState.Attack;
                }
            }
        }

        // ═══════════════════════════════════════
        // 블록 감지
        // ═══════════════════════════════════════

        /// <summary>전방에 파괴할 블록이 있는지 확인.</summary>
        private bool TryFindBlockToBreak(out Vector3Int blockPos)
        {
            blockPos = Vector3Int.zero;
            var world = VoxelWorld.Instance;
            if (world == null) return false;

            Vector3 fwd = transform.forward;
            Vector3 origin = transform.position + Vector3.up * 0.5f;

            // 정면 + 위 두 칸 확인 (머리 높이, 몸통 높이)
            for (int dy = 0; dy <= 1; dy++)
            {
                Vector3 checkPos = origin + fwd * BlockDetectRange + Vector3.up * dy;
                int bx = Mathf.FloorToInt(checkPos.x);
                int by = Mathf.FloorToInt(checkPos.y);
                int bz = Mathf.FloorToInt(checkPos.z);

                BlockType bt = world.GetBlock(bx, by, bz);
                if (bt != BlockType.Air && BlockData.IsSolid(bt))
                {
                    blockPos = new Vector3Int(bx, by, bz);
                    return true;
                }
            }

            return false;
        }

        // ═══════════════════════════════════════
        // 이동
        // ═══════════════════════════════════════

        private void MoveToward(Vector3 target, float speed)
        {
            Vector3 dir = (target - transform.position);
            dir.y = 0;

            if (dir.sqrMagnitude < 0.5f) return;

            dir = dir.normalized;

            if (dir.sqrMagnitude > 0.01f)
                transform.rotation = Quaternion.Slerp(
                    transform.rotation,
                    Quaternion.LookRotation(dir),
                    Time.deltaTime * 5f);

            Vector3 move = dir * speed * Time.deltaTime;
            move.y = _velocity.y * Time.deltaTime;
            move += _knockbackVel * Time.deltaTime;
            _cc.Move(move);

            // 넉백 감쇠
            if (_knockbackVel.sqrMagnitude > 0.01f)
                _knockbackVel = Vector3.Lerp(_knockbackVel, Vector3.zero, Time.deltaTime * _ghoulKnockbackDecay);
            else
                _knockbackVel = Vector3.zero;
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

            if (moved < 0.05f && (_state == GhoulState.Chase))
            {
                _stuckTimer += Time.deltaTime;

                if (_stuckTimer > 1f && _cc.isGrounded)
                {
                    // 블록 파괴 가능하면 파괴 시도, 아니면 점프
                    if (CanBreakBlocks && TryFindBlockToBreak(out Vector3Int bp))
                    {
                        _targetBlock = bp;
                        _state = GhoulState.BreakBlock;
                        _stuckTimer = 0;
                    }
                    else
                    {
                        _velocity.y = _jumpForce;
                        _stuckTimer = 0;
                    }
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
            StartCoroutine(DamageFlash());
            _ghoulAnim?.TriggerHit();

            // 피격 넉백 (공격 반대 방향으로)
            var player = PlayerController.Instance;
            if (player != null)
            {
                Vector3 knockDir = (transform.position - player.transform.position).normalized;
                knockDir.y = 0;
                _knockbackVel = knockDir * _ghoulKnockbackForce + Vector3.up * 2f;
            }

            if (Health <= 0)
                Die();
            else
                _state = GhoulState.Chase;
        }

        private void Die()
        {
            // 보스 사망 시 드롭 등 처리 가능
            if (IsBoss)
            {
                Debug.Log($"[Ghoul] BOSS KILLED!");
                // TODO: 특수 드롭, 경험치 등
            }

            StartCoroutine(DeathRoutine());
        }

        // ═══════════════════════════════════════
        // 공개 API
        // ═══════════════════════════════════════

        public bool IsAlive => Health > 0;
        public GhoulState CurrentState => _state;

        /// <summary>즉시 플레이어 추적 시작 (호드 스폰용).</summary>
        public void ForceChasePlayer()
        {
            var player = PlayerController.Instance;
            if (player != null)
            {
                _targetPosition = player.transform.position;
                _state = GhoulState.Chase;
                DirectDetectRange = 999f; // 호드 구울은 항상 플레이어 감지
            }
        }

        // ═══════════════════════════════════════
        // 시각 피드백
        // ═══════════════════════════════════════

        private System.Collections.IEnumerator DamageFlash()
        {
            if (_renderer == null) _renderer = GetComponentInChildren<Renderer>();
            if (_renderer == null) yield break;

            Color original = _renderer.material.color;
            _renderer.material.color = Color.white;
            yield return new WaitForSeconds(0.1f);
            if (_renderer != null)
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
    }
}
