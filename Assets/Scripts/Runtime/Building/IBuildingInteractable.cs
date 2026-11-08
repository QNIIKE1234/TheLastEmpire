using UnityEngine;
using System.Collections.Generic;

namespace TheLastEmpire
{
    /// <summary>
    /// Interface สำหรับสิ่งปลูกสร้างหรือวัตถุที่สามารถโต้ตอบได้ (Interactions)
    /// ทำงานร่วมกับ LootUI (Loot_HUD) และ PlayerController เพื่อแสดง Prompt เพียงอันเดียวที่ใกล้ที่สุด
    /// </summary>
    public interface IBuildingInteractable
    {
        Transform InteractionTransform { get; }
        Vector3 PromptWorldOffset { get; }
        float InteractionDistance { get; }
        bool CanInteract { get; }
        string GetInteractionPrompt();
        void OnInteract();
        void OnSecondaryInteract();
    }

    /// <summary>
    /// ทะเบียนศูนย์กลางสำหรับติดตามสิ่งปลูกสร้างที่สามารถโต้ตอบได้ทั้งหมดในฉาก
    /// </summary>
    public static class BuildingInteractionRegistry
    {
        public static readonly List<IBuildingInteractable> ActiveInteractables = new List<IBuildingInteractable>();

        public static void Register(IBuildingInteractable interactable)
        {
            if (interactable != null && !ActiveInteractables.Contains(interactable))
            {
                ActiveInteractables.Add(interactable);
            }
        }

        public static void Unregister(IBuildingInteractable interactable)
        {
            if (interactable != null)
            {
                ActiveInteractables.Remove(interactable);
            }
        }
    }
}

