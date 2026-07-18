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

        public SaveData()
        {
            exploredStageIndices = new List<int>();
            clearedStageIndices = new List<int>();
        }
    }
}
