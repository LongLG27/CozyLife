using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using CozyLife.Player;
using CozyLife.World;
using CozyLife.UI;

namespace CozyLife.Core
{
    public enum CozySfxType
    {
        Footstep = 0,
        UiClick = 1,
        Coin = 2,
        Door = 3,
        Craft = 4,
        EatDrink = 5,
        Harvest = 6,
        PlaceFurniture = 7,
        PickupFurniture = 8,
        SicknessOrAlert = 9,
        Cure = 10,
        Snore = 11,
        Sizzle = 12,
        SawWood = 13,
        CashRegister = 14,
        Watering = 15,
        PageTurn = 16,
        DoctorHeal = 17,
        BallBounce = 18,
        NetSwish = 19,
        Whistle = 20,
        Cheer = 21,
        WolfHowl = 22,
        BellGong = 23,
        MysticChime = 24,
        Gunshot = 25,
        Splash = 26,
        ReelIn = 27,
        BicycleBell = 28,
        PetBark = 29,
        PianoTune = 30,
        GramophoneMusic = 31
    }

    /// <summary>
    /// Hệ thống Âm Thanh & Nhạc Nền tổng hợp (Procedural Cozy Audio Engine):
    /// - Tự động tạo AudioClip ấm áp bằng sóng âm C# (không phụ thuộc file âm thanh ngoài):
    ///   + Nhạc nền Ban Ngày (Cozy Acoustic / Kalimba Lofi Chord Progression: Cmaj7 - Am7 - Fmaj7 - G6)
    ///   + Nhạc nền Ban Đêm (Calm Lullaby Piano + Tiếng dế đêm dịu nhẹ)
    ///   + Âm thanh môi trường Thời Tiết (Tiếng mưa rơi / gió tuyết ngoài trời)
    ///   + 11 Hiệu ứng âm thanh (SFX): Tiếng bước chân theo nhịp đi, Click UI, Nhận Xu, Mở cửa, Đóng gỗ, Ăn uống, Thu hoạch, Đặt/Cất nội thất, Cảnh báo bệnh & Khỏi bệnh.
    /// </summary>
    public class CozyAudioManager : MonoBehaviour
    {
        public static CozyAudioManager Instance { get; private set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticState()
        {
            Instance = null;
        }

        private const int SampleRate = 22050;
        private const string MutePrefKey = "CozyLife_AudioMuted_v1";

        private AudioSource bgmSource;
        private AudioSource weatherSource;
        private AudioSource sfxSource;

        private AudioClip dayBgmClip;
        private AudioClip nightBgmClip;
        private AudioClip rainLoopClip;
        private readonly Dictionary<CozySfxType, AudioClip> sfxClips = new Dictionary<CozySfxType, AudioClip>();

        private bool isMuted = false;
        private float masterVolume = 0.78f;
        private bool playingNightBgm = false;
        private float footstepCooldown = 0f;

        public static bool IsMuted => Instance != null && Instance.isMuted;

        public static void EnsureInstance()
        {
            if (Instance != null)
                return;

            GameObject go = new GameObject("CozyAudioManager");
            DontDestroyOnLoad(go);
            Instance = go.AddComponent<CozyAudioManager>();
        }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            isMuted = PlayerPrefs.GetInt(MutePrefKey, 0) == 1;

            EnsureAudioListener();
            CreateAudioSources();
            GenerateAllProceduralClips();

            bgmSource.clip = dayBgmClip;
            bgmSource.loop = true;
            bgmSource.volume = isMuted ? 0f : 0.34f * masterVolume;
            bgmSource.Play();

            weatherSource.clip = rainLoopClip;
            weatherSource.loop = true;
            weatherSource.volume = 0f;
            weatherSource.Play();
        }

        private void EnsureAudioListener()
        {
            if (FindAnyObjectByType<AudioListener>() == null)
            {
                Camera cam = Camera.main;
                if (cam != null)
                    cam.gameObject.AddComponent<AudioListener>();
                else
                    gameObject.AddComponent<AudioListener>();
            }
        }

        private void CreateAudioSources()
        {
            bgmSource = gameObject.AddComponent<AudioSource>();
            bgmSource.playOnAwake = false;
            bgmSource.spatialBlend = 0f;

            weatherSource = gameObject.AddComponent<AudioSource>();
            weatherSource.playOnAwake = false;
            weatherSource.spatialBlend = 0f;

            sfxSource = gameObject.AddComponent<AudioSource>();
            sfxSource.playOnAwake = false;
            sfxSource.spatialBlend = 0f;
        }

        private void Update()
        {
            if (footstepCooldown > 0f)
                footstepCooldown -= Time.deltaTime;

            if (isMuted)
            {
                bgmSource.volume = 0f;
                weatherSource.volume = 0f;
                return;
            }

            bool isNight = CozyTownWorldBuilder.Instance != null && CozyTownWorldBuilder.Instance.IsNightTime;
            if (isNight != playingNightBgm)
            {
                playingNightBgm = isNight;
                bgmSource.clip = playingNightBgm ? nightBgmClip : dayBgmClip;
                bgmSource.Play();
            }

            float targetBgmVol = (playingNightBgm ? 0.30f : 0.35f) * masterVolume;
            bgmSource.volume = Mathf.MoveTowards(bgmSource.volume, targetBgmVol, Time.deltaTime * 0.4f);

            bool isOutdoors = CozyTownWorldBuilder.Instance == null ||
                              CozyTownWorldBuilder.Instance.CurrentBuilding == BuildingType.None;
            WeatherType weather = CozyTownWorldBuilder.Instance != null
                ? CozyTownWorldBuilder.Instance.CurrentWeather
                : WeatherType.Clear;

            float targetWeatherVol = 0f;
            if (isOutdoors)
            {
                if (weather == WeatherType.Rain)
                    targetWeatherVol = 0.28f * masterVolume;
                else if (weather == WeatherType.Snow)
                    targetWeatherVol = 0.16f * masterVolume;
            }
            weatherSource.volume = Mathf.MoveTowards(weatherSource.volume, targetWeatherVol, Time.deltaTime * 0.5f);
        }

