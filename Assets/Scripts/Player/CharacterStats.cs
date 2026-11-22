using System;
using System.Collections.Generic;
using UnityEngine;
using CozyLife.Core;
using CozyLife.World;

namespace CozyLife.Player
{
    /// <summary>
    /// Quản lý các thanh chỉ số sinh tồn (Máu, Đói, Khát, Thể Lực) và hệ thống Moodles ngắn gọn.
    /// </summary>
    public class CharacterStats : MonoBehaviour
    {
        [Header("Max Vitals")]
        [SerializeField] private float maxHealth = 100f;
        [SerializeField] private float maxHunger = 100f;
        [SerializeField] private float maxThirst = 100f;
        [SerializeField] private float maxStamina = 100f;

        [Header("Current Vitals")]
        [SerializeField] private float health = 100f;
        [SerializeField] private float hunger = 85f;
        [SerializeField] private float thirst = 85f;
        [SerializeField] private float stamina = 100f;

        [Header("Decay & Regen Rates (1s = 1 phút game)")]
        [SerializeField] private float hungerDecayPerGameMin = 0.15f; // ~9 điểm / 1 tiếng game
        [SerializeField] private float thirstDecayPerGameMin = 0.20f; // ~12 điểm / 1 tiếng game
        [SerializeField] private float staminaRegenPerSec = 6.0f;
        [SerializeField] private float starvationDamagePerGameMin = 0.5f;
        [SerializeField] private float wellFedHealPerGameMin = 0.35f;

        [Header("Sickness & Bad Weather Exposure")]
        [SerializeField] private SicknessType currentSickness = SicknessType.None;
        [SerializeField] private float sicknessDurationMinutes = 0f;
        [SerializeField] private float outdoorWeatherExposureMinutes = 0f;
        private float sicknessCheckTimer = 0f;

        private readonly List<MoodleActiveInfo> activeMoodles = new List<MoodleActiveInfo>();
        private bool isNearCozyFurniture;

        public float Health => health;
        public float MaxHealth => maxHealth;
        public float Hunger => hunger;
        public float MaxHunger => maxHunger;
        public float Thirst => thirst;
        public float MaxThirst => maxThirst;
        public float Stamina => stamina;
        public float MaxStamina => maxStamina;

        public SicknessType CurrentSickness => currentSickness;
        public bool IsSick => currentSickness != SicknessType.None;
        public float SicknessDurationMinutes => sicknessDurationMinutes;
        public float OutdoorExposureMinutes => outdoorWeatherExposureMinutes;

        public event Action OnVitalsChanged;
        public event Action<string> OnStatusNotification;

        public void SetNearCozyFurniture(bool near)
        {
            isNearCozyFurniture = near;
        }

        public void RestoreVitalsFromSave(float hp, float hng, float thr, float stm, SicknessType sick, float sickMins, float expMins)
        {
            health = Mathf.Clamp(hp, 5f, maxHealth);
            hunger = Mathf.Clamp(hng, 0f, maxHunger);
            thirst = Mathf.Clamp(thr, 0f, maxThirst);
            stamina = Mathf.Clamp(stm, 0f, maxStamina);
            currentSickness = sick;
            sicknessDurationMinutes = Mathf.Max(0f, sickMins);
            outdoorWeatherExposureMinutes = Mathf.Max(0f, expMins);
            RefreshMoodles();
            OnVitalsChanged?.Invoke();
        }

