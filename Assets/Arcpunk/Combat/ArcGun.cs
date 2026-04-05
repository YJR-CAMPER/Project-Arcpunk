// ── ArcGun.cs ──
// 전기 무기: 레이캐스트 + LineRenderer 전기 이펙트.
// 전력을 소비하여 발사. 전력 없으면 발사 불가.
// Player 오브젝트에 부착.
//
// 전력 소모: 가장 가까운 전력 네트워크의 배터리에서 차감.
// 프로토타입 단순화: 플레이어 위치에서 가장 가까운 배터리 블록을 찾아 소모.

using System.Collections;
using UnityEngine;
using Arcpunk.Voxel;
using Arcpunk.Power;
using Arcpunk.Ghoul;

namespace Arcpunk.Combat
{
    public class ArcGun : MonoBehaviour
    {
        [Header("Stats")]
        [SerializeField] private float _damage = 25f;
        [SerializeField] private float _range = 30f;
        [SerializeField] private float _powerPerShot = 5f;
        [SerializeField] private float _fireRate = 0.3f; // 초 단위 쿨타임
        [SerializeField] private float _soundIntensity = 40f;

        [Header("Visual")]
        [SerializeField] private int _boltSegments = 6;
        [SerializeField] private float _boltJitter = 0.5f;
        [SerializeField] private float _boltDuration = 0.1f;
        [SerializeField] private Color _boltColor = new Color(0.4f, 0.7f, 1f, 0.9f);

        private Camera _cam;
        private float _fireTimer;
        private LineRenderer _boltLine;
        private Light _muzzleFlash;

        private void Start()
        {
            _cam = Camera.main;
            CreateVisuals();
        }

        private void CreateVisuals()
        {
            // 발사 이펙트용 LineRenderer
            GameObject boltObj = new GameObject("ArcGunBolt");
            boltObj.transform.SetParent(transform);
            _boltLine = boltObj.AddComponent<LineRenderer>();
            _boltLine.startWidth = 0.08f;
            _boltLine.endWidth = 0.03f;
            _boltLine.positionCount = 0;
            _boltLine.enabled = false;

            Material boltMat = new Material(Shader.Find("Particles/Standard Unlit"));
            if (boltMat != null)
            {
                boltMat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
                boltMat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.One);
                boltMat.SetColor("_Color", _boltColor);
            }
            else
            {
                boltMat = new Material(Shader.Find("Unlit/Color"));
                boltMat.color = _boltColor;
            }
            _boltLine.material = boltMat;

            // 머즐 플래시
            GameObject flashObj = new GameObject("MuzzleFlash");
            flashObj.transform.SetParent(_cam.transform);
            flashObj.transform.localPosition = new Vector3(0.3f, -0.2f, 0.5f);
            _muzzleFlash = flashObj.AddComponent<Light>();
            _muzzleFlash.type = LightType.Point;
            _muzzleFlash.range = 8f;
            _muzzleFlash.color = _boltColor;
            _muzzleFlash.intensity = 0;
        }

        private void Update()
        {
            if (_fireTimer > 0)
                _fireTimer -= Time.deltaTime;

            // 좌클릭 중이고 BlockInteraction이 블록을 파괴 중이 아닐 때
            // → E키로 무기 모드 전환? 프로토타입에서는 F키로 발사
            if (Input.GetMouseButton(0) && Input.GetKey(KeyCode.F) && _fireTimer <= 0)
            {
                TryFire();
            }

            // 또는: 마우스 가운데 버튼으로 발사
            if (Input.GetMouseButton(2) && _fireTimer <= 0)
            {
                TryFire();
            }
        }

        private void TryFire()
        {
            // 전력 체크
            var powerSys = VoxelPowerSystem.Instance;
            if (powerSys == null) return;

            var (stored, _) = powerSys.GetGlobalPowerStats();
            if (stored < _powerPerShot)
            {
                // 전력 부족 — 발사 불가 피드백
                Debug.Log("[ArcGun] Not enough power!");
                return;
            }

            // 전력 소비: 가장 가까운 네트워크에서 차감
            // (프로토타입 단순화: ConsumeGlobalPower)
            ConsumeGlobalPower(_powerPerShot);

            _fireTimer = _fireRate;

            // 레이캐스트
            Ray ray = new Ray(_cam.transform.position, _cam.transform.forward);
            Vector3 endPoint;
            bool hit = false;
            SimpleGhoul hitGhoul = null;

            if (Physics.Raycast(ray, out RaycastHit rayHit, _range))
            {
                endPoint = rayHit.point;

                // 구울 맞았는지 체크
                hitGhoul = rayHit.collider.GetComponent<SimpleGhoul>();
                if (hitGhoul == null)
                    hitGhoul = rayHit.collider.GetComponentInParent<SimpleGhoul>();

                hit = hitGhoul != null;
            }
            else
            {
                endPoint = ray.origin + ray.direction * _range;
            }

            // 데미지
            if (hit && hitGhoul != null)
            {
                hitGhoul.TakeDamage(_damage);
            }

            // 시각 효과
            StartCoroutine(FireVFX(ray.origin + _cam.transform.forward * 0.5f, endPoint));

            // 자극 등록 (소리 + 빛)
            StimulusManager.Instance?.OnWeaponFired(transform.position, _soundIntensity);
        }

        private void ConsumeGlobalPower(float amount)
        {
            // 프로토타입 단순화: 모든 네트워크에서 순차적으로 차감
            // (나중에 플레이어 위치 기반으로 개선)
            // PowerNetwork에 직접 접근이 안 되므로, VoxelPowerSystem에 메서드 추가 필요
            // → Day 3에서는 DebugHUD의 stored 값을 직접 감소시키는 대신
            //   OnLightningStrike의 역방향으로 처리

            // 임시 구현: VoxelPowerSystem에서 전역 소비
            VoxelPowerSystem.Instance?.ConsumeGlobal(amount);
        }

        private IEnumerator FireVFX(Vector3 from, Vector3 to)
        {
            // 지그재그 볼트
            _boltLine.positionCount = _boltSegments;
            for (int i = 0; i < _boltSegments; i++)
            {
                float t = i / (float)(_boltSegments - 1);
                Vector3 pos = Vector3.Lerp(from, to, t);
                if (i > 0 && i < _boltSegments - 1)
                {
                    pos += Random.insideUnitSphere * _boltJitter;
                }
                _boltLine.SetPosition(i, pos);
            }

            _boltLine.enabled = true;
            _muzzleFlash.intensity = 5f;

            yield return new WaitForSeconds(_boltDuration);

            _boltLine.enabled = false;

            // 플래시 감쇠
            float fade = 0.1f;
            while (fade > 0)
            {
                fade -= Time.deltaTime;
                _muzzleFlash.intensity = (fade / 0.1f) * 5f;
                yield return null;
            }
            _muzzleFlash.intensity = 0;
        }
    }
}
