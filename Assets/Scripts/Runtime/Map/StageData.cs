using System;

namespace TheLastEmpire
{
    [Serializable]
    public class StageData
    {
        public int x;
        public int y;
        public BiomeType biome;
        public bool isExplored;
        public bool isCleared;
        public int stageSeed;
        public System.Collections.Generic.List<string> remainingEnemyPrefabNames;

        public StageData(int x, int y, BiomeType biome, int stageSeed)
        {
            this.x = x;
            this.y = y;
            this.biome = biome;
            this.isExplored = false;
            this.isCleared = false;
            this.stageSeed = stageSeed;
        }
    }
}