        private void Update()
        {
            float dt = Time.deltaTime;
            float gameMinutesDelta = dt; // Chuẩn thế giới chung: 1 giây thực = 1 phút trong game (60s = 1h)

            // 1. Giảm Đói & Khát theo số phút trong game
            hunger = Mathf.Clamp(hunger - hungerDecayPerGameMin * gameMinutesDelta, 0f, maxHunger);
            thirst = Mathf.Clamp(thirst - thirstDecayPerGameMin * gameMinutesDelta, 0f, maxThirst);

            // 2. No Bụng (Well-Fed): Hồi máu nhẹ
            if (hunger >= 75f && thirst >= 50f && health < maxHealth)
            {
                health = Mathf.Clamp(health + wellFedHealPerGameMin * gameMinutesDelta, 0f, maxHealth);
            }

            // 3. Đói lả hoặc Kiệt nước (<= 0): Giảm máu
            if (hunger <= 0.01f)
            {
                health = Mathf.Clamp(health - starvationDamagePerGameMin * gameMinutesDelta, 0f, maxHealth);
            }
            if (thirst <= 0.01f)
            {
                health = Mathf.Clamp(health - starvationDamagePerGameMin * 1.2f * gameMinutesDelta, 0f, maxHealth);
            }

            // 4. Hồi phục Thể lực (Bị bệnh sẽ hồi chậm hơn đáng kể; Nội thất trang trí nhà tăng tốc hồi sức!)
            float regenMult = 1f;
            if (thirst < 25f) regenMult *= 0.5f;
            if (isNearCozyFurniture)
            {
                int roomCozy = 0;
                if (CozyTownWorldBuilder.Instance != null)
                {
                    roomCozy = CozyFurnitureAura.GetRoomCozyScore(CozyTownWorldBuilder.Instance.CurrentBuilding);
                }
                float cozyBonus = 0.60f + Mathf.Clamp(roomCozy * 0.005f, 0f, 0.90f);
                regenMult *= (1f + cozyBonus);
            }
            regenMult *= GetStaminaRegenMultiplier();

            stamina = Mathf.Clamp(stamina + staminaRegenPerSec * regenMult * dt, 0f, maxStamina);

            // 5. Cập nhật thời gian Bệnh tật & Tích lũy phơi nhiễm thời tiết xấu ngoài trời (10% nguy cơ)
            UpdateWeatherExposureAndSickness(gameMinutesDelta);

            // 6. Ngất xỉu khi hết máu
            if (health <= 0.01f)
            {
                RecoverAtClinic();
            }

            RefreshMoodles();
        }

        private void UpdateWeatherExposureAndSickness(float gameMinutesDelta)
        {
            if (currentSickness != SicknessType.None)
            {
                sicknessDurationMinutes -= gameMinutesDelta;
                if (sicknessDurationMinutes <= 0f)
                {
                    CureSickness("🌿 Cơ thể bạn đã tự đề kháng khỏi bệnh!");
                }
                return;
            }

            WeatherType weather = CozyTownWorldBuilder.Instance != null
                ? CozyTownWorldBuilder.Instance.CurrentWeather
                : WeatherType.Clear;

            bool isOutdoors = CozyTownWorldBuilder.Instance == null ||
                              CozyTownWorldBuilder.Instance.CurrentBuilding == BuildingType.None;

            if (isOutdoors && weather != WeatherType.Clear)
            {
                outdoorWeatherExposureMinutes += gameMinutesDelta;
                sicknessCheckTimer += gameMinutesDelta;

                // Khi đi dưới thời tiết xấu quá 20 phút game, cứ mỗi 15 phút game kiểm tra 1 lần: 10% bị bệnh!
                if (outdoorWeatherExposureMinutes >= 20f && sicknessCheckTimer >= 15f)
                {
                    sicknessCheckTimer = 0f;
                    if (UnityEngine.Random.value < 0.10f)
                    {
                        SicknessType targetSick = weather switch
                        {
                            WeatherType.Rain => SicknessType.Fever,
                            WeatherType.Heatwave => SicknessType.Heatstroke,
                            WeatherType.Snow => SicknessType.Cold,
                            _ => SicknessType.None
                        };

                        if (targetSick != SicknessType.None)
                        {
                            ContractSickness(targetSick);
                        }
                    }
                    else if (outdoorWeatherExposureMinutes >= 35f)
                    {
                        string warn = weather switch
                        {
                            WeatherType.Rain => "💧 Bạn dầm mưa lâu... Coi chừng bị sốt (10%)! Hãy tìm chỗ trú.",
                            WeatherType.Heatwave => "🔥 Bạn phơi nắng gắt lâu... Coi chừng sốc nhiệt (10%)! Hãy vào bóng râm.",
                            WeatherType.Snow => "❄️ Bạn dầm bão tuyết buốt cóng... Coi chừng cảm lạnh (10%)! Hãy vào nhà sưởi ấm.",
                            _ => ""
                        };
                        if (!string.IsNullOrEmpty(warn))
                            OnStatusNotification?.Invoke(warn);
                    }
                }
            }
            else
            {
                // Khi ở trong nhà hoặc trời quang ráo -> Hồi phục nhanh mức phơi nhiễm thời tiết
                if (outdoorWeatherExposureMinutes > 0f)
                {
                    outdoorWeatherExposureMinutes = Mathf.Max(0f, outdoorWeatherExposureMinutes - gameMinutesDelta * 2.5f);
                    sicknessCheckTimer = 0f;
                }
            }
        }

