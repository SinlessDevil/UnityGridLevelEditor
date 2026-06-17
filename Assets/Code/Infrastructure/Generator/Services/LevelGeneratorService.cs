using System;
using System.Collections.Generic;
using UnityEngine;
using Code.Infrastructure.Generator.Factory;
using Code.Infrastructure.Services.StaticData;
using Code.Infrastructure.StaticData;
using Code.LevelEditor;
using DG.Tweening;

namespace Code.Infrastructure.Generator.Services
{
    public class LevelGeneratorService : ILevelGeneratorService
    {
        // Occupied tiles use a blue checkerboard to highlight where blocks stand.
        private static readonly Color OccupiedColorEven = new Color(0.30f, 0.55f, 0.95f);
        private static readonly Color OccupiedColorNotEven = new Color(0.18f, 0.34f, 0.66f);

        private Tile[,] _tileMatrix;
        private GameObject _rootMapHolder;
        private int _currentLevelIndex = 1;

        private float _nextLevelDelay = 5f;
        private bool _autoSwitchEnabled = false;
        private float _timer;
        
        private readonly IStaticDataService _staticDataService;
        private readonly ITileFactory _tileFactory;
        private readonly IBlockFactory _blockFactory;

        public LevelGeneratorService(
            IStaticDataService staticDataService,
            ITileFactory tileFactory,
            IBlockFactory blockFactory)
        {
            _staticDataService = staticDataService;
            _tileFactory = tileFactory;
            _blockFactory = blockFactory;
        }

        public void SetUpRootMapHolder(GameObject rootMapHolder)
        {
            _rootMapHolder = rootMapHolder;
        }

        public void CleanUp(Action onComplete = null)
        {
            if (_tileMatrix == null)
            {
                onComplete?.Invoke();
                return;
            }

            int width = _tileMatrix.GetLength(0);
            int height = _tileMatrix.GetLength(1);
            float baseDelay = 0.01f;
            int counter = 0;
            int total = width * height;

            for (int y = height - 1; y >= 0; y--)
            {
                for (int x = width - 1; x >= 0; x--)
                {
                    Tile tile = _tileMatrix[x, y];
                    if (tile == null)
                    {
                        counter++;
                        continue;
                    }

                    float delay = ((width - x) + (height - y)) * baseDelay;
                    DOVirtual.DelayedCall(delay, () =>
                    {
                        tile.PlayAnimationHideTile(() =>
                        {
                            UnityEngine.Object.Destroy(tile.gameObject);
                            counter++;
                            if (counter >= total)
                            {
                                _tileMatrix = null;
                                onComplete?.Invoke();
                            }
                        });
                    });
                }
            }
        }

        public void GenerateLevel()
        {
            var levelData = _staticDataService.GetLevelData(_currentLevelIndex.ToString());
            int width = levelData.Cells.GetLength(0);
            int height = levelData.Cells.GetLength(1);

            _tileMatrix = new Tile[width, height];
            TileBalanceData tileBalanceData = _staticDataService.Balance.TileBalanceData;

            float baseDelay = 0.02f;

            // 1) Floor tiles.
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    Vector3 spawnPosition = new Vector3(
                        x - width / 2,
                        0f,
                        y - height / 2
                    );

                    Tile tile = _tileFactory.CreateTile(_rootMapHolder);
                    tile.transform.localPosition = spawnPosition;

                    LevelCell cell = levelData.Cells[x, height - 1 - y];
                    bool occupied = cell?.Block != null;
                    bool isEven = (x + y) % 2 == 0;

                    Color tileColor = occupied
                        ? (isEven ? OccupiedColorEven : OccupiedColorNotEven)
                        : (isEven ? tileBalanceData.ColorEven : tileBalanceData.ColorNotEven);
                    tile.SetColor(tileColor);
                    tile.SetUpScale();

                    float delay = (x + y) * baseDelay;
                    DOVirtual.DelayedCall(delay, tile.PlayAnimationShowTile);

                    _tileMatrix[x, y] = tile;
                }
            }