        public static void ToggleMute()
        {
            EnsureInstance();
            Instance.isMuted = !Instance.isMuted;
            PlayerPrefs.SetInt(MutePrefKey, Instance.isMuted ? 1 : 0);
            PlayerPrefs.Save();

            if (!Instance.isMuted)
            {
                PlaySfx(CozySfxType.UiClick);
                CozyLifeHUD.PushToast("🔊 Đã BẬT âm thanh & nhạc nền!");
            }
            else
            {
                CozyLifeHUD.PushToast("🔇 Đã TẮT tiếng (Nhấn [M] để bật lại).");
            }
        }

        public static void PlaySfx(CozySfxType type, float volumeScale = 1f)
        {
            EnsureInstance();
            if (Instance == null || Instance.isMuted)
                return;

            if (type == CozySfxType.Footstep)
            {
                if (Instance.footstepCooldown > 0f)
                    return;
                Instance.footstepCooldown = 0.14f;
            }

            if (Instance.sfxClips.TryGetValue(type, out AudioClip clip) && clip != null)
            {
                Instance.sfxSource.pitch = (type == CozySfxType.Footstep)
                    ? UnityEngine.Random.Range(0.92f, 1.08f)
                    : 1.0f;
                Instance.sfxSource.PlayOneShot(clip, Mathf.Clamp01(0.65f * volumeScale * Instance.masterVolume));
            }
        }

        private void GenerateAllProceduralClips()
        {
            dayBgmClip = CreateDayBgmClip();
            nightBgmClip = CreateNightBgmClip();
            rainLoopClip = CreateRainAmbientClip();

            sfxClips[CozySfxType.Footstep] = CreateFootstepClip();
            sfxClips[CozySfxType.UiClick] = CreateTwoToneClip(587.3f, 880f, 0.065f, 0.45f);
            sfxClips[CozySfxType.Coin] = CreateArpeggioClip(new float[] { 987.77f, 1318.51f, 1567.98f }, 0.08f, 0.55f);
            sfxClips[CozySfxType.Door] = CreateTwoToneClip(240f, 330f, 0.14f, 0.50f);
            sfxClips[CozySfxType.Craft] = CreateArpeggioClip(new float[] { 392f, 493.88f, 587.33f }, 0.09f, 0.50f);
            sfxClips[CozySfxType.EatDrink] = CreateArpeggioClip(new float[] { 440f, 554.37f, 659.25f }, 0.08f, 0.48f);
            sfxClips[CozySfxType.Harvest] = CreateArpeggioClip(new float[] { 523.25f, 659.25f, 783.99f, 1046.50f }, 0.065f, 0.52f);
            sfxClips[CozySfxType.PlaceFurniture] = CreateTwoToneClip(349.23f, 523.25f, 0.10f, 0.55f);
            sfxClips[CozySfxType.PickupFurniture] = CreateTwoToneClip(523.25f, 349.23f, 0.09f, 0.48f);
            sfxClips[CozySfxType.SicknessOrAlert] = CreateTwoToneClip(370f, 261.63f, 0.16f, 0.58f);
            sfxClips[CozySfxType.Cure] = CreateArpeggioClip(new float[] { 523.25f, 659.25f, 783.99f, 987.77f, 1046.50f }, 0.075f, 0.55f);

            sfxClips[CozySfxType.Snore] = CreateSnoreClip();
            sfxClips[CozySfxType.Sizzle] = CreateSizzleClip();
            sfxClips[CozySfxType.SawWood] = CreateSawWoodClip();
            sfxClips[CozySfxType.CashRegister] = CreateCashRegisterClip();
            sfxClips[CozySfxType.Watering] = CreateWateringClip();
            sfxClips[CozySfxType.PageTurn] = CreatePageTurnClip();
            sfxClips[CozySfxType.DoctorHeal] = CreateDoctorHealClip();
            sfxClips[CozySfxType.BallBounce] = CreateBallBounceClip();
            sfxClips[CozySfxType.NetSwish] = CreateNetSwishClip();
            sfxClips[CozySfxType.Whistle] = CreateWhistleClip();
            sfxClips[CozySfxType.Cheer] = CreateCheerClip();
            sfxClips[CozySfxType.WolfHowl] = CreateWolfHowlClip();
            sfxClips[CozySfxType.BellGong] = CreateBellGongClip();
            sfxClips[CozySfxType.MysticChime] = CreateMysticChimeClip();
            sfxClips[CozySfxType.Gunshot] = CreateGunshotClip();
            sfxClips[CozySfxType.Splash] = CreateWateringClip();
            sfxClips[CozySfxType.ReelIn] = CreateTwoToneClip(880f, 1318f, 0.05f, 0.45f);
            sfxClips[CozySfxType.BicycleBell] = CreateTwoToneClip(2093f, 2637f, 0.08f, 0.60f);
            sfxClips[CozySfxType.PetBark] = CreateTwoToneClip(440f, 660f, 0.10f, 0.55f);
            sfxClips[CozySfxType.PianoTune] = CreateArpeggioClip(new float[] { 261.63f, 329.63f, 392f, 523.25f, 659.25f }, 0.18f, 0.65f);
            sfxClips[CozySfxType.GramophoneMusic] = CreateArpeggioClip(new float[] { 329.63f, 392f, 493.88f, 587.33f }, 0.22f, 0.55f);
        }