        public void ContractSickness(SicknessType sickness)
        {
            if (currentSickness == sickness)
                return;

            currentSickness = sickness;
            sicknessDurationMinutes = 12f * 60f; // Kéo dài tương đương 12 tiếng game nếu không chữa
            outdoorWeatherExposureMinutes = 0f;
            sicknessCheckTimer = 0f;

            string toastMsg = sickness switch
            {
                SicknessType.Fever => "🤒 ⚠️ BỊ SỐT do dầm mưa lâu! Tốc chạy -30%, tốn thể lực x1.6, hồi sức -55%!",
                SicknessType.Heatstroke => "🥵 ⚠️ BỊ SỐC NHIỆT do nắng gắt! Tốc chạy -35%, tốn thể lực x1.8, hồi sức -60%!",
                SicknessType.Cold => "🥶 ⚠️ BỊ CẢM LẠNH do bão tuyết! Tốc chạy -25%, tốn thể lực x1.5, hồi sức -50%!",
                _ => "⚠️ Bạn đã bị bệnh do thời tiết xấu!"
            };

            CozyAudioManager.PlaySfx(CozySfxType.SicknessOrAlert);
            OnStatusNotification?.Invoke(toastMsg);
            RefreshMoodles();
            OnVitalsChanged?.Invoke();
        }

        public void CureSickness(string reason = "✨ Bạn đã bình phục, khỏi bệnh hoàn toàn!")
        {
            if (currentSickness == SicknessType.None)
                return;

            currentSickness = SicknessType.None;
            sicknessDurationMinutes = 0f;
            outdoorWeatherExposureMinutes = 0f;

            CozyAudioManager.PlaySfx(CozySfxType.Cure);
            if (!string.IsNullOrEmpty(reason))
                OnStatusNotification?.Invoke(reason);

            RefreshMoodles();
            OnVitalsChanged?.Invoke();
        }

        public float GetSicknessSpeedMultiplier()
        {
            return currentSickness switch
            {
                SicknessType.Fever => 0.70f,       // Giảm 30% tốc độ di chuyển
                SicknessType.Heatstroke => 0.65f,  // Giảm 35% tốc độ di chuyển
                SicknessType.Cold => 0.75f,        // Giảm 25% tốc độ di chuyển
                _ => 1.0f
            };
        }

        public float GetStaminaDrainMultiplier()
        {
            return currentSickness switch
            {
                SicknessType.Fever => 1.60f,       // Tiêu hao thể lực nhiều hơn (+60%)
                SicknessType.Heatstroke => 1.80f,  // Tiêu hao thể lực nhiều hơn (+80%)
                SicknessType.Cold => 1.50f,        // Tiêu hao thể lực nhiều hơn (+50%)
                _ => 1.0f
            };
        }

