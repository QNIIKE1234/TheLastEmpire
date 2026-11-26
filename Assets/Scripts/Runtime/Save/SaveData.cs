using System;
using System.Collections.Generic;

namespace TheLastEmpire
{
    [Serializable]
    public class SaveData
    {
        public int worldSeed;
        public int playerCoordX;
        public int playerCoordY;
        
        // List of 1D grid indices (x + y * 64) that have been explored / cleared
        public List<int> exploredStageIndices;
        public List<int> clearedStageIndices;

        // Persistent Roaming Alien World Boss state (50,000 HP, never resets)
        public int alienBossStageX = -1;
        public int alienBossStageY = -1;
        public float alienBossRemainingHp = 50000f;
        public bool alienBossDefeated = false;

        public SaveData()
        {
            exploredStageIndices = new List<int>();
            clearedStageIndices = new List<int>();
        }
    }
}
