// ── PlayerGunController.cs ──
// 플레이어 총기 컨트롤러.
// 총기 아이템 장착 시 자동 활성화, BlockInteraction의 LMB를 오버라이드.
//
// 동작:
// - 인벤토리에서 총기 아이템 감지 → GunDef 로드
// - SemiAuto: LMB 클릭당 1발
// - FullAuto: LMB 홀드 시 연사 (미니건은 스핀업 후)
// - 히트스캔 레이캐스트 → 구울 데미지 + 넉백
// - 탄약 소모 (인벤토리에서 차감)
// - VFX: 트레이서 라인 + 머즐 플래시 + 히트마커
// - 반동 애니메이션 + 크로스헤어 연동

using System.Collections;
using UnityEngine;

namespace Arcpunk.Combat
{
    public class PlayerGunController : MonoBehaviour
    {
        // ── 설정 ──
        [Header("Layers")]
        [SerializeField] private LayerMask _hitLayers;         // 구울 + 지형
        [SerializeField] private LayerMask _ghoulLayer;

        [Header("VFX")]
        [SerializeField] private float _tracerDuration = 0.06f;
        [SerializeField] private float _muzzleFlashDuration = 0.05f;

        // ── 상태 ──
        private GunDef _currentGun;
        private bool _isGunEquipped;
        private float _fireCooldown;
        private float _spinUpProgress;     // 미니건 스핀업 진행도 (0~1)
        private bool _isFiring;

        // ── 참조 ──
        private Camera _cam;
        private Inventory.PlayerInventory _inventory;
        private GunCrosshairUI _crosshair;

        // ── VFX 오브젝트 ──
        private LineRenderer _tracerLine;
        private Light _muzzleFlash;
        private Coroutine _tracerCoroutine;
        private Coroutine _recoilCoroutine;

        // ── 공개 프로퍼티 ──
        public bool IsGunEquipped => _isGunEquipped;
        public GunType CurrentGunType => _isGunEquipped ? _currentGun.Type : GunType.None;
        public float SpinUpProgress => _spinUpProgress;

        // =====================================================
        //  초기화
        // =====================================================

        private void Start()
        {
            _cam = Camera.main;
            _inventory = Inventory.PlayerInventory.Instance;
            _crosshair = GetComponent<GunCrosshairUI>();
            CreateVFX();
        }

        private void CreateVFX()
        {
            // ── 트레이서 라인 ──
            var tracerObj = new GameObject("GunTracer");
            tracerObj.transform.SetParent(transform, false);

            _tracerLine = tracerObj.AddComponent<LineRenderer>();
            _tracerLine.positionCount = 2;
            _tracerLine.startWidth = 0.02f;
            _tracerLine.endWidth = 0.01f;
            _tracerLine.useWorldSpace = true;
            _tracerLine.enabled = false;

            var mat = new Material(Shader.Find("Particles/Standard Unlit"));
            if (mat != null)
            {
                mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
                mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.One);
                mat.SetColor("_Color", Color.yellow);
            }
            _tracerLine.material = mat;

            // ── 머즐 플래시 ──
            var flashObj = new GameObject("MuzzleFlash");
            flashObj.transform.SetParent(_cam != null ? _cam.transform : transform, false);
            flashObj.transform.localPosition = new Vector3(0.3f, -0.2f, 0.8f);

            _muzzleFlash = flashObj.AddComponent<Light>();
            _muzzleFlash.type = LightType.Point;
            _muzzleFlash.range = 8f;
            _muzzleFlash.intensity = 0f;
            _muzzleFlash.color = new Color(1f, 0.8f, 0.4f);
        }

        // =====================================================
        //  Update
        // =====================================================

        private void Update()
        {
            // UI 열려있으면 차단
            if (UI.KnappingUI.Instance != null && UI.KnappingUI.Instance.IsOpen) return;
            if (UI.InventoryUI.Instance != null && UI.InventoryUI.Instance.IsOpen) return;

            // ── 총기 장착 감지 ──
            DetectGun();

            if (!_isGunEquipped) return;

            // ── 쿨다운 ──
            if (_fireCooldown > 0f)
                _fireCooldown -= Time.deltaTime;

            // ── 발사 입력 ──
            if (_currentGun.Mode == FireMode.SemiAuto)
            {
                if (Input.GetMouseButtonDown(0) && _fireCooldown <= 0f)
                {
                    TryFire();
                }
            }
            else if (_currentGun.Mode == FireMode.FullAuto)
            {
                if (Input.GetMouseButton(0))
                {
                    // 미니건 스핀업
                    if (_currentGun.SpinUpTime > 0f && _spinUpProgress < 1f)
                    {
                        _spinUpProgress += Time.deltaTime / _currentGun.SpinUpTime;
                        _spinUpProgress = Mathf.Clamp01(_spinUpProgress);
                        _isFiring = false;
                        return;
                    }

                    if (_fireCooldown <= 0f)
                    {
                        TryFire();
                    }
                }
                else
                {
                    _spinUpProgress = 0f;
                    _isFiring = false;
                }
            }
        }

