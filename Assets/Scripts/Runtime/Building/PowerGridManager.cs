using System;
using System.Collections.Generic;
using UnityEngine;

namespace TheLastEmpire
{
    /// <summary>
    /// IPowerConsumer — อินเตอร์เฟซสำหรับสิ่งปลูกสร้างที่ต้องใช้พลังงานไฟฟ้า
    /// </summary>
    public interface IPowerConsumer
    {
        Vector3 Position { get; }
        float PowerDemand { get; }
        bool IsConsumerActive { get; }
    }

    /// <summary>
    /// PowerGridManager — ระบบจัดการโครงข่ายพลังงานไฟฟ้าประจำฐาน (Base Power Grid System)
    /// คุณสมบัติ:
    /// - รวมศูนย์การตรวจสอบกำลังผลิตไฟฟ้า (Power Supply) จากเครื่องปั่นไฟ (GeneratorBuilding)
    /// - คำนวณความต้องการไฟฟ้าทั้งหมด (Power Demand) จากเครื่องจักรผู้ใช้ไฟ (IPowerConsumer)
    /// - คำนวณประสิทธิภาพพลังงาน (Power Efficiency 0.0 ถึง 1.0):
    ///   * 0.0 = ไม่มีไฟฟ้าเลย (เช่น ยังไม่ได้สร้างเครื่องปั่นไฟ หรือไฟดับ) -> เครื่องจักรหยุดนิ่ง 100%
    ///   * 0.0 - 0.99 = พลังงานไม่พอ (Brownout) -> ความเร็วการทำงานลดลงตามสัดส่วน (เช่น ผลิตได้ 50 แต่ใช้ 80 = ความเร็ว 62.5%)
    ///   * 1.0 = ไฟฟ้าเพียงพอสมบูรณ์ -> ทำงานเต็มประสิทธิภาพ 100%
    /// </summary>
    public static class PowerGridManager
    {
        private static readonly List<GeneratorBuilding> _generators = new List<GeneratorBuilding>();
        private static readonly List<IPowerConsumer> _consumers = new List<IPowerConsumer>();

        public static event Action OnGridChanged;

        public static IReadOnlyList<GeneratorBuilding> Generators => _generators;
        public static IReadOnlyList<IPowerConsumer> Consumers => _consumers;

        public static float TotalSupply
        {
            get
            {
                float total = 0f;
                for (int i = 0; i < _generators.Count; i++)
                {
                    if (_generators[i] != null && _generators[i].IsRunning)
                    {
                        total += _generators[i].PowerOutput;
                    }
                }
                return total;
            }
        }

        public static float TotalDemand
        {
            get
            {
                float total = 0f;
                for (int i = 0; i < _consumers.Count; i++)
                {
                    if (_consumers[i] != null && _consumers[i].IsConsumerActive)
                    {
                        total += _consumers[i].PowerDemand;
                    }
                }
                return total;
            }
        }

        public static float GlobalEfficiency
        {
            get
            {
                float supply = TotalSupply;
                if (supply <= 0f) return 0f;
                float demand = TotalDemand;
                if (demand <= 0f) return 1f;
                return Mathf.Clamp01(supply / demand);
            }
        }

        public static int ActiveGeneratorsCount
        {
            get
            {
                int count = 0;
                for (int i = 0; i < _generators.Count; i++)
                {
                    if (_generators[i] != null && _generators[i].IsRunning) count++;
                }
                return count;
            }
        }

        public static int ActiveConsumersCount
        {
            get
            {
                int count = 0;
                for (int i = 0; i < _consumers.Count; i++)
                {
                    if (_consumers[i] != null && _consumers[i].IsConsumerActive) count++;
                }
                return count;
            }
        }

        public static void RegisterGenerator(GeneratorBuilding gen)
        {
            if (gen != null && !_generators.Contains(gen))
            {
                _generators.Add(gen);
                OnGridChanged?.Invoke();
            }
        }

        public static void UnregisterGenerator(GeneratorBuilding gen)
        {
            if (gen != null)
            {
                _generators.Remove(gen);
                OnGridChanged?.Invoke();
            }
        }

        public static void RegisterConsumer(IPowerConsumer consumer)
        {
            if (consumer != null && !_consumers.Contains(consumer))
            {
                _consumers.Add(consumer);
                OnGridChanged?.Invoke();
            }
        }

        public static void UnregisterConsumer(IPowerConsumer consumer)
        {
            if (consumer != null)
            {
                _consumers.Remove(consumer);
                OnGridChanged?.Invoke();
            }
        }

        /// <summary>
        /// ตรวจสอบว่ามีเครื่องผลิตไฟฟ้าที่กำลังทำงานในแมพหรือไม่ (ระบบไฟฟ้าครอบคลุมทั่วทั้งฐาน/ทั้งแมพ)
        /// </summary>
        public static bool IsPositionPowered(Vector3 position)
        {
            return TotalSupply > 0f;
        }

        /// <summary>
        /// กำลังผลิตไฟฟ้ารวม (Supply) ทั้งหมดในแมพ (วัตต์)
        /// </summary>
        public static float GetAvailableSupply(Vector3 position)
        {
            return TotalSupply;
        }

        /// <summary>
        /// ความต้องการไฟฟ้ารวม (Demand) ของเครื่องจักรทั้งหมดในแมพ (วัตต์)
        /// </summary>
        public static float GetConnectedDemand(Vector3 position)
        {
            return TotalDemand;
        }

        /// <summary>
        /// คำนวณอัตราส่วนประสิทธิภาพไฟฟ้าทั่วทั้งแมพ (0.0 ถึง 1.0)
        /// ถ้าไม่มีเครื่องปั่นไฟ หรือไม่มีไฟฟ้าจ่าย -> คืนค่า 0.0 (เครื่องหยุดทำงาน)
        /// ถ้าจ่ายไฟพอ -> คืนค่า 1.0 (100%)
        /// ถ้าไฟไม่พอ -> คืนค่าตามสัดส่วน Supply / Demand (Brownout)
        /// </summary>
        public static float GetPowerEfficiency(Vector3 position, float myDemand = 0f)
        {
            return GlobalEfficiency;
        }
    }
}
