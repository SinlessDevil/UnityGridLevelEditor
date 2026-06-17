using System.Collections.Generic;
using UnityEngine;

namespace Code.LevelEditor
{
    [CreateAssetMenu(menuName = "StaticData/Levels/LevelData", fileName = "LevelData", order = 801)]
    public class LevelMatrixEditor : ScriptableObject
    {
        [SerializeField] private int indexLevel;
        [SerializeField] private int width = 5;
        [SerializeField] private int height = 5;
        [SerializeField] private int nextInstanceId = 1;

        // Unity can't serialize a 2D array, so the grid is stored flat (row-major)
        // and addressed through Index(x, y). Use GetCell / GetLevelDataDto to read it.
        [SerializeField] private LevelCell[] cells = new LevelCell[0];

        public int IndexLevel
        {
            get => indexLevel;
            set => indexLevel = value;
        }

        public int Width => width;
        public int Height => height;

        /// <summary>Returns a fresh instance id for a newly placed multi-cell block.</summary>
        public int NewInstanceId() => nextInstanceId++;

        /// <summary>Current instance-id counter (captured/restored by the editor undo history).</summary>
        public int NextInstanceId => nextInstanceId;

        /// <summary>Deep copy of the grid, used by the editor's undo history.</summary>
        public LevelCell[] CloneCells()
        {
            EnsureInitialized();

            var copy = new LevelCell[cells.Length];
            for (int i = 0; i < cells.Length; i++)
            {
                var c = cells[i];
                copy[i] = new LevelCell
                {
                    Block = c?.Block,
                    Rotation = c?.Rotation ?? Quaternion.identity,
                    InstanceId = c?.InstanceId ?? 0
                };
            }

            return copy;
        }

        /// <summary>Replaces the whole grid from an undo snapshot (deep-copied so the snapshot stays intact).</summary>
        public void RestoreCells(LevelCell[] snapshot, int snapWidth, int snapHeight, int snapNextInstanceId)
        {
            width = Mathf.Max(1, snapWidth);
            height = Mathf.Max(1, snapHeight);
            nextInstanceId = snapNextInstanceId;

            var restored = new LevelCell[width * height];
            for (int i = 0; i < restored.Length; i++)
            {
                var c = snapshot != null && i < snapshot.Length ? snapshot[i] : null;
                restored[i] = c != null
                    ? new LevelCell { Block = c.Block, Rotation = c.Rotation, InstanceId = c.InstanceId }
                    : new LevelCell();
            }

            cells = restored;
        }

        public LevelCell GetCell(int x, int y) => cells[Index(x, y)];
        public LevelCell GetCell(Vector2Int pos) => cells[Index(pos.x, pos.y)];

        public bool InBounds(int x, int y) => x >= 0 && x < width && y >= 0 && y < height;
        public bool InBounds(Vector2Int pos) => InBounds(pos.x, pos.y);

        private int Index(int x, int y) => x + y * width;

        public void EnsureInitialized()
        {
            if (width <= 0) width = 1;
            if (height <= 0) height = 1;

            if (cells == null || cells.Length != width * height)
            {
                Resize(width, height);
                return;
            }

            for (int i = 0; i < cells.Length; i++)
                cells[i] ??= new LevelCell();
        }

        public void Resize(int newWidth, int newHeight)
        {
            newWidth = Mathf.Max(1, newWidth);
            newHeight = Mathf.Max(1, newHeight);

            var newCells = new LevelCell[newWidth * newHeight];

            // Only carry over old cells when the current array is consistent with
            // width/height (a freshly created asset can have a 0-length array while
            // the size fields still hold their defaults).
            bool hasOldGrid = cells != null && cells.Length == width * height;

            for (int y = 0; y < newHeight; y++)
            for (int x = 0; x < newWidth; x++)
            {
                LevelCell preserved = null;
                if (hasOldGrid && x < width && y < height)
                    preserved = cells[x + y * width];

                newCells[x + y * newWidth] = preserved ?? new LevelCell();
            }

            cells = newCells;
            width = newWidth;
            height = newHeight;
        }

        public LevelDataDTO GetLevelDataDto()
        {
            EnsureInitialized();

            var grid = new LevelCell[width, height];
            for (int y = 0; y < height; y++)
            for (int x = 0; x < width; x++)
                grid[x, y] = GetCell(x, y);

            return new LevelDataDTO(grid, indexLevel);
        }

        public IEnumerable<Vector2Int> GetAllOfType(BlockDataEditor type)
        {
            for (int y = 0; y < height; y++)
            for (int x = 0; x < width; x++)
                if (GetCell(x, y)?.Block == type)
                    yield return new Vector2Int(x, y);
        }

        private void OnValidate() => EnsureInitialized();
    }
}
