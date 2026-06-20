using System.Collections.Generic;
using UnityEngine;

namespace Code.LevelEditor.Editor
{
    /// <summary>
    /// Domain mutations on a <see cref="LevelMatrixEditor"/> grid, with no UI deps.
    /// Shared by the grid view, the block popup and the clipboard so the rules for
    /// clearing cells and whole multi-cell instances live in exactly one place.
    /// Callers are responsible for bounds-checking, marking the level dirty and
    /// pushing undo steps — these methods only touch cell data.
    /// </summary>
    public static class LevelGridOps
    {
        /// <summary>All cells belonging to one placed multi-cell instance.</summary>
        public static List<Vector2Int> GetInstanceCells(LevelMatrixEditor level, int id)
        {
            var list = new List<Vector2Int>();
            for (int y = 0; y < level.Height; y++)
            for (int x = 0; x < level.Width; x++)
                if (level.GetCell(x, y).InstanceId == id)
                    list.Add(new Vector2Int(x, y));
            return list;
        }

        /// <summary>Empties a single cell (block, rotation and instance id).</summary>
        public static void ClearCell(LevelMatrixEditor level, Vector2Int pos)
        {
            var c = level.GetCell(pos);
            c.Block = null;
            c.Rotation = Quaternion.identity;
            c.InstanceId = 0;
        }

        /// <summary>Empties every cell of a multi-cell instance.</summary>
        public static void ClearInstance(LevelMatrixEditor level, int id)
        {
            for (int y = 0; y < level.Height; y++)
            for (int x = 0; x < level.Width; x++)
                if (level.GetCell(x, y).InstanceId == id)
                    ClearCell(level, new Vector2Int(x, y));
        }

        /// <summary>
        /// Clears the given cells. Any cell that belongs to a multi-cell instance
        /// clears that whole instance; standalone blocks are cleared individually.
        /// </summary>
        public static void ClearArea(LevelMatrixEditor level, IEnumerable<Vector2Int> cells)
        {
            var instanceIds = new HashSet<int>();
            var standalone = new List<Vector2Int>();

            foreach (var p in cells)
            {
                var c = level.GetCell(p);
                if (c.InstanceId != 0)
                    instanceIds.Add(c.InstanceId);
                else if (c.Block != null)
                    standalone.Add(p);
            }

            foreach (var id in instanceIds)
                ClearInstance(level, id);

            foreach (var p in standalone)
                ClearCell(level, p);
        }
    }
}
