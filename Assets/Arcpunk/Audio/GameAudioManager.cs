// ── GameAudioManager.cs ──
// 중앙 사운드 매니저. Inspector에서 클립 할당, 각 시스템에서 호출.
// 씬에 빈 오브젝트로 배치 → 컴포넌트 추가 → 클립 할당.
//
// BGM은 WeatherSystem.OnWeatherChanged를 구독하여 자동 크로스페이드.

using UnityEngine;

namespace Arcpunk.Audio
{
    public class GameAudioManager : MonoBehaviour
    {
        public static GameAudioManager Instance { get; private set; }

        // ═══════════════════════════════════════
        //  Inspector 클립 슬롯
        // ═══════════════════════════════════════

        [Header("── 총기 ──")]
        [SerializeField] private AudioClip _pistolFire;
        [SerializeField] private AudioClip _rifleFire;
        [SerializeField] private AudioClip _minigunFire;
        [SerializeField] private AudioClip _gunDryFire;        // 탄약 없음 딸깍

        [Header("── 구울 ──")]
        [SerializeField] private AudioClip[] _ghoulAggro;      // 어그로 (Chase 진입)
        [SerializeField] private AudioClip[] _ghoulAttack;      // 공격
        [SerializeField] private AudioClip[] _ghoulHit;         // 피격
        [SerializeField] private AudioClip[] _ghoulDeath;       // 사망

        [Header("── 블록 ──")]
        [SerializeField] private AudioClip _blockBreak;
        [SerializeField] private AudioClip _blockPlace;
        [SerializeField] private AudioClip _blockHit;           // 채굴 중 타격음

        [Header("── 근접 전투 ──")]
        [SerializeField] private AudioClip _meleeSwing;
        [SerializeField] private AudioClip _meleeHit;

        [Header("── 플레이어 ──")]
        [SerializeField] private AudioClip _playerHurt;
        [SerializeField] private AudioClip _playerDeath;
        [SerializeField] private AudioClip _itemPickup;
        [SerializeField] private AudioClip _eat;                // 버섯 섭취

        [Header("── BGM / 앰비언트 ──")]
        [SerializeField] private AudioClip _bgmCalm;
        [SerializeField] private AudioClip _bgmStorm;
        [SerializeField] private float _bgmVolume = 0.3f;
        [SerializeField] private float _crossfadeSpeed = 0.8f;  // 초당 볼륨 변화량

        [Header("── 볼륨 설정 ──")]
        [SerializeField] private float _sfxVolume = 1f;
        [SerializeField] private float _gunVolume = 0.7f;
        [SerializeField] private float _ghoulVolume = 0.8f;

        // ── BGM 오디오소스 (크로스페이드용 2개) ──
        private AudioSource _bgmA;
        private AudioSource _bgmB;
        private bool _bgmAIsActive = true;

        // ── 2D SFX 소스 (UI/플레이어 사운드) ──
        private AudioSource _sfx2D;

        // ═══════════════════════════════════════
        //  초기화
        // ═══════════════════════════════════════

        private void Awake()
        {
            Instance = this;
            CreateAudioSources();
        }

        private void Start()
        {
            // WeatherSystem 구독 → BGM 자동 전환
            var weather = Weather.WeatherSystem.Instance;
            if (weather != null)
                weather.OnWeatherChanged += OnWeatherChanged;

            // Calm BGM으로 시작
            PlayBGM(_bgmCalm);
        }

        private void OnDestroy()
        {
            var weather = Weather.WeatherSystem.Instance;
            if (weather != null)
                weather.OnWeatherChanged -= OnWeatherChanged;
        }

        private void CreateAudioSources()
        {
            // BGM A
            _bgmA = gameObject.AddComponent<AudioSource>();
            _bgmA.loop = true;
            _bgmA.spatialBlend = 0f;
            _bgmA.volume = 0f;
            _bgmA.playOnAwake = false;

            // BGM B (크로스페이드 대상)
            _bgmB = gameObject.AddComponent<AudioSource>();
            _bgmB.loop = true;
            _bgmB.spatialBlend = 0f;
            _bgmB.volume = 0f;
            _bgmB.playOnAwake = false;

            // 2D SFX
            _sfx2D = gameObject.AddComponent<AudioSource>();
            _sfx2D.spatialBlend = 0f;
            _sfx2D.playOnAwake = false;
        }

        // ═══════════════════════════════════════
        //  BGM 크로스페이드
        // ═══════════════════════════════════════

