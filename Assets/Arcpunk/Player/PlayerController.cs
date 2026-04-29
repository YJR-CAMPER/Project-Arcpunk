// ── PlayerController.cs ──
// 1인칭 컨트롤러. CharacterController 기반.
// [추가] 외부 넉백 속도 수용 (AddExternalVelocity)
// [추가] 사망 시 입력 차단

using UnityEngine;
using Arcpunk.UI;

namespace Arcpunk.Player
{
    [RequireComponent(typeof(CharacterController))]
    public class PlayerController : MonoBehaviour
    {
        public static PlayerController Instance { get; private set; }

        [Header("Movement")]
        [SerializeField] private float _moveSpeed = 5f;
        [SerializeField] private float _sprintMultiplier = 1.8f;
        [SerializeField] private float _jumpForce = 7f;
        [SerializeField] private float _gravity = -20f;

        [Header("Look")]
        [SerializeField] private float _mouseSensitivity = 2f;
        [SerializeField] private float _maxLookAngle = 85f;

        [Header("Knockback")]
        [SerializeField] private float _knockbackDecay = 8f;   // 넉백 감쇠 속도

        [Header("References")]
        [SerializeField] private Transform _cameraHolder;

        private CharacterController _cc;
        private Vector3 _velocity;
        private Vector3 _knockbackVelocity;  // 외부 넉백 벡터
        private float _xRotation;
        private bool _cursorLocked = true;

        // 사망 카메라 연출
        private bool _deathTiltActive;
        private float _deathTiltProgress;

        private void Awake()
        {
            Instance = this;
            _cc = GetComponent<CharacterController>();
        }

        private void Start()
        {
            SetCursorLock(true);

            var world = Voxel.VoxelWorld.Instance;
            if (world != null)
                transform.position = world.GetSpawnPosition();
        }

        private void Update()
        {
            // 사망 상태
            var health = GetComponent<PlayerHealth>();
            if (health != null && health.IsDead)
            {
                UpdateDeathCamera();
                return; // 모든 입력 차단
            }

            HandleCursorToggle();

            // UI 열려있으면 조작 차단
            if (KnappingUI.Instance != null && KnappingUI.Instance.IsOpen)
                return;
            if (InventoryUI.Instance != null && InventoryUI.Instance.IsOpen)
                return;

            if (_cursorLocked)
            {
                HandleLook();
                HandleMovement();
            }

            // 넉백 감쇠
            ApplyKnockbackDecay();
        }

        private void HandleLook()
        {
            float mouseX = Input.GetAxis("Mouse X") * _mouseSensitivity;
            float mouseY = Input.GetAxis("Mouse Y") * _mouseSensitivity;

            _xRotation -= mouseY;
            _xRotation = Mathf.Clamp(_xRotation, -_maxLookAngle, _maxLookAngle);

            if (_cameraHolder != null)
                _cameraHolder.localRotation = Quaternion.Euler(_xRotation, 0, 0);
            else
                Camera.main.transform.localRotation = Quaternion.Euler(_xRotation, 0, 0);

            transform.Rotate(Vector3.up * mouseX);
        }

        private void HandleMovement()
        {
            float h = Input.GetAxisRaw("Horizontal");
            float v = Input.GetAxisRaw("Vertical");
            Vector3 moveDir = (transform.right * h + transform.forward * v).normalized;

            float speed = _moveSpeed;
            if (Input.GetKey(KeyCode.LeftShift))
                speed *= _sprintMultiplier;

            if (_cc.isGrounded && _velocity.y < 0)
                _velocity.y = -2f;

            if (Input.GetKeyDown(KeyCode.Space) && _cc.isGrounded)
                _velocity.y = _jumpForce;

            _velocity.y += _gravity * Time.deltaTime;

            // 이동 + 넉백 합산
            Vector3 finalMove = moveDir * speed * Time.deltaTime;
            finalMove.y = _velocity.y * Time.deltaTime;
            finalMove += _knockbackVelocity * Time.deltaTime;

            _cc.Move(finalMove);
        }

        // ═══════════════════════════════════════
        // 넉백
        // ═══════════════════════════════════════

        /// <summary>외부에서 넉백 속도를 추가. PlayerHealth에서 호출.</summary>
        public void AddExternalVelocity(Vector3 velocity)
        {
            _knockbackVelocity += velocity;
        }

        private void ApplyKnockbackDecay()
        {
            if (_knockbackVelocity.sqrMagnitude > 0.01f)
            {
                _knockbackVelocity = Vector3.Lerp(
                    _knockbackVelocity, Vector3.zero,
                    Time.deltaTime * _knockbackDecay);
            }
            else
            {
                _knockbackVelocity = Vector3.zero;
            }
        }

        // ═══════════════════════════════════════
        // 사망 카메라
        // ═══════════════════════════════════════

        private void UpdateDeathCamera()
        {
            if (!_deathTiltActive)
            {
                _deathTiltActive = true;
                _deathTiltProgress = 0f;
            }

            // 카메라 천천히 기울어짐 (쓰러지는 연출)
            _deathTiltProgress += Time.deltaTime * 0.5f;
            float tilt = Mathf.Lerp(0, 45f, Mathf.Clamp01(_deathTiltProgress));
            float drop = Mathf.Lerp(0, -0.5f, Mathf.Clamp01(_deathTiltProgress));

            if (_cameraHolder != null)
            {
                _cameraHolder.localRotation = Quaternion.Euler(
                    _xRotation + tilt * 0.3f,
                    0,
                    tilt);
                _cameraHolder.localPosition = new Vector3(0, drop, 0);
            }

            // 중력은 계속 적용
            if (_cc.isGrounded && _velocity.y < 0)
                _velocity.y = -2f;
            _velocity.y += _gravity * Time.deltaTime;
            _cc.Move(new Vector3(0, _velocity.y * Time.deltaTime, 0));
        }

        /// <summary>리스폰 시 카메라 복원.</summary>
        public void ResetDeathState()
        {
            _deathTiltActive = false;
            _deathTiltProgress = 0f;
            _knockbackVelocity = Vector3.zero;

            if (_cameraHolder != null)
            {
                _cameraHolder.localRotation = Quaternion.Euler(_xRotation, 0, 0);
                _cameraHolder.localPosition = Vector3.zero;
            }

            SetCursorLock(true);
        }

        // ═══════════════════════════════════════
        // 커서
        // ═══════════════════════════════════════

        private void HandleCursorToggle()
        {
            if (Input.GetKeyDown(KeyCode.Escape))
                SetCursorLock(!_cursorLocked);

            if (!_cursorLocked && Input.GetMouseButtonDown(0))
                SetCursorLock(true);
        }

        private void SetCursorLock(bool locked)
        {
            _cursorLocked = locked;
            Cursor.lockState = locked ? CursorLockMode.Locked : CursorLockMode.None;
            Cursor.visible = !locked;
        }

        // ── 공개 API ──
        public float CurrentSpeed => new Vector3(_cc.velocity.x, 0, _cc.velocity.z).magnitude;
        public bool IsGrounded => _cc.isGrounded;
    }
}