        private static AudioClip CreateDayBgmClip()
        {
            // 16 giây vòng lặp Lofi Acoustic / Kalimba ấm áp: Cmaj7 -> Am7 -> Fmaj7 -> G6
            float duration = 16.0f;
            int totalSamples = Mathf.RoundToInt(duration * SampleRate);
            float[] data = new float[totalSamples];

            float[][] chords = new float[][]
            {
                new float[] { 130.81f, 261.63f, 329.63f, 392.00f, 493.88f }, // Cmaj7
                new float[] { 110.00f, 220.00f, 261.63f, 329.63f, 392.00f }, // Am7
                new float[] { 87.31f,  174.61f, 261.63f, 329.63f, 440.00f }, // Fmaj7
                new float[] { 98.00f,  196.00f, 246.94f, 293.66f, 392.00f }  // G6
            };

            int[] arpPattern = { 1, 2, 3, 4, 3, 2, 4, 3 };
            float beatDur = 0.5f; // 32 nhịp x 0.5s = 16s

            for (int i = 0; i < totalSamples; i++)
            {
                float t = i / (float)SampleRate;
                int measure = Mathf.Clamp((int)(t / 4.0f), 0, 3);
                float[] chord = chords[measure];

                // Bass ấm chạy suốt ô nhịp
                float mTime = t % 4.0f;
                float bassEnv = Mathf.Exp(-mTime * 0.9f);
                float bass = Mathf.Sin(2f * Mathf.PI * chord[0] * t) * 0.22f * bassEnv;

                // Pad nền dịu nhẹ
                float pad = (Mathf.Sin(2f * Mathf.PI * chord[1] * t) +
                             Mathf.Sin(2f * Mathf.PI * chord[2] * t)) * 0.07f;

                // Giai điệu gảy Kalimba / Guitar
                int stepInMeasure = Mathf.Clamp((int)(mTime / beatDur), 0, 7);
                float noteLocalTime = mTime % beatDur;
                float pluckFreq = chord[arpPattern[stepInMeasure]];
                float pluckEnv = Mathf.Exp(-noteLocalTime * 6.5f) * Mathf.Clamp01(noteLocalTime / 0.012f);
                float pluck = (Mathf.Sin(2f * Mathf.PI * pluckFreq * t) +
                               0.35f * Mathf.Sin(4f * Mathf.PI * pluckFreq * t)) * 0.26f * pluckEnv;

                data[i] = Mathf.Clamp(bass + pad + pluck, -0.85f, 0.85f);
            }

            AudioClip clip = AudioClip.Create("CozyDayBGM", totalSamples, 1, SampleRate, false);
            clip.SetData(data, 0);
            return clip;
        }

        private static AudioClip CreateNightBgmClip()
        {
            // 16 giây vòng lặp Đêm yên bình: Piano Lullaby + Tiếng dế đêm nhẹ nhàng
            float duration = 16.0f;
            int totalSamples = Mathf.RoundToInt(duration * SampleRate);
            float[] data = new float[totalSamples];

            float[][] nightChords = new float[][]
            {
                new float[] { 174.61f, 261.63f, 329.63f, 440.00f }, // Fmaj7
                new float[] { 164.81f, 246.94f, 293.66f, 392.00f }, // Em7
                new float[] { 146.83f, 220.00f, 261.63f, 349.23f }, // Dm7
                new float[] { 130.81f, 196.00f, 246.94f, 329.63f }  // Cmaj7
            };

            int[] lullabySteps = { 0, 2, 3, 2 };

            for (int i = 0; i < totalSamples; i++)
            {
                float t = i / (float)SampleRate;
                int measure = Mathf.Clamp((int)(t / 4.0f), 0, 3);
                float[] chord = nightChords[measure];
                float mTime = t % 4.0f;

                int step = Mathf.Clamp((int)(mTime / 1.0f), 0, 3);
                float localT = mTime % 1.0f;
                float noteFreq = chord[lullabySteps[step]];
                float env = Mathf.Exp(-localT * 3.2f) * Mathf.Clamp01(localT / 0.025f);

                float bell = (Mathf.Sin(2f * Mathf.PI * noteFreq * t) +
                              0.2f * Mathf.Sin(6f * Mathf.PI * noteFreq * t)) * 0.24f * env;

                float pad = (Mathf.Sin(2f * Mathf.PI * chord[0] * t) +
                             Mathf.Sin(2f * Mathf.PI * chord[1] * t)) * 0.08f * Mathf.Exp(-mTime * 0.45f);

                // Tiếng dế đêm rất nhẹ ở tần số cao
                float cricketCycle = t % 2.0f;
                float cricket = 0f;
                if (cricketCycle > 0.2f && cricketCycle < 0.55f)
                {
                    float pulse = Mathf.Sin(2f * Mathf.PI * 28f * t) > 0f ? 1f : 0f;
                    cricket = Mathf.Sin(2f * Mathf.PI * 3950f * t) * 0.018f * pulse;
                }

                data[i] = Mathf.Clamp(bell + pad + cricket, -0.80f, 0.80f);
            }

            AudioClip clip = AudioClip.Create("CozyNightBGM", totalSamples, 1, SampleRate, false);
            clip.SetData(data, 0);
            return clip;
        }

        private static AudioClip CreateRainAmbientClip()
        {
            float duration = 4.0f;
            int totalSamples = Mathf.RoundToInt(duration * SampleRate);
            float[] data = new float[totalSamples];
            System.Random rng = new System.Random(12345);
            float prev = 0f;

            for (int i = 0; i < totalSamples; i++)
            {
                float white = (float)(rng.NextDouble() * 2.0 - 1.0);
                // Bộ lọc thông thấp tạo tiếng mưa rào ấm không bị chói
                prev = prev * 0.86f + white * 0.14f;
                data[i] = prev * 0.35f;
            }

            AudioClip clip = AudioClip.Create("CozyRainLoop", totalSamples, 1, SampleRate, false);
            clip.SetData(data, 0);
            return clip;
        }

