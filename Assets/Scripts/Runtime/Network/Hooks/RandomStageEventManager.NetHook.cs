using UnityEngine;
using TheLastEmpire.Network;

namespace TheLastEmpire
{
    /// <summary>
    /// RandomStageEventManager.NetHook — ส่วนขยาย partial class สำหรับซิงค์อีเวนต์และคัตซีน (เช่น FleeingSurvivor)
    /// ให้เกิดพร้อมกันและใช้ Seed เดียวกัน 100% บนเครื่อง Host และ Client ทุกเครื่อง
    /// </summary>
    public partial class RandomStageEventManager
    {
        private bool ShouldDeferStageEventToHost()
        {
            return EmpireNetworkManager.IsOnlineClient;
        }

        /// <summary>
        /// สั่งรันอีเวนต์ประจำห้องด้วยชนิดอีเวนต์และ Seed ที่ Host กำหนดมาใน StageTransitionNetPayload
        /// รับประกันว่าคัตซีน Maya วิ่งหนี (FleeingSurvivor), กล่องแอร์ดรอป, หรือพ่อค้าพเนจร จะเกิดตรงกันทุกหน้าจอ
        /// </summary>
        public void ExecuteEventWithSeed(StageEventType eventType, StageData stage, int eventSeed)
        {
            if (stage == null || eventType == StageEventType.None) return;

            CleanupActiveEvent();
            stage.randomEventType = eventType;
            stage.hasEventTriggered = true;

            System.Random deterministicRng = new System.Random(eventSeed);
            Debug.Log($"<color=#38BDF8>[RandomStageEventManager.NetHook]</color> Executing synced stage event '{eventType}' with seed {eventSeed} at Stage ({stage.x}, {stage.y})");
            ExecuteEvent(eventType, stage, deterministicRng);
        }
    }
}