        // =====================================================
        //  총기 감지
        // =====================================================

        private void DetectGun()
        {
            if (_inventory == null)
            {
                _inventory = Inventory.PlayerInventory.Instance;
                if (_inventory == null) { _isGunEquipped = false; return; }
            }

            var item = _inventory.GetSelectedItem();
            if (item == null || item.IsEmpty)
            {
                _isGunEquipped = false;
                _spinUpProgress = 0f;
                return;
            }

            if (GunDatabase.TryGetGun(item.Type, out GunDef gun))
            {
                _currentGun = gun;
                _isGunEquipped = true;
            }
            else
            {
                _isGunEquipped = false;
                _spinUpProgress = 0f;
            }
        }

        // =====================================================
        //  발사
        // =====================================================

        private void TryFire()
        {
            // ── 탄약 체크 ──
            if (!HasAmmo())
            {
                Debug.Log($"[Gun] {_currentGun.DisplayName}: 탄약 부족!");
                return;
            }

            // ── 탄약 소모 ──
            ConsumeAmmo();

            // ── 쿨다운 설정 ──
            _fireCooldown = _currentGun.FireInterval;
            _isFiring = true;

            // ── 히트스캔 ──
            Vector3 origin = _cam.transform.position;
            Vector3 direction = ApplySpread(_cam.transform.forward);

            bool hit = Physics.Raycast(origin, direction, out RaycastHit hitInfo,
                _currentGun.Range, _hitLayers);

            Vector3 endPoint;

            if (hit)
            {
                endPoint = hitInfo.point;

                // 구울 데미지
                if (IsGhoulLayer(hitInfo.collider.gameObject.layer))
                {
                    ApplyDamage(hitInfo);
                }
            }
            else
            {
                endPoint = origin + direction * _currentGun.Range;
            }

            // ── VFX ──
            FireVFX(origin + _cam.transform.forward * 0.5f, endPoint);

            // ── 크로스헤어 반응 ──
            _crosshair?.OnFire(_currentGun.SpreadAngle);

            // ── 반동 애니메이션 ──
            PlayRecoil();
        }

        // =====================================================
        //  스프레드 (탄 퍼짐)
        // =====================================================

        private Vector3 ApplySpread(Vector3 forward)
        {
            if (_currentGun.SpreadAngle <= 0f)
                return forward;

            float spread = _currentGun.SpreadAngle;

            // 미니건: 연사 중 스프레드 약간 증가
            if (_currentGun.Type == GunType.Minigun && _isFiring)
                spread *= 1.3f;

            float halfRad = spread * 0.5f * Mathf.Deg2Rad;
            Vector3 randomOffset = new Vector3(
                Random.Range(-halfRad, halfRad),
                Random.Range(-halfRad, halfRad),
                0f);

            return (Quaternion.LookRotation(forward) * Quaternion.Euler(
                randomOffset.x * Mathf.Rad2Deg,
                randomOffset.y * Mathf.Rad2Deg, 0f)) * Vector3.forward;
        }

        // =====================================================
        //  데미지
        // =====================================================

        private void ApplyDamage(RaycastHit hit)
        {
            var ghoul = hit.collider.GetComponent<Ghoul.SimpleGhoul>();
            if (ghoul == null)
                ghoul = hit.collider.GetComponentInParent<Ghoul.SimpleGhoul>();

            if (ghoul == null || !ghoul.IsAlive) return;

            ghoul.TakeDamage(_currentGun.Damage);

            // 히트마커
            _crosshair?.OnHit();

            // 넉백
            var rb = ghoul.GetComponent<Rigidbody>();
            if (rb == null) rb = ghoul.GetComponentInChildren<Rigidbody>();
            if (rb != null)
            {
                Vector3 knockDir = (hit.point - _cam.transform.position).normalized;
                knockDir.y = Mathf.Max(knockDir.y, 0.2f);
                rb.AddForce(knockDir * _currentGun.Knockback, ForceMode.Impulse);
            }
        }

        // =====================================================
        //  탄약
        // =====================================================

        private bool HasAmmo()
        {
            if (_inventory == null) return false;
            return _inventory.HasItem(_currentGun.AmmoType, _currentGun.AmmoPerShot);
        }