        private static AudioClip CreateFootstepClip()
        {
            float duration = 0.075f;
            int totalSamples = Mathf.RoundToInt(duration * SampleRate);
            float[] data = new float[totalSamples];
            System.Random rng = new System.Random(777);

            for (int i = 0; i < totalSamples; i++)
            {
                float t = i / (float)SampleRate;
                float env = Mathf.Exp(-t * 48f);
                float thump = Mathf.Sin(2f * Mathf.PI * 135f * t) * 0.55f;
                float rustle = ((float)rng.NextDouble() * 2f - 1f) * 0.25f;
                data[i] = (thump + rustle) * env * 0.5f;
            }

            AudioClip clip = AudioClip.Create("SfxFootstep", totalSamples, 1, SampleRate, false);
            clip.SetData(data, 0);
            return clip;
        }

        private static AudioClip CreateTwoToneClip(float freq1, float freq2, float noteDuration, float amplitude)
        {
            int samplesPerNote = Mathf.RoundToInt(noteDuration * SampleRate);
            int totalSamples = samplesPerNote * 2;
            float[] data = new float[totalSamples];

            for (int i = 0; i < totalSamples; i++)
            {
                bool second = i >= samplesPerNote;
                int localIdx = second ? (i - samplesPerNote) : i;
                float lt = localIdx / (float)SampleRate;
                float freq = second ? freq2 : freq1;
                float env = Mathf.Exp(-lt * 16f) * Mathf.Clamp01(lt / 0.005f);
                data[i] = Mathf.Sin(2f * Mathf.PI * freq * lt) * amplitude * env;
            }

            AudioClip clip = AudioClip.Create($"SfxTwoTone_{freq1}_{freq2}", totalSamples, 1, SampleRate, false);
            clip.SetData(data, 0);
            return clip;
        }

        private static AudioClip CreateArpeggioClip(float[] freqs, float noteDuration, float amplitude)
        {
            int samplesPerNote = Mathf.RoundToInt(noteDuration * SampleRate);
            int totalSamples = samplesPerNote * freqs.Length;
            float[] data = new float[totalSamples];

            for (int n = 0; n < freqs.Length; n++)
            {
                float freq = freqs[n];
                for (int j = 0; j < samplesPerNote; j++)
                {
                    int idx = n * samplesPerNote + j;
                    float lt = j / (float)SampleRate;
                    float env = Mathf.Exp(-lt * 14f) * Mathf.Clamp01(lt / 0.005f);
                    data[idx] = (Mathf.Sin(2f * Mathf.PI * freq * lt) +
                                 0.25f * Mathf.Sin(4f * Mathf.PI * freq * lt)) * amplitude * env;
                }
            }

            AudioClip clip = AudioClip.Create("SfxArpeggio", totalSamples, 1, SampleRate, false);
            clip.SetData(data, 0);
            return clip;
        }

        private static AudioClip CreateSnoreClip()
        {
            // Tiếng thở ngáy khò khò nhè nhẹ êm ái khi ngủ (0.85s)
            float duration = 0.85f;
            int totalSamples = Mathf.RoundToInt(duration * SampleRate);
            float[] data = new float[totalSamples];
            System.Random rng = new System.Random(4321);

            for (int i = 0; i < totalSamples; i++)
            {
                float t = i / (float)SampleRate;
                float breathCycle = Mathf.Sin(Mathf.PI * (t / duration));
                float freq = 90f + Mathf.Sin(2f * Mathf.PI * 6f * t) * 18f;
                float hum = Mathf.Sin(2f * Mathf.PI * freq * t) * 0.45f;
                float rasp = ((float)rng.NextDouble() * 2f - 1f) * 0.18f;
                data[i] = (hum + rasp) * breathCycle * 0.42f;
            }

            AudioClip clip = AudioClip.Create("SfxSnore", totalSamples, 1, SampleRate, false);
            clip.SetData(data, 0);
            return clip;
        }

        private static AudioClip CreateSizzleClip()
        {
            // Tiếng dầu mỡ sôi xèo xèo khi xốc chảo nấu ăn (0.65s)
            float duration = 0.65f;
            int totalSamples = Mathf.RoundToInt(duration * SampleRate);
            float[] data = new float[totalSamples];
            System.Random rng = new System.Random(8888);
            float prev = 0f;

            for (int i = 0; i < totalSamples; i++)
            {
                float t = i / (float)SampleRate;
                float env = Mathf.Sin(Mathf.PI * (t / duration));
                float white = (float)(rng.NextDouble() * 2.0 - 1.0);
                prev = prev * 0.65f + white * 0.35f;
                float crackle = (rng.NextDouble() > 0.985) ? 0.85f : 0f;
                data[i] = (prev * 0.38f + crackle) * env * 0.45f;
            }

            AudioClip clip = AudioClip.Create("SfxSizzle", totalSamples, 1, SampleRate, false);
            clip.SetData(data, 0);
            return clip;
        }

        private static AudioClip CreateSawWoodClip()
        {
            // Tiếng kéo cưa gỗ kèn kẹt nhịp nhàng (0.55s)
            float duration = 0.55f;
            int totalSamples = Mathf.RoundToInt(duration * SampleRate);
            float[] data = new float[totalSamples];
            System.Random rng = new System.Random(1111);

            for (int i = 0; i < totalSamples; i++)
            {
                float t = i / (float)SampleRate;
                float env = Mathf.Sin(Mathf.PI * (t / duration));
                float sawTone = Mathf.Sin(2f * Mathf.PI * 340f * t) * 0.35f;
                float grit = ((float)rng.NextDouble() * 2f - 1f) * 0.45f;
                data[i] = (sawTone + grit) * env * 0.50f;
            }

            AudioClip clip = AudioClip.Create("SfxSawWood", totalSamples, 1, SampleRate, false);
            clip.SetData(data, 0);
            return clip;
        }