        public float GetStaminaRegenMultiplier()
        {
            return currentSickness switch
            {
                SicknessType.Fever => 0.45f,       // Hồi thể lực lâu hơn (giảm 55%)
                SicknessType.Heatstroke => 0.40f,  // Hồi thể lực lâu hơn (giảm 60%)
                SicknessType.Cold => 0.50f,        // Hồi thể lực lâu hơn (giảm 50%)
                _ => 1.0f
            };
        }

        public void ModifyVitals(float deltaHealth, float deltaHunger, float deltaThirst, float deltaStamina)
        {
            health = Mathf.Clamp(health + deltaHealth, 0f, maxHealth);
            hunger = Mathf.Clamp(hunger + deltaHunger, 0f, maxHunger);
            thirst = Mathf.Clamp(thirst + deltaThirst, 0f, maxThirst);
            stamina = Mathf.Clamp(stamina + deltaStamina, 0f, maxStamina);

            RefreshMoodles();
            OnVitalsChanged?.Invoke();
        }

        public bool TryConsumeStamina(float amount)
        {
            float finalCost = amount * GetStaminaDrainMultiplier();
            if (stamina < finalCost)
                return false;

            stamina = Mathf.Clamp(stamina - finalCost, 0f, maxStamina);
            return true;
        }

        public float GetMovementSpeedMultiplier()
        {
            float mult = 1f;
            if (hunger >= 80f && thirst >= 70f)
                mult += 0.1f;
            if (hunger <= 15f)
                mult *= 0.85f;
            if (thirst <= 15f)
                mult *= 0.82f;
            if (health <= 35f)
                mult *= 0.8f;

            // Debuff tốc độ di chuyển khi bị bệnh
            mult *= GetSicknessSpeedMultiplier();
            return mult;
        }

        private void RecoverAtClinic()
        {
            health = maxHealth * 0.65f;
            hunger = maxHunger * 0.55f;
            thirst = maxThirst * 0.65f;
            stamina = maxStamina * 0.8f;
            transform.position = new Vector3(0f, 0f, 0f);
            OnStatusNotification?.Invoke("⚕️ Bạn ngất vì kiệt sức và đã được Bác Sĩ sơ cứu!");
        }

        public IReadOnlyList<MoodleActiveInfo> GetActiveMoodles()
        {
            return activeMoodles;
        }