        private void ConsumeAmmo()
        {
            if (_inventory == null) return;
            _inventory.RemoveItem(_currentGun.AmmoType, _currentGun.AmmoPerShot);
        }

        // =====================================================
        //  VFX
        // =====================================================

        private void FireVFX(Vector3 from, Vector3 to)
        {
            if (_tracerCoroutine != null)
                StopCoroutine(_tracerCoroutine);
            _tracerCoroutine = StartCoroutine(TracerCoroutine(from, to));

            StartCoroutine(MuzzleFlashCoroutine());
        }

        private IEnumerator TracerCoroutine(Vector3 from, Vector3 to)
        {
            _tracerLine.startWidth = _currentGun.TracerWidth;
            _tracerLine.endWidth = _currentGun.TracerWidth * 0.5f;
            _tracerLine.material.SetColor("_Color", _currentGun.TracerColor);

            _tracerLine.SetPosition(0, from);
            _tracerLine.SetPosition(1, to);
            _tracerLine.enabled = true;

            yield return new WaitForSeconds(_tracerDuration);

            _tracerLine.enabled = false;
            _tracerCoroutine = null;
        }

        private IEnumerator MuzzleFlashCoroutine()
        {
            _muzzleFlash.intensity = _currentGun.MuzzleFlashIntensity;
            _muzzleFlash.range = _currentGun.Type == GunType.Minigun ? 5f : 10f;

            // 플래시 색상 랜덤 변동 (불꽃 느낌)
            _muzzleFlash.color = new Color(
                1f,
                Random.Range(0.6f, 0.9f),
                Random.Range(0.2f, 0.5f));

            float elapsed = 0f;
            float duration = _currentGun.Type == GunType.Minigun ? 0.03f : _muzzleFlashDuration;
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / duration;
                _muzzleFlash.intensity = Mathf.Lerp(
                    _currentGun.MuzzleFlashIntensity, 0f, t * t);
                yield return null;
            }

            _muzzleFlash.intensity = 0f;
        }

        // =====================================================
        //  반동 애니메이션
        // =====================================================

        private void PlayRecoil()
        {
            var held = Voxel.HeldVoxelItem.Instance;
            if (held == null) return;

            if (_recoilCoroutine != null)
                StopCoroutine(_recoilCoroutine);
            _recoilCoroutine = StartCoroutine(RecoilCoroutine(held.transform));

            // ── 총소리 ──
            Audio.GameAudioManager.Instance?.PlayGunFire(
                _currentGun.Type, _cam.transform.position);
        }

        private IEnumerator RecoilCoroutine(Transform heldTransform)
        {
            Vector3 originalPos = heldTransform.localPosition;
            Quaternion originalRot = heldTransform.localRotation;

            // 총기별 반동 강도
            float kickBack = _currentGun.Type switch
            {
                GunType.Pistol  => 0.08f,
                GunType.Rifle   => 0.12f,
                GunType.Minigun => 0.03f,
                _ => 0.05f,
            };
            float kickUp = kickBack * 2f;
            float duration = _currentGun.Type == GunType.Minigun ? 0.04f : 0.1f;

            // 킥 (뒤로 + 위로 회전)
            float elapsed = 0f;
            while (elapsed < duration * 0.3f)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / (duration * 0.3f);
                heldTransform.localPosition = originalPos + new Vector3(0, 0, -kickBack * t);
                heldTransform.localRotation = originalRot *
                    Quaternion.Euler(-kickUp * t * Mathf.Rad2Deg, 0, 0);
                yield return null;
            }

            // 복귀 (smoothstep)
            elapsed = 0f;
            Vector3 kickedPos = heldTransform.localPosition;
            Quaternion kickedRot = heldTransform.localRotation;
            while (elapsed < duration * 0.7f)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / (duration * 0.7f);
                float smooth = t * t * (3f - 2f * t);
                heldTransform.localPosition = Vector3.Lerp(kickedPos, originalPos, smooth);
                heldTransform.localRotation = Quaternion.Slerp(kickedRot, originalRot, smooth);
                yield return null;
            }

            heldTransform.localPosition = originalPos;
            heldTransform.localRotation = originalRot;
            _recoilCoroutine = null;
        }

        // =====================================================
        //  유틸
        // =====================================================

        private bool IsGhoulLayer(int layer)
        {
            return (_ghoulLayer.value & (1 << layer)) != 0;
        }

        private void OnDisable()
        {
            _isGunEquipped = false;
            _spinUpProgress = 0f;
            if (_tracerLine != null) _tracerLine.enabled = false;
            if (_muzzleFlash != null) _muzzleFlash.intensity = 0f;
        }
    }
}