        private static AudioClip CreateCashRegisterClip()
        {
            // Tiếng máy tính tiền cạch bánh răng + tiếng chuông "Kaching Ding!" (0.42s)
            float duration = 0.42f;
            int totalSamples = Mathf.RoundToInt(duration * SampleRate);
            float[] data = new float[totalSamples];

            for (int i = 0; i < totalSamples; i++)
            {
                float t = i / (float)SampleRate;
                float click = (t < 0.12f) ? Mathf.Sin(2f * Mathf.PI * 480f * t) * Mathf.Exp(-t * 30f) * 0.35f : 0f;
                float ding = (t >= 0.08f) ? Mathf.Sin(2f * Mathf.PI * 1760f * (t - 0.08f)) * Mathf.Exp(-(t - 0.08f) * 8f) * 0.55f : 0f;
                data[i] = click + ding;
            }

            AudioClip clip = AudioClip.Create("SfxCashRegister", totalSamples, 1, SampleRate, false);
            clip.SetData(data, 0);
            return clip;
        }

        private static AudioClip CreateWateringClip()
        {
            // Tiếng nước tưới rào rào xối xả dịu mát (0.50s)
            float duration = 0.50f;
            int totalSamples = Mathf.RoundToInt(duration * SampleRate);
            float[] data = new float[totalSamples];
            System.Random rng = new System.Random(2468);

            for (int i = 0; i < totalSamples; i++)
            {
                float t = i / (float)SampleRate;
                float env = Mathf.Sin(Mathf.PI * (t / duration));
                float noise = ((float)rng.NextDouble() * 2f - 1f) * 0.45f;
                float trickle = Mathf.Sin(2f * Mathf.PI * 620f * t) * 0.20f;
                data[i] = (noise + trickle) * env * 0.50f;
            }

            AudioClip clip = AudioClip.Create("SfxWatering", totalSamples, 1, SampleRate, false);
            clip.SetData(data, 0);
            return clip;
        }

        private static AudioClip CreatePageTurnClip()
        {
            // Tiếng lật trang sách sột soạt nhẹ nhàng (0.28s)
            float duration = 0.28f;
            int totalSamples = Mathf.RoundToInt(duration * SampleRate);
            float[] data = new float[totalSamples];
            System.Random rng = new System.Random(1357);

            for (int i = 0; i < totalSamples; i++)
            {
                float t = i / (float)SampleRate;
                float env = Mathf.Exp(-t * 18f) * Mathf.Sin(Mathf.PI * (t / duration));
                float rustle = ((float)rng.NextDouble() * 2f - 1f) * 0.40f;
                data[i] = rustle * env * 0.45f;
            }

            AudioClip clip = AudioClip.Create("SfxPageTurn", totalSamples, 1, SampleRate, false);
            clip.SetData(data, 0);
            return clip;
        }

        private static AudioClip CreateDoctorHealClip()
        {
            // Tiếng chuông ấm áp chữa lành & hồi phục sinh lực (0.45s)
            return CreateArpeggioClip(new float[] { 587.33f, 739.99f, 880.00f, 1174.66f }, 0.08f, 0.48f);
        }

        private static AudioClip CreateBallBounceClip()
        {
            // Tiếng bóng nảy cồm cộp "Thud/Bong" (0.18s)
            float duration = 0.18f;
            int totalSamples = Mathf.RoundToInt(duration * SampleRate);
            float[] data = new float[totalSamples];

            for (int i = 0; i < totalSamples; i++)
            {
                float t = i / (float)SampleRate;
                float freq = Mathf.Lerp(260f, 95f, t / duration);
                float env = Mathf.Exp(-t * 26f);
                data[i] = Mathf.Sin(2f * Mathf.PI * freq * t) * env * 0.75f;
            }

            AudioClip clip = AudioClip.Create("SfxBallBounce", totalSamples, 1, SampleRate, false);
            clip.SetData(data, 0);
            return clip;
        }

        private static AudioClip CreateNetSwishClip()
        {
            // Tiếng bóng xé lưới rổ "Swish!" hoặc rung lưới gôn (0.22s)
            float duration = 0.22f;
            int totalSamples = Mathf.RoundToInt(duration * SampleRate);
            float[] data = new float[totalSamples];
            System.Random rng = new System.Random(7788);

            for (int i = 0; i < totalSamples; i++)
            {
                float t = i / (float)SampleRate;
                float env = Mathf.Sin(Mathf.PI * (t / duration));
                float noise = ((float)rng.NextDouble() * 2f - 1f) * 0.45f;
                float chord = Mathf.Sin(2f * Mathf.PI * 880f * t) * 0.15f;
                data[i] = (noise + chord) * env * 0.65f;
            }

            AudioClip clip = AudioClip.Create("SfxNetSwish", totalSamples, 1, SampleRate, false);
            clip.SetData(data, 0);
            return clip;
        }

        private static AudioClip CreateWhistleClip()
        {
            // Tiếng còi trọng tài thổi "Tuýt tuýt!" (0.32s)
            float duration = 0.32f;
            int totalSamples = Mathf.RoundToInt(duration * SampleRate);
            float[] data = new float[totalSamples];

            for (int i = 0; i < totalSamples; i++)
            {
                float t = i / (float)SampleRate;
                float env = (t < 0.12f)
                    ? Mathf.Sin(Mathf.PI * (t / 0.12f))
                    : ((t > 0.16f && t < 0.30f) ? Mathf.Sin(Mathf.PI * ((t - 0.16f) / 0.14f)) : 0f);
                float tone1 = Mathf.Sin(2f * Mathf.PI * 2600f * t);
                float tone2 = Mathf.Sin(2f * Mathf.PI * 2900f * t);
                data[i] = (tone1 + tone2) * 0.5f * env * 0.55f;
            }

            AudioClip clip = AudioClip.Create("SfxWhistle", totalSamples, 1, SampleRate, false);
            clip.SetData(data, 0);
            return clip;
        }

