using System;

namespace TheLastEmpire
{
    [Serializable]
    public struct DroppedItemData
    {
        public string itemName;
        public int quantity;
        public bool isMoney;
        public int moneyAmount;
        public float posX;
        public float posY;
    }

    [Serializable]
    public class StageData
    {
        public int x;
        public int y;
        public BiomeType biome;
        public bool isExplored;
        public bool isCleared;
        public int stageSeed;
        public bool isShopStage;
        public System.Collections.Generic.List<string> remainingEnemyPrefabNames;
        public System.Collections.Generic.List<ShopItemConfig> savedShopItems;
        public System.Collections.Generic.List<DroppedItemData> droppedItems = new System.Collections.Generic.List<DroppedItemData>();

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