        private void RefreshMoodles()
        {
            activeMoodles.Clear();

            if (hunger >= 75f)
            {
                activeMoodles.Add(new MoodleActiveInfo
                {
                    type = MoodleType.WellFed,
                    title = "🍖 No Bụng",
                    subtitle = "+Hồi máu, +10% Tốc chạy",
                    badgeColor = new Color(0.28f, 0.76f, 0.36f),
                    isPositive = true
                });
            }
            else if (hunger <= 10f)
            {
                activeMoodles.Add(new MoodleActiveInfo
                {
                    type = MoodleType.Starving,
                    title = "🍖 Đói Lả!",
                    subtitle = "Đang mất máu! Cần ăn ngay",
                    badgeColor = new Color(0.90f, 0.22f, 0.22f),
                    isPositive = false
                });
            }
            else if (hunger <= 38f)
            {
                activeMoodles.Add(new MoodleActiveInfo
                {
                    type = MoodleType.Hungry,
                    title = "🍖 Đói Bụng",
                    subtitle = "Cần ăn Bánh Mì / Súp",
                    badgeColor = new Color(0.92f, 0.62f, 0.20f),
                    isPositive = false
                });
            }

            if (thirst >= 78f)
            {
                activeMoodles.Add(new MoodleActiveInfo
                {
                    type = MoodleType.Hydrated,
                    title = "💧 Đủ Nước",
                    subtitle = "+Hồi thể lực nhanh",
                    badgeColor = new Color(0.24f, 0.68f, 0.92f),
                    isPositive = true
                });
            }
            else if (thirst <= 10f)
            {
                activeMoodles.Add(new MoodleActiveInfo
                {
                    type = MoodleType.Dehydrated,
                    title = "💧 Kiệt Nước!",
                    subtitle = "Đi chậm & mất máu!",
                    badgeColor = new Color(0.90f, 0.22f, 0.22f),
                    isPositive = false
                });
            }
            else if (thirst <= 40f)
            {
                activeMoodles.Add(new MoodleActiveInfo
                {
                    type = MoodleType.Thirsty,
                    title = "💧 Khát Nước",
                    subtitle = "Cần uống Nước / Trà",
                    badgeColor = new Color(0.92f, 0.62f, 0.20f),
                    isPositive = false
                });
            }

            if (health <= 45f)
            {
                activeMoodles.Add(new MoodleActiveInfo
                {
                    type = MoodleType.Injured,
                    title = "❤️ Bị Thương",
                    subtitle = "Cần Bác Sĩ / Túi Sơ Cứu",
                    badgeColor = new Color(0.88f, 0.25f, 0.30f),
                    isPositive = false
                });
            }

            if (stamina <= 25f)
            {
                activeMoodles.Add(new MoodleActiveInfo
                {
                    type = MoodleType.Tired,
                    title = "⚡ Mệt Mỏi",
                    subtitle = "Cần ngồi ghế / ngủ nghỉ",
                    badgeColor = new Color(0.85f, 0.55f, 0.22f),
                    isPositive = false
                });
            }

            if (isNearCozyFurniture)
            {
                int roomCozy = CozyTownWorldBuilder.Instance != null
                    ? CozyFurnitureAura.GetRoomCozyScore(CozyTownWorldBuilder.Instance.CurrentBuilding)
                    : 0;
                int bonusPct = 60 + Mathf.Clamp(Mathf.RoundToInt(roomCozy * 0.5f), 0, 90);
                activeMoodles.Add(new MoodleActiveInfo
                {
                    type = MoodleType.CozyRested,
                    title = roomCozy > 0 ? $"🛋️ Ấm Cúng ({roomCozy}đ)" : "🪑 Ấm Cúng",
                    subtitle = $"+{bonusPct}% Hồi thể lực",
                    badgeColor = new Color(0.75f, 0.52f, 0.88f),
                    isPositive = true
                });
            }

            // Moodles Debuff Bệnh Tật Thời Tiết (Ưu tiên hiển thị nổi bật)
            if (currentSickness == SicknessType.Fever)
            {
                activeMoodles.Insert(0, new MoodleActiveInfo
                {
                    type = MoodleType.SickFever,
                    title = "🤒 Bị Sốt (Mưa)",
                    subtitle = "Tốc độ -30%, Thể lực tốn x1.6, Hồi -55%",
                    badgeColor = new Color(0.96f, 0.42f, 0.20f),
                    isPositive = false
                });
            }
            else if (currentSickness == SicknessType.Heatstroke)
            {
                activeMoodles.Insert(0, new MoodleActiveInfo
                {
                    type = MoodleType.SickHeatstroke,
                    title = "🥵 Sốc Nhiệt (Nắng)",
                    subtitle = "Tốc độ -35%, Thể lực tốn x1.8, Hồi -60%",
                    badgeColor = new Color(0.95f, 0.18f, 0.22f),
                    isPositive = false
                });
            }
            else if (currentSickness == SicknessType.Cold)
            {
                activeMoodles.Insert(0, new MoodleActiveInfo
                {
                    type = MoodleType.SickCold,
                    title = "🥶 Cảm Lạnh (Tuyết)",
                    subtitle = "Tốc độ -25%, Thể lực tốn x1.5, Hồi -50%",
                    badgeColor = new Color(0.32f, 0.75f, 0.95f),
                    isPositive = false
                });
            }
        }
    }
}