        private static AudioClip CreateCheerClip()
        {
            // Tiếng vỗ tay hoan hô rộn ràng khi ghi bàn (0.65s)
            float duration = 0.65f;
            int totalSamples = Mathf.RoundToInt(duration * SampleRate);
            float[] data = new float[totalSamples];
            System.Random rng = new System.Random(9999);

            for (int i = 0; i < totalSamples; i++)
            {
                float t = i / (float)SampleRate;
                float env = Mathf.Exp(-t * 3.5f) * Mathf.Sin(Mathf.PI * (t / duration));
                float clap = ((float)rng.NextDouble() * 2f - 1f) * 0.5f;
                float chime = Mathf.Sin(2f * Mathf.PI * 1046.5f * t) * 0.15f;
                data[i] = (clap + chime) * env * 0.60f;
            }

            AudioClip clip = AudioClip.Create("SfxCheer", totalSamples, 1, SampleRate, false);
            clip.SetData(data, 0);
            return clip;
        }

        private static AudioClip CreateWolfHowlClip()
        {
            // Tiếng sói hú rùng rợn trong đêm (1.25s) với tần số trượt từ 160Hz lên 320Hz rồi hạ dần xuống 200Hz
            float duration = 1.25f;
            int totalSamples = Mathf.RoundToInt(duration * SampleRate);
            float[] data = new float[totalSamples];

            for (int i = 0; i < totalSamples; i++)
            {
                float t = i / (float)SampleRate;
                float progress = t / duration;

                // Tần số trượt (pitch slide) với dao động vibrato
                float baseFreq = progress < 0.45f
                    ? Mathf.Lerp(160f, 320f, progress / 0.45f)
                    : Mathf.Lerp(320f, 200f, (progress - 0.45f) / 0.55f);

                float vibrato = Mathf.Sin(2f * Mathf.PI * 5.5f * t) * 12f;
                float freq = baseFreq + vibrato;

                float env = Mathf.Sin(Mathf.PI * Mathf.Clamp01(progress));
                float wave = Mathf.Sin(2f * Mathf.PI * freq * t) * 0.48f +
                             Mathf.Sin(4f * Mathf.PI * freq * t) * 0.16f;

                data[i] = Mathf.Clamp(wave * env * 0.70f, -0.9f, 0.9f);
            }

            AudioClip clip = AudioClip.Create("SfxWolfHowl", totalSamples, 1, SampleRate, false);
            clip.SetData(data, 0);
            return clip;
        }

        private static AudioClip CreateBellGongClip()
        {
            // Tiếng chuông nhà thờ thị trấn vang vọng điểm báo bình minh (1.4s)
            float duration = 1.4f;
            int totalSamples = Mathf.RoundToInt(duration * SampleRate);
            float[] data = new float[totalSamples];

            float[] harmonics = { 261.63f, 392.0f, 523.25f, 784.0f }; // C4, G4, C5, G5
            float[] weights = { 0.50f, 0.28f, 0.18f, 0.10f };

            for (int i = 0; i < totalSamples; i++)
            {
                float t = i / (float)SampleRate;
                float env = Mathf.Exp(-t * 2.6f) * Mathf.Clamp01(t / 0.006f);

                float sample = 0f;
                for (int h = 0; h < harmonics.Length; h++)
                {
                    sample += Mathf.Sin(2f * Mathf.PI * harmonics[h] * t) * weights[h];
                }

                data[i] = sample * env * 0.75f;
            }

            AudioClip clip = AudioClip.Create("SfxBellGong", totalSamples, 1, SampleRate, false);
            clip.SetData(data, 0);
            return clip;
        }

        private static AudioClip CreateMysticChimeClip()
        {
            // Tiếng chuông pha lê thần bí khi Tiên Tri soi bài (0.8s)
            float duration = 0.8f;
            int totalSamples = Mathf.RoundToInt(duration * SampleRate);
            float[] data = new float[totalSamples];
            float[] notes = { 587.33f, 783.99f, 987.77f, 1174.66f, 1567.98f };

            for (int i = 0; i < totalSamples; i++)
            {
                float t = i / (float)SampleRate;
                float sample = 0f;

                for (int n = 0; n < notes.Length; n++)
                {
                    float noteStartTime = n * 0.11f;
                    if (t >= noteStartTime)
                    {
                        float localT = t - noteStartTime;
                        float env = Mathf.Exp(-localT * 4.5f) * Mathf.Clamp01(localT / 0.01f);
                        sample += Mathf.Sin(2f * Mathf.PI * notes[n] * localT) * 0.20f * env;
                    }
                }

                data[i] = Mathf.Clamp(sample * 0.75f, -0.9f, 0.9f);
            }

            AudioClip clip = AudioClip.Create("SfxMysticChime", totalSamples, 1, SampleRate, false);
            clip.SetData(data, 0);
            return clip;
        }

        private static AudioClip CreateGunshotClip()
        {
            // Tiếng súng nổ đanh thép của Thợ Săn trước khi hy sinh (0.35s)
            float duration = 0.35f;
            int totalSamples = Mathf.RoundToInt(duration * SampleRate);
            float[] data = new float[totalSamples];
            System.Random rng = new System.Random(4444);
            float filterPrev = 0f;

            for (int i = 0; i < totalSamples; i++)
            {
                float t = i / (float)SampleRate;
                float env = Mathf.Exp(-t * 22f);
                float noise = ((float)rng.NextDouble() * 2f - 1f);
                filterPrev = filterPrev * 0.7f + noise * 0.3f;
                float thud = Mathf.Sin(2f * Mathf.PI * 85f * t) * Mathf.Exp(-t * 14f) * 0.65f;

                data[i] = Mathf.Clamp((filterPrev * 0.6f + thud) * env, -0.95f, 0.95f);
            }

            AudioClip clip = AudioClip.Create("SfxGunshot", totalSamples, 1, SampleRate, false);
            clip.SetData(data, 0);
            return clip;
        }
    }