        private void OnWeatherChanged(Weather.WeatherState state)
        {
            switch (state.Phase)
            {
                case Weather.WeatherPhase.Calm:
                case Weather.WeatherPhase.Subsiding:
                    PlayBGM(_bgmCalm);
                    break;
                case Weather.WeatherPhase.Building:
                case Weather.WeatherPhase.Storm:
                    PlayBGM(_bgmStorm);
                    break;
            }
        }

        private void PlayBGM(AudioClip clip)
        {
            if (clip == null) return;

            var incoming = _bgmAIsActive ? _bgmB : _bgmA;
            _bgmAIsActive = !_bgmAIsActive;

            // 같은 클립이면 스킵
            if (incoming.clip == clip && incoming.isPlaying) return;

            incoming.clip = clip;
            incoming.Play();
        }

        private void Update()
        {
            // 크로스페이드
            var active = _bgmAIsActive ? _bgmA : _bgmB;
            var inactive = _bgmAIsActive ? _bgmB : _bgmA;

            float delta = _crossfadeSpeed * Time.deltaTime;
            active.volume = Mathf.MoveTowards(active.volume, _bgmVolume, delta);
            inactive.volume = Mathf.MoveTowards(inactive.volume, 0f, delta);

            if (inactive.volume <= 0f && inactive.isPlaying)
                inactive.Stop();
        }

        // ═══════════════════════════════════════
        //  총기 사운드
        // ═══════════════════════════════════════

        public void PlayGunFire(Combat.GunType gunType, Vector3 position)
        {
            AudioClip clip = gunType switch
            {
                Combat.GunType.Pistol  => _pistolFire,
                Combat.GunType.Rifle   => _rifleFire,
                Combat.GunType.Minigun => _minigunFire,
                _ => null,
            };
            Play3D(clip, position, _gunVolume);
        }

        public void PlayGunDryFire()
        {
            Play2D(_gunDryFire, _gunVolume);
        }

        // ═══════════════════════════════════════
        //  구울 사운드
        // ═══════════════════════════════════════

        public void PlayGhoulAggro(Vector3 pos)  => Play3DRandom(_ghoulAggro, pos, _ghoulVolume);
        public void PlayGhoulAttack(Vector3 pos) => Play3DRandom(_ghoulAttack, pos, _ghoulVolume);
        public void PlayGhoulHit(Vector3 pos)    => Play3DRandom(_ghoulHit, pos, _ghoulVolume);
        public void PlayGhoulDeath(Vector3 pos)  => Play3DRandom(_ghoulDeath, pos, _ghoulVolume);

        // ═══════════════════════════════════════
        //  블록 사운드
        // ═══════════════════════════════════════

        public void PlayBlockBreak(Vector3 pos) => Play3D(_blockBreak, pos, _sfxVolume);
        public void PlayBlockPlace(Vector3 pos) => Play3D(_blockPlace, pos, _sfxVolume);
        public void PlayBlockHit(Vector3 pos)   => Play3D(_blockHit, pos, _sfxVolume * 0.6f);

        // ═══════════════════════════════════════
        //  근접 / 플레이어 사운드
        // ═══════════════════════════════════════

        public void PlayMeleeSwing() => Play2D(_meleeSwing, _sfxVolume);
        public void PlayMeleeHit()   => Play2D(_meleeHit, _sfxVolume);
        public void PlayPlayerHurt() => Play2D(_playerHurt, _sfxVolume);
        public void PlayPlayerDeath()=> Play2D(_playerDeath, _sfxVolume);
        public void PlayItemPickup() => Play2D(_itemPickup, _sfxVolume * 0.5f);
        public void PlayEat()        => Play2D(_eat, _sfxVolume);

        // ═══════════════════════════════════════
        //  내부 재생 유틸
        // ═══════════════════════════════════════

        private void Play2D(AudioClip clip, float volume)
        {
            if (clip == null) return;
            _sfx2D.PlayOneShot(clip, volume);
        }

        private void Play3D(AudioClip clip, Vector3 pos, float volume)
        {
            if (clip == null) return;
            AudioSource.PlayClipAtPoint(clip, pos, volume);
        }

        private void Play3DRandom(AudioClip[] clips, Vector3 pos, float volume)
        {
            if (clips == null || clips.Length == 0) return;
            var clip = clips[Random.Range(0, clips.Length)];
            Play3D(clip, pos, volume);
        }
    }
}
