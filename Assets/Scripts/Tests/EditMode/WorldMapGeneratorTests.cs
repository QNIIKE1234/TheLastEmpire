using NUnit.Framework;
using UnityEngine;

namespace TheLastEmpire
{
    public class WorldMapGeneratorTests
    {
        private WorldMapGenerator _generator;

        [SetUp]
        public void SetUp()
        {
            _generator = ScriptableObject.CreateInstance<WorldMapGenerator>();
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_generator);
        }

        [Test]
        public void GenerateMap_PopulatesExpectedCells()
        {
            Assert.IsNull(_generator.gridData);

            _generator.GenerateMap();

            Assert.IsNotNull(_generator.gridData);
            Assert.AreEqual(WorldMapGenerator.GridSize * WorldMapGenerator.GridSize, _generator.gridData.Length);
        }

        [Test]
        public void GenerateMap_CoordinatesAreAssignedCorrectly()
        {
            _generator.GenerateMap();

            for (int y = 0; y < WorldMapGenerator.GridSize; y++)
            {
                for (int x = 0; x < WorldMapGenerator.GridSize; x++)
                {
                    StageData stage = _generator.GetStage(x, y);
                    Assert.IsNotNull(stage);
                    Assert.AreEqual(x, stage.x);
                    Assert.AreEqual(y, stage.y);
                }
            }
        }
    }
}