    /// <summary>
    /// Hệ thống Lưu Game Bền Vững (Persistent Save / Load System):
    /// Lưu trữ toàn bộ trạng thái thế giới mở sang JSON (PlayerPrefs + File JSON):
    /// - Chỉ số sinh tồn, bệnh tật, Xu, nghề nghiệp, túi đồ 8 ô
    /// - Ngày, giờ, thời tiết, vị trí nhân vật & tòa nhà đang đứng
    /// - Các bất động sản đã mua mở khóa (BuyableCottage, BuyableTownhouse, BuyableVilla)
    /// - Trạng thái sinh trưởng của cả 9 ô đất nông trại
    /// - Toàn bộ nội thất trang trí người chơi đã đặt trong nhà riêng
    /// - Điểm thân thiết với 4 cư dân NPC (An, Minh, Lan, Hùng)
    /// </summary>
    public class CozySaveManager : MonoBehaviour
    {
        public static CozySaveManager Instance { get; private set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticState()
        {
            Instance = null;
        }

        private const string SavePrefsKey = "CozyLife_FullSave_v1";
        private float autoSaveTimer = 60f; // Tự động lưu mỗi 60 giây thực tế (= 1 tiếng trong game)

        private static string SaveFilePath => Path.Combine(Application.persistentDataPath, "cozylife_save_v1.json");

        public static void EnsureInstance()
        {
            if (Instance != null)
                return;

            GameObject go = new GameObject("CozySaveManager");
            DontDestroyOnLoad(go);
            Instance = go.AddComponent<CozySaveManager>();
        }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
        }

        private void Update()
        {
            if (CozyLifeMainMenu.IsActive)
                return;

            autoSaveTimer -= Time.deltaTime;
            if (autoSaveTimer <= 0f)
            {
                autoSaveTimer = 60f;
                SaveGame(false);
            }
        }

        private void OnApplicationQuit()
        {
            if (!CozyLifeMainMenu.IsActive)
            {
                SaveGame(false);
            }
        }

        private void OnApplicationPause(bool pauseStatus)
        {
            if (pauseStatus && !CozyLifeMainMenu.IsActive)
            {
                SaveGame(false);
            }
        }

        public static bool HasSaveData()
        {
            if (PlayerPrefs.HasKey(SavePrefsKey))
                return true;

            try
            {
                return File.Exists(SaveFilePath);
            }
            catch
            {
                return false;
            }
        }

        public static void SaveGame(bool showToast = true)
        {
            EnsureInstance();

            PlayerController pc = FindAnyObjectByType<PlayerController>();
            CharacterStats stats = FindAnyObjectByType<CharacterStats>();
            ProfessionAndInventory profInv = FindAnyObjectByType<ProfessionAndInventory>();
            CozyTownWorldBuilder world = CozyTownWorldBuilder.Instance;

            if (stats == null || profInv == null || world == null)
                return;

            CozyLifeSaveData data = new CozyLifeSaveData
            {
                saveVersion = 1,
                savedAtTimestamp = DateTime.Now.ToString("dd/MM HH:mm"),

                // 1. Nhân vật & Chỉ số sinh tồn
                playerName = profInv.PlayerName,
                currentProfession = profInv.CurrentProfession,
                coins = profInv.Coins,
                health = stats.Health,
                hunger = stats.Hunger,
                thirst = stats.Thirst,
                stamina = stats.Stamina,
                currentSickness = stats.CurrentSickness,
                sicknessDurationMinutes = stats.SicknessDurationMinutes,
                outdoorWeatherExposureMinutes = stats.OutdoorExposureMinutes,

                // 2. Thời gian Thế Giới, Thời tiết & Vị trí
                currentDay = world.CurrentDay,
                currentHour = world.CurrentHour,
                currentWeather = world.CurrentWeather,
                weatherHoursRemaining = world.WeatherHoursRemaining,
                nextWeatherEventHours = world.NextWeatherEventHours,
                currentBuilding = world.CurrentBuilding,
                playerPosX = pc != null ? pc.transform.position.x : 0f,
                playerPosY = pc != null ? pc.transform.position.y : 0f
            };

            // 3. Túi đồ 8 ô
            foreach (var slot in profInv.Inventory)
            {
                if (slot != null && slot.itemType != ItemType.None && slot.count > 0)
                {
                    data.inventory.Add(new InventorySlot(slot.itemType, slot.count));
                }
            }

            // 4. Các ngôi nhà đã mua mở khóa
            foreach (var door in BuildingDoorInteractable.AllDoors)
            {
                if (door != null && !door.IsExitDoor && door.PurchasePriceCoins > 0 && door.IsUnlocked)
                {
                    if (!data.unlockedBuildings.Contains(door.TargetBuilding))
                        data.unlockedBuildings.Add(door.TargetBuilding);
                }
            }

            // 5. Trạng thái 9 ô đất nông trại
            foreach (var plot in CropPlotInteractable.AllCropPlots)
            {
                if (plot == null) continue;
                data.cropPlots.Add(new SavedCropPlotData
                {
                    plotIndex = plot.PlotIndex,
                    cropStage = (int)plot.CurrentStage,
                    growthGameMinutes = plot.GrowthMinutes,
                    requiredGameMinutes = plot.RequiredMinutes
                });
            }

            // 6. Toàn bộ nội thất người chơi đã đặt trang trí
            foreach (var furn in CozyFurnitureAura.AllFurniture)
            {
                if (furn == null || !furn.IsPlayerPlaced) continue;
                data.placedFurniture.Add(new SavedPlacedFurnitureData
                {
                    itemType = furn.FurnitureItemType,
                    building = furn.RoomBuilding,
                    posX = furn.transform.position.x,
                    posY = furn.transform.position.y,
                    isFlipped = furn.IsFlipped
                });
            }

            // 7. Tình bạn với 4 NPC
            foreach (var friend in TownFriendAgent.AllFriends)
            {
                if (friend == null) continue;
                data.friends.Add(new SavedFriendData
                {
                    friendName = friend.FriendName,
                    friendshipPoints = friend.FriendshipPoints,
                    friendHealth = friend.FriendHealth,
                    friendHunger = friend.FriendHunger
                });
            }

            string json = JsonUtility.ToJson(data, true);
            PlayerPrefs.SetString(SavePrefsKey, json);
            PlayerPrefs.Save();

            try
            {
                File.WriteAllText(SaveFilePath, json);
            }
            catch
            {
                // Fallback an toàn vào PlayerPrefs nếu môi trường khóa ghi file
            }

            if (showToast)
            {
                CozyAudioManager.PlaySfx(CozySfxType.Coin);
                CozyLifeHUD.PushToast($"💾 Đã lưu game bền vững! (Ngày {data.currentDay} — {data.coins} Xu, {data.placedFurniture.Count} nội thất)");
            }
        }