            // 2) Blocks: one per multi-cell object (at its center), one per standalone cell.
            SpawnBlocks(levelData, width, height, baseDelay);
        }

        private void SpawnBlocks(LevelDataDTO levelData, int width, int height, float baseDelay)
        {
            var objects = new Dictionary<int, List<Vector2Int>>();

            for (int cy = 0; cy < height; cy++)
            {
                for (int cx = 0; cx < width; cx++)
                {
                    LevelCell cell = levelData.Cells[cx, cy];
                    if (cell?.Block == null)
                        continue;

                    if (cell.InstanceId == 0)
                    {
                        SpawnBlock(new List<Vector2Int> { new Vector2Int(cx, cy) }, cell, height, baseDelay);
                        continue;
                    }

                    if (!objects.TryGetValue(cell.InstanceId, out var list))
                        objects[cell.InstanceId] = list = new List<Vector2Int>();
                    list.Add(new Vector2Int(cx, cy));
                }
            }

            foreach (var group in objects.Values)
            {
                LevelCell sample = levelData.Cells[group[0].x, group[0].y];
                SpawnBlock(group, sample, height, baseDelay);
            }
        }

        // Spawns a single block at the geometric center of its cells (the midpoint of all
        // the object's tiles). A standalone cell is just a one-cell object.
        private void SpawnBlock(List<Vector2Int> cells, LevelCell cellData, int height, float baseDelay)
        {
            Vector2 centroidCell = Vector2.zero;
            Vector3 centerWorld = Vector3.zero;
            int counted = 0;

            foreach (var c in cells)
            {
                centroidCell += new Vector2(c.x, c.y);

                Tile t = TileForCell(c, height);
                if (t != null)
                {
                    centerWorld += t.transform.position;
                    counted++;
                }
            }

            if (counted == 0)
                return;

            centroidCell /= cells.Count;
            centerWorld /= counted;

            // Cell nearest the centroid owns the block (parent + animation).
            Vector2Int rep = cells[0];
            float bestDist = float.MaxValue;
            foreach (var c in cells)
            {
                float d = (new Vector2(c.x, c.y) - centroidCell).sqrMagnitude;
                if (d < bestDist)
                {
                    bestDist = d;
                    rep = c;
                }
            }

            Tile repTile = TileForCell(rep, height);
            if (repTile == null)
                return;

            GameObject block = _blockFactory.CreateBlock(cellData.Block.Prefab, repTile.gameObject);
            block.transform.rotation = cellData.Rotation;

            repTile.SetBlock(block);
            // SetBlock snaps the block to repTile's anchor; shift it to the object's center.
            block.transform.position += centerWorld - repTile.transform.position;

            int repLoopY = height - 1 - rep.y;
            float delay = (rep.x + repLoopY) * baseDelay;
            DOVirtual.DelayedCall(delay, repTile.PlayAnimationShowBlock);
        }

        private Tile TileForCell(Vector2Int cell, int height)
        {
            int ty = height - 1 - cell.y;
            if (cell.x < 0 || cell.x >= _tileMatrix.GetLength(0) || ty < 0 || ty >= _tileMatrix.GetLength(1))
                return null;

            return _tileMatrix[cell.x, ty];
        }

        public void LoadNextLevel()
        {
            CleanUp(() => GenerateLevel());
            _currentLevelIndex++;
        }
        
        public void Tick(float deltaTime)
        {
            if (!_autoSwitchEnabled)
                return;

            _timer += deltaTime;
            if (_timer >= _nextLevelDelay)
            {
                _timer = 0;
                LoadNextLevel();
            }
        }
        
        public bool HasEnableAutoSwitch() => _autoSwitchEnabled;

        public void EnableAutoSwitch(bool enabled, float delaySeconds = 5f)
        {
            _autoSwitchEnabled = enabled;
            _nextLevelDelay = delaySeconds;
            _timer = 0f;
        }
    }
}
