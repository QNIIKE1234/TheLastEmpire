using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace TheLastEmpire
{
    public interface IShiftAbility
    {
        string AbilityName { get; }
        string Description { get; }
        bool IsActive { get; }
        float CooldownRemaining { get; }
        float CooldownDuration { get; }

        void Initialize(PlayerController player);
        void OnShiftPressed();
        void OnShiftHeld();
        void OnShiftReleased();
        void UpdateAbility();
        void OnDestroy();
    }
}
