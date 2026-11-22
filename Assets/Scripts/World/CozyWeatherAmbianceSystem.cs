using System.Collections.Generic;
using UnityEngine;
using CozyLife.Core;

namespace CozyLife.World
{
    /// <summary>
    /// Hệ thống Hiệu ứng Khí hậu & Bầu không khí Thị trấn (Weather & Seasonal Ambiance):
    /// - Mưa rào (Rain): Vệt mưa rơi nghiêng rơi nhanh kèm tiếng mưa rì rào và gợn sóng nước nhỏ.
    /// - Tuyết rơi (Snow): Bông tuyết trắng muốt lững lờ đung đưa theo gió.
    /// - Nắng ráo (Clear): Cánh hoa Anh đào (Sakura) hồng phấn rơi nhẹ bay lượn trong công viên.
    /// - Tự động ẩn khi người chơi đi vào bên trong các căn nhà / quán xá.
    /// </summary>
    public class CozyWeatherAmbianceSystem : MonoBehaviour
    {
        public static CozyWeatherAmbianceSystem Instance { get; private set; }

        private const int MaxParticles = 48;
        private readonly List<AmbianceParticle> activeParticles = new List<AmbianceParticle>();
        private Camera targetCamera;
        private WeatherType lastWeather = WeatherType.Clear;

        private class AmbianceParticle
        {
            public GameObject obj;
            public SpriteRenderer sr;
            public Vector3 velocity;
            public float swayFrequency;
            public float swayAmplitude;
            public float phase;
            public float lifetime;
            public float maxLifetime;
            public ParticleType type;
        }

        private enum ParticleType
        {
            RainDrop,
            SnowFlake,
            SakuraPetal
        }

        private Sprite rainStreakSprite;
        private Sprite snowFlakeSprite;
        private Sprite sakuraPetalSprite;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            targetCamera = Camera.main;

            CreateParticleSprites();
        }

        private void CreateParticleSprites()
        {
            // 1. Rain streak sprite (2x12 px crisp cyan streak)
            Texture2D rainTex = new Texture2D(2, 12, TextureFormat.RGBA32, false);
            Color[] rainColors = new Color[24];
            for (int y = 0; y < 12; y++)
            {
                float a = Mathf.Lerp(0.15f, 0.85f, (float)y / 11f);
                Color c = new Color(0.75f, 0.90f, 1f, a);
                rainColors[y * 2] = c;
                rainColors[y * 2 + 1] = c;
            }
            rainTex.SetPixels(rainColors);
            rainTex.filterMode = FilterMode.Bilinear;
            rainTex.Apply();
            rainStreakSprite = Sprite.Create(rainTex, new Rect(0, 0, 2, 12), new Vector2(0.5f, 0.5f), 32f);

            // 2. Snow flake sprite (6x6 px soft white circle)
            Texture2D snowTex = new Texture2D(8, 8, TextureFormat.RGBA32, false);
            Color[] snowColors = new Color[64];
            for (int y = 0; y < 8; y++)
            {
                for (int x = 0; x < 8; x++)
                {
                    float dist = Vector2.Distance(new Vector2(x, y), new Vector2(3.5f, 3.5f));
                    float a = Mathf.Clamp01(1f - (dist / 3.5f));
                    snowColors[y * 8 + x] = new Color(1f, 1f, 1f, a * 0.90f);
                }
            }
            snowTex.SetPixels(snowColors);
            snowTex.filterMode = FilterMode.Bilinear;
            snowTex.Apply();
            snowFlakeSprite = Sprite.Create(snowTex, new Rect(0, 0, 8, 8), new Vector2(0.5f, 0.5f), 32f);

            // 3. Sakura petal sprite (8x8 px delicate pink petal)
            Texture2D petalTex = new Texture2D(8, 8, TextureFormat.RGBA32, false);
            Color[] petalColors = new Color[64];
            for (int y = 0; y < 8; y++)
            {
                for (int x = 0; x < 8; x++)
                {
                    float dist = Vector2.Distance(new Vector2(x, y), new Vector2(3.5f, 3.5f));
                    float a = Mathf.Clamp01(1f - (dist / 3.2f));
                    petalColors[y * 8 + x] = new Color(1.0f, 0.65f, 0.78f, a * 0.85f);
                }
            }
            petalTex.SetPixels(petalColors);
            petalTex.filterMode = FilterMode.Bilinear;
            petalTex.Apply();
            sakuraPetalSprite = Sprite.Create(petalTex, new Rect(0, 0, 8, 8), new Vector2(0.5f, 0.5f), 32f);
        }

        private void Start()
        {
            SpawnParticlesPool();
        }

