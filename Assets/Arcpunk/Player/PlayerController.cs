// ── PlayerController.cs ──
// 간단한 1인칭 컨트롤러. CharacterController 기반.
// WASD 이동, 마우스 시점, 스페이스 점프.
// 프로토타입용 — 나중에 교체 가능.

using UnityEngine;

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

        [Header("References")]
        [SerializeField] private Transform _cameraHolder; // 빈 오브젝트, 카메라의 부모

        private CharacterController _cc;
        private Vector3 _velocity;
        private float _xRotation;
        private bool _cursorLocked = true;

        private void Awake()
        {
            Instance = this;
            _cc = GetComponent<CharacterController>();
        }

        private void Start()
        {
            SetCursorLock(true);

            // 스폰 위치
            var world = Voxel.VoxelWorld.Instance;
            if (world != null)
            {
                transform.position = world.GetSpawnPosition();
            }
        }

        private void Update()
        {
            HandleCursorToggle();

            if (_cursorLocked)
            {
                HandleLook();
                HandleMovement();
            }
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
            // 수평 이동
            float h = Input.GetAxisRaw("Horizontal");
            float v = Input.GetAxisRaw("Vertical");
            Vector3 moveDir = (transform.right * h + transform.forward * v).normalized;

            float speed = _moveSpeed;
            if (Input.GetKey(KeyCode.LeftShift))
                speed *= _sprintMultiplier;

            // 중력
            if (_cc.isGrounded && _velocity.y < 0)
                _velocity.y = -2f; // 약간의 하향력으로 grounded 유지

            // 점프
            if (Input.GetKeyDown(KeyCode.Space) && _cc.isGrounded)
                _velocity.y = _jumpForce;

            _velocity.y += _gravity * Time.deltaTime;

            // 이동 적용
            Vector3 finalMove = moveDir * speed * Time.deltaTime;
            finalMove.y = _velocity.y * Time.deltaTime;

            _cc.Move(finalMove);
        }

        private void HandleCursorToggle()
        {
            // Escape로 커서 토글
            if (Input.GetKeyDown(KeyCode.Escape))
            {
                SetCursorLock(!_cursorLocked);
            }

            // 화면 클릭으로 다시 잠금
            if (!_cursorLocked && Input.GetMouseButtonDown(0))
            {
                SetCursorLock(true);
            }
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
