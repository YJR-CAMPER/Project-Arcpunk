// ── GhoulAnimator.cs ──
// SimpleGhoul의 FSM 상태를 Unity Animator 파라미터로 전달.
// Mixamo 애니메이션과 연동.
//
// Animator Controller에 필요한 파라미터:
//   Bool  "IsWalking"    — Chase 상태
//   Bool  "IsAttacking"  — Attack / BreakBlock 상태
//   Trigger "Hit"        — 피격 시
//   Trigger "Death"      — 사망 시
//   Bool  "IsBoss"       — 보스 전용 애니메이션 분기
//   Float "MoveSpeed"    — 블렌드 트리용 (선택)
//
// Mixamo 추천 애니메이션:
//   Idle    → "Zombie Idle" 또는 "Breathing Idle"
//   Walk    → "Zombie Walk" 또는 "Zombie Running"
//   Attack  → "Zombie Attack" 또는 "Standing Melee Attack Downward"
//   Hit     → "Standing React Small From Front"
//   Death   → "Zombie Dying" 또는 "Falling Back Death"
//   [보스]  → "Zombie Scream" (등장), "Standing Melee Combo" (공격)

using UnityEngine;

namespace Arcpunk.Ghoul
{
    [RequireComponent(typeof(SimpleGhoul))]
    public class GhoulAnimator : MonoBehaviour
    {
        private Animator _animator;
        private SimpleGhoul _ghoul;
        private SimpleGhoul.GhoulState _lastState;
        private bool _wasDead;

        // Animator 파라미터 해시 (문자열 비교 회피)
        private static readonly int _hashIsWalking = Animator.StringToHash("IsWalking");
        private static readonly int _hashIsAttacking = Animator.StringToHash("IsAttacking");
        private static readonly int _hashHit = Animator.StringToHash("Hit");
        private static readonly int _hashDeath = Animator.StringToHash("Death");
        private static readonly int _hashIsBoss = Animator.StringToHash("IsBoss");
        private static readonly int _hashMoveSpeed = Animator.StringToHash("MoveSpeed");

        private void Start()
        {
            _ghoul = GetComponent<SimpleGhoul>();
            _animator = GetComponentInChildren<Animator>();

            if (_animator == null)
            {
                Debug.LogWarning($"[GhoulAnimator] {name}: Animator를 찾을 수 없습니다.");
                enabled = false;
                return;
            }

            if (_ghoul.IsBoss)
                _animator.SetBool(_hashIsBoss, true);

            _lastState = _ghoul.CurrentState;
        }

        private void Update()
        {
            if (_animator == null || _ghoul == null) return;

            // 사망 체크
            if (!_ghoul.IsAlive && !_wasDead)
            {
                _wasDead = true;
                OnDeath();
                return;
            }
            if (_wasDead) return;

            // 상태 변화 감지
            var currentState = _ghoul.CurrentState;
            if (currentState != _lastState)
            {
                OnStateChanged(_lastState, currentState);
                _lastState = currentState;
            }

            // 이동 속도 전달
            var cc = GetComponent<CharacterController>();
            if (cc != null)
            {
                float speed = new Vector3(cc.velocity.x, 0, cc.velocity.z).magnitude;
                _animator.SetFloat(_hashMoveSpeed, speed);
            }
        }

        private void OnStateChanged(SimpleGhoul.GhoulState from, SimpleGhoul.GhoulState to)
        {
            _animator.SetBool(_hashIsWalking, false);
            _animator.SetBool(_hashIsAttacking, false);

            switch (to)
            {
                case SimpleGhoul.GhoulState.Idle:
                    break;
                case SimpleGhoul.GhoulState.Chase:
                    _animator.SetBool(_hashIsWalking, true);
                    break;
                case SimpleGhoul.GhoulState.Attack:
                case SimpleGhoul.GhoulState.BreakBlock:
                    _animator.SetBool(_hashIsAttacking, true);
                    break;
            }
        }

        /// <summary>피격 트리거. SimpleGhoul.TakeDamage()에서 호출.</summary>
        public void TriggerHit()
        {
            if (_animator != null && _ghoul.IsAlive)
                _animator.SetTrigger(_hashHit);
        }

        private void OnDeath()
        {
            if (_animator == null) return;
            _animator.SetBool(_hashIsWalking, false);
            _animator.SetBool(_hashIsAttacking, false);
            _animator.SetTrigger(_hashDeath);
        }
    }
}
