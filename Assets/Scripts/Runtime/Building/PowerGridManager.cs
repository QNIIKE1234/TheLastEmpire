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
        /// ตรวจสอบว่ามีเครื่องผลิตไฟฟ้าที่กำลังทำงานครอบคลุมตำแหน่งนี้หรือไม่
        /// </summary>
        public static bool IsPositionPowered(Vector3 position)
        {
            for (int i = 0; i < _generators.Count; i++)
            {
                GeneratorBuilding gen = _generators[i];
                if (gen != null && gen.IsRunning)
                {
                    if (Vector3.Distance(position, gen.transform.position) <= gen.PowerRadius)
                    {
                        return true;
                    }
                }
            }
            return false;
        }

        /// <summary>
        /// คำนวณกำลังผลิตไฟฟ้ารวม (Supply) ที่จ่ายถึงตำแหน่งนี้ (วัตต์)
        /// </summary>
        public static float GetAvailableSupply(Vector3 position)
        {
            float total = 0f;
            for (int i = 0; i < _generators.Count; i++)
            {
                GeneratorBuilding gen = _generators[i];
                if (gen != null && gen.IsRunning)
                {
                    if (Vector3.Distance(position, gen.transform.position) <= gen.PowerRadius)
                    {
                        total += gen.PowerOutput;
                    }
                }
            }
            return total;
        }

        /// <summary>
        /// คำนวณความต้องการไฟฟ้ารวม (Demand) ของเครื่องจักรทั้งหมดในรัศมีเครื่องปั่นไฟที่ตำแหน่งนี้เชื่อมต่อ
        /// </summary>
        public static float GetConnectedDemand(Vector3 position)
        {
            List<GeneratorBuilding> coveringGens = new List<GeneratorBuilding>();
            for (int i = 0; i < _generators.Count; i++)
            {
                GeneratorBuilding gen = _generators[i];
                if (gen != null && gen.IsRunning && Vector3.Distance(position, gen.transform.position) <= gen.PowerRadius)
                {
                    coveringGens.Add(gen);
                }
            }

            if (coveringGens.Count == 0) return 0f;

            float totalDemand = 0f;
            HashSet<IPowerConsumer> counted = new HashSet<IPowerConsumer>();

            for (int i = 0; i < _consumers.Count; i++)
            {
                IPowerConsumer c = _consumers[i];
                if (c == null || !c.IsConsumerActive || counted.Contains(c)) continue;

                for (int g = 0; g < coveringGens.Count; g++)
                {
                    if (Vector3.Distance(c.Position, coveringGens[g].transform.position) <= coveringGens[g].PowerRadius)
                    {
                        counted.Add(c);
                        totalDemand += c.PowerDemand;
                        break;
                    }
                }
            }

            return totalDemand;
        }

        /// <summary>
        /// คำนวณอัตราส่วนประสิทธิภาพไฟฟ้า (0.0 ถึง 1.0)
        /// ถ้าไม่มีเครื่องปั่นไฟ หรือไม่มีไฟฟ้าจ่ายถึง -> คืนค่า 0.0
        /// </summary>
        public static float GetPowerEfficiency(Vector3 position, float myDemand = 0f)
        {
            float supply = GetAvailableSupply(position);
            if (supply <= 0f)
            {
                return 0f; // ไม่มีเครื่องปั่นไฟเลย -> ประสิทธิภาพเป็น 0 เครื่องหยุดทำงาน
            }

            float demand = GetConnectedDemand(position);
            if (demand <= 0f) demand = myDemand;
            if (demand <= 0f) return 1f;

            return Mathf.Clamp01(supply / demand);
        }
    }
}