        private void SpawnParticlesPool()
        {
            for (int i = 0; i < MaxParticles; i++)
            {
                GameObject pObj = new GameObject($"WeatherParticle_{i}");
                pObj.transform.SetParent(transform);
                var sr = pObj.AddComponent<SpriteRenderer>();
                sr.sortingOrder = 3000; // Above all world objects, below HUD

                var p = new AmbianceParticle
                {
                    obj = pObj,
                    sr = sr
                };
                ResetParticle(p, true);
                activeParticles.Add(p);
            }
        }

        private void Update()
        {
            if (targetCamera == null)
                targetCamera = Camera.main;
            if (targetCamera == null) return;

            var world = CozyTownWorldBuilder.Instance;
            if (world == null) return;

            bool isOutdoor = (world.CurrentBuilding == BuildingType.None);
            WeatherType curWeather = world.CurrentWeather;

            if (curWeather != lastWeather)
            {
                lastWeather = curWeather;
                foreach (var p in activeParticles)
                {
                    ResetParticle(p, true);
                }
            }

            if (!isOutdoor)
            {
                // In room: hide all particles
                for (int i = 0; i < activeParticles.Count; i++)
                {
                    if (activeParticles[i].obj.activeSelf)
                        activeParticles[i].obj.SetActive(false);
                }
                return;
            }

            float dt = Time.deltaTime;
            Vector3 camPos = targetCamera.transform.position;

            for (int i = 0; i < activeParticles.Count; i++)
            {
                var p = activeParticles[i];
                if (!p.obj.activeSelf)
                    p.obj.SetActive(true);

                p.lifetime += dt;
                p.phase += dt * p.swayFrequency;

                // Position update
                Vector3 pos = p.obj.transform.position;
                pos += p.velocity * dt;

                if (p.type == ParticleType.SnowFlake || p.type == ParticleType.SakuraPetal)
                {
                    pos.x += Mathf.Sin(p.phase) * p.swayAmplitude * dt;
                }

                p.obj.transform.position = pos;

                // Check bounds relative to camera
                float camDistX = Mathf.Abs(pos.x - camPos.x);
                float camDistY = pos.y - camPos.y;

                if (p.lifetime >= p.maxLifetime || camDistY < -7f || camDistX > 12f)
                {
                    ResetParticle(p, false);
                }
            }
        }

        private void ResetParticle(AmbianceParticle p, bool randomizeY)
        {
            var world = CozyTownWorldBuilder.Instance;
            WeatherType weather = world != null ? world.CurrentWeather : WeatherType.Clear;

            Vector3 camPos = targetCamera != null ? targetCamera.transform.position : Vector3.zero;

            float spawnX = camPos.x + Random.Range(-11f, 11f);
            float spawnY = randomizeY ? (camPos.y + Random.Range(-6f, 8f)) : (camPos.y + Random.Range(6.5f, 9.0f));
            p.obj.transform.position = new Vector3(spawnX, spawnY, 0f);

            p.phase = Random.Range(0f, 6.28f);
            p.lifetime = 0f;
            p.maxLifetime = Random.Range(5f, 9f);

            switch (weather)
            {
                case WeatherType.Rain:
                    p.type = ParticleType.RainDrop;
                    p.sr.sprite = rainStreakSprite;
                    p.velocity = new Vector3(Random.Range(-2.2f, -1.2f), Random.Range(-14f, -10f), 0f);
                    p.swayFrequency = 0f;
                    p.swayAmplitude = 0f;
                    p.obj.transform.localScale = new Vector3(1.1f, 1.4f, 1f);
                    p.obj.transform.rotation = Quaternion.Euler(0f, 0f, 12f);
                    break;

                case WeatherType.Snow:
                    p.type = ParticleType.SnowFlake;
                    p.sr.sprite = snowFlakeSprite;
                    p.velocity = new Vector3(Random.Range(-0.8f, -0.2f), Random.Range(-1.6f, -0.9f), 0f);
                    p.swayFrequency = Random.Range(1.8f, 3.2f);
                    p.swayAmplitude = Random.Range(0.6f, 1.4f);
                    float snowScale = Random.Range(0.8f, 1.5f);
                    p.obj.transform.localScale = new Vector3(snowScale, snowScale, 1f);
                    p.obj.transform.rotation = Quaternion.identity;
                    break;

                default: // Clear / Nắng ráo: Cánh hoa Sakura rơi lãng mạn
                    p.type = ParticleType.SakuraPetal;
                    p.sr.sprite = sakuraPetalSprite;
                    p.velocity = new Vector3(Random.Range(-1.0f, -0.4f), Random.Range(-1.2f, -0.6f), 0f);
                    p.swayFrequency = Random.Range(2.2f, 4.0f);
                    p.swayAmplitude = Random.Range(0.8f, 1.8f);
                    float petalScale = Random.Range(1.0f, 1.6f);
                    p.obj.transform.localScale = new Vector3(petalScale, petalScale, 1f);
                    p.obj.transform.rotation = Quaternion.Euler(0f, 0f, Random.Range(0f, 360f));
                    break;
            }
        }
    }
}