        public static bool LoadGame(bool showToast = true)
        {
            EnsureInstance();

            string json = "";
            if (PlayerPrefs.HasKey(SavePrefsKey))
            {
                json = PlayerPrefs.GetString(SavePrefsKey, "");
            }
            else
            {
                try
                {
                    if (File.Exists(SaveFilePath))
                        json = File.ReadAllText(SaveFilePath);
                }
                catch
                {
                    json = "";
                }
            }

            if (string.IsNullOrWhiteSpace(json))
                return false;

            CozyLifeSaveData data;
            try
            {
                data = JsonUtility.FromJson<CozyLifeSaveData>(json);
            }
            catch
            {
                return false;
            }

            if (data == null)
                return false;

            PlayerController pc = FindAnyObjectByType<PlayerController>();
            CharacterStats stats = FindAnyObjectByType<CharacterStats>();
            ProfessionAndInventory profInv = FindAnyObjectByType<ProfessionAndInventory>();
            CozyTownWorldBuilder world = CozyTownWorldBuilder.Instance;

            if (stats != null)
            {
                stats.RestoreVitalsFromSave(
                    data.health, data.hunger, data.thirst, data.stamina,
                    data.currentSickness, data.sicknessDurationMinutes, data.outdoorWeatherExposureMinutes);
            }

            if (profInv != null)
            {
                profInv.RestoreStateFromSave(data.playerName, data.currentProfession, data.coins, data.inventory);
            }

            if (world != null)
            {
                world.RestoreWorldStateFromSave(
                    data.currentDay, data.currentHour,
                    data.currentWeather, data.weatherHoursRemaining, data.nextWeatherEventHours,
                    data.currentBuilding);
            }

            // Khôi phục các ngôi nhà đã mua
            if (data.unlockedBuildings != null)
            {
                foreach (var door in BuildingDoorInteractable.AllDoors)
                {
                    if (door != null && !door.IsExitDoor && door.PurchasePriceCoins > 0)
                    {
                        bool owned = data.unlockedBuildings.Contains(door.TargetBuilding);
                        door.SetUnlockedFromSave(owned);
                    }
                }
            }

            // Khôi phục 9 ô đất nông trại
            if (data.cropPlots != null && data.cropPlots.Count > 0)
            {
                foreach (var savedPlot in data.cropPlots)
                {
                    foreach (var plot in CropPlotInteractable.AllCropPlots)
                    {
                        if (plot != null && plot.PlotIndex == savedPlot.plotIndex)
                        {
                            plot.RestoreFromSave(
                                (CropPlotInteractable.CropStage)savedPlot.cropStage,
                                savedPlot.growthGameMinutes,
                                savedPlot.requiredGameMinutes);
                            break;
                        }
                    }
                }
            }

            // Xóa nội thất người chơi đặt cũ trước khi khôi phục từ bản lưu
            for (int i = CozyFurnitureAura.AllFurniture.Count - 1; i >= 0; i--)
            {
                var f = CozyFurnitureAura.AllFurniture[i];
                if (f != null && f.IsPlayerPlaced)
                {
                    Destroy(f.gameObject);
                }
            }

            // Khôi phục toàn bộ nội thất người chơi đã đặt trang trí
            if (data.placedFurniture != null)
            {
                foreach (var sf in data.placedFurniture)
                {
                    Vector3 pos = new Vector3(sf.posX, sf.posY, 0f);
                    CozyFurnitureAura.SpawnPlacedFurniture(sf.itemType, sf.building, pos, sf.isFlipped, true);
                }
            }

            // Khôi phục điểm thân thiết bạn bè
            if (data.friends != null)
            {
                foreach (var sf in data.friends)
                {
                    foreach (var friend in TownFriendAgent.AllFriends)
                    {
                        if (friend != null && friend.FriendName == sf.friendName)
                        {
                            friend.RestoreFromSave(sf.friendshipPoints, sf.friendHealth, sf.friendHunger);
                            break;
                        }
                    }
                }
            }

            // Đặt lại vị trí đứng của nhân vật nếu hợp lệ
            if (pc != null && (Mathf.Abs(data.playerPosX) > 0.01f || Mathf.Abs(data.playerPosY) > 0.01f))
            {
                pc.TeleportToPosition(new Vector3(data.playerPosX, data.playerPosY, 0f));
            }

            if (showToast)
            {
                CozyLifeHUD.PushToast($"📂 Đã tải dữ liệu lưu: Ngày {data.currentDay} ({data.coins} Xu, {data.placedFurniture.Count} nội thất)!");
            }
            return true;
        }

        public static void DeleteSaveData()
        {
            PlayerPrefs.DeleteKey(SavePrefsKey);
            PlayerPrefs.Save();
            try
            {
                if (File.Exists(SaveFilePath))
                    File.Delete(SaveFilePath);
            }
            catch
            {
            }
            CozyAudioManager.PlaySfx(CozySfxType.SicknessOrAlert);
            CozyLifeHUD.PushToast("🗑️ Đã xóa dữ liệu lưu cũ! Bạn sẽ bắt đầu hành trình hoàn toàn mới.");
        }
    }
}
