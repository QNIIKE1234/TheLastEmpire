using NUnit.Framework;
using UnityEngine;
using TheLastEmpire.Runtime.Map;

namespace TheLastEmpire.Tests.EditMode
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
        public void GenerateMap_Populates4096Cells()
        {
            Assert.IsNull(_generator.gridData);

            _generator.GenerateMap();

            Assert.IsNotNull(_generator.gridData);
            Assert.AreEqual(4096, _generator.gridData.Length);
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
                    Assert.IsNotNull(stage, $"Stage at ({x}, {y}) should not be null");
                    Assert.AreEqual(x, stage.x);
                    Assert.AreEqual(y, stage.y);
                }
            }
        }

        [Test]
        public void GetStage_OutOfBoundsReturnsNull()
        {
            _generator.GenerateMap();

            Assert.IsNull(_generator.GetStage(-1, 0));
            Assert.IsNull(_generator.GetStage(0, -1));
            Assert.IsNull(_generator.GetStage(WorldMapGenerator.GridSize, 0));
            Assert.IsNull(_generator.GetStage(0, WorldMapGenerator.GridSize));
        }

        [Test]
        public void GeneratePreviewTexture_ReturnsCorrectSizeTexture()
        {
            _generator.GenerateMap();
            Texture2D preview = _generator.GeneratePreviewTexture();

            Assert.IsNotNull(preview);
            Assert.AreEqual(WorldMapGenerator.GridSize, preview.width);
            Assert.AreEqual(WorldMapGenerator.GridSize, preview.height);
            
            // Clean up dynamically created texture
            Object.DestroyImmediate(preview);
        }
    }
}
