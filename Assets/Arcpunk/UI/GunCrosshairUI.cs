// ── GunCrosshairUI.cs ──
// 총기 장착 시 표시되는 크로스헤어.
// 발사 시 벌어지고, 구울 조준 시 색상 변경.
// Player 오브젝트에 PlayerGunController와 함께 부착.

using UnityEngine;

namespace Arcpunk.Combat
{
    public class GunCrosshairUI : MonoBehaviour
    {
        // ── 설정 ──
        [Header("Appearance")]
        [SerializeField] private float _baseSize = 20f;         // 기본 크기
        [SerializeField] private float _maxSpreadSize = 50f;    // 최대 벌어짐
        [SerializeField] private float _lineLength = 8f;
        [SerializeField] private float _lineWidth = 2f;
        [SerializeField] private float _gap = 4f;              // 중앙 빈 공간
        [SerializeField] private float _dotSize = 2f;

        [Header("Colors")]
        [SerializeField] private Color _normalColor = new Color(1f, 1f, 1f, 0.85f);
        [SerializeField] private Color _enemyColor = new Color(1f, 0.3f, 0.3f, 1f);
        [SerializeField] private Color _hitFlashColor = new Color(1f, 1f, 0.3f, 1f);

        [Header("Animation")]
        [SerializeField] private float _spreadRecoverSpeed = 8f;
        [SerializeField] private float _fireKick = 12f;        // 발사 시 벌어짐 추가량

        // ── 상태 ──
        private PlayerGunController _gun;
        private Camera _cam;
        private float _currentSpread;
        private float _hitFlashTimer;
        private Texture2D _pixel;
        private bool _isTargetingEnemy;

        private void Start()
        {
            _gun = GetComponent<PlayerGunController>();
            _cam = Camera.main;
            _pixel = new Texture2D(1, 1);
            _pixel.SetPixel(0, 0, Color.white);
            _pixel.Apply();
        }

        private void Update()
        {
            if (_gun == null || !_gun.IsGunEquipped) return;

            // 스프레드 복귀
            _currentSpread = Mathf.Lerp(_currentSpread, 0f, Time.deltaTime * _spreadRecoverSpeed);

            // 히트 플래시 감소
            if (_hitFlashTimer > 0f)
                _hitFlashTimer -= Time.deltaTime;

            // 적 조준 체크
            CheckEnemyTarget();
        }

        /// <summary>발사 시 호출 — 크로스헤어 벌어짐.</summary>
        public void OnFire(float spreadAngle)
        {
            _currentSpread += _fireKick + spreadAngle;
            _currentSpread = Mathf.Min(_currentSpread, _maxSpreadSize);
        }

        /// <summary>적 적중 시 호출 — 히트마커 플래시.</summary>
        public void OnHit()
        {
            _hitFlashTimer = 0.15f;
        }

        private void CheckEnemyTarget()
        {
            if (_cam == null) return;

            Ray ray = new Ray(_cam.transform.position, _cam.transform.forward);
            _isTargetingEnemy = Physics.Raycast(ray, out RaycastHit hit, 100f) &&
                hit.collider.GetComponentInParent<Ghoul.SimpleGhoul>() != null;
        }

        private void OnGUI()
        {
            if (_gun == null || !_gun.IsGunEquipped) return;

            // 색상 결정
            Color color;
            if (_hitFlashTimer > 0f)
                color = _hitFlashColor;
            else if (_isTargetingEnemy)
                color = _enemyColor;
            else
                color = _normalColor;

            GUI.color = color;

            float cx = Screen.width * 0.5f;
            float cy = Screen.height * 0.5f;
            float spread = _gap + _currentSpread;

            // ── 중앙 점 ──
            GUI.DrawTexture(new Rect(
                cx - _dotSize * 0.5f,
                cy - _dotSize * 0.5f,
                _dotSize, _dotSize), _pixel);

            // ── 상 ──
            GUI.DrawTexture(new Rect(
                cx - _lineWidth * 0.5f,
                cy - spread - _lineLength,
                _lineWidth, _lineLength), _pixel);

            // ── 하 ──
            GUI.DrawTexture(new Rect(
                cx - _lineWidth * 0.5f,
                cy + spread,
                _lineWidth, _lineLength), _pixel);

            // ── 좌 ──
            GUI.DrawTexture(new Rect(
                cx - spread - _lineLength,
                cy - _lineWidth * 0.5f,
                _lineLength, _lineWidth), _pixel);

            // ── 우 ──
            GUI.DrawTexture(new Rect(
                cx + spread,
                cy - _lineWidth * 0.5f,
                _lineLength, _lineWidth), _pixel);

            GUI.color = Color.white;
        }

        private void OnDestroy()
        {
            if (_pixel != null) Destroy(_pixel);
        }
    }
}